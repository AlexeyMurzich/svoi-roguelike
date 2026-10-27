using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;

namespace ShpilSvoih
{
    public static class TohaSoundTests
    {
        public sealed class Voice : IMusicVoice
        {
            public bool Paused = true, Disposed;
            public int Changes;
            public void SetPaused(bool value) { Paused = value; Changes++; }
            public void Dispose() { Disposed = true; }
        }
        public static void Lifecycle()
        {
            var voices = new List<Voice>(); var loaded = new List<bool>();
            var light = new MusicPcm(48000, 2, new byte[16]);
            var dark = new MusicPcm(48000, 2, new byte[32]);
            using (var sound = new TohaAttackSound(isDark => { loaded.Add(isDark); return isDark ? dark : light; }, pcm =>
            {
                TestSuite.Check(pcm == (voices.Count == 1 ? dark : light), "correct light/dark PCM passed to voice");
                var voice = new Voice(); voices.Add(voice); return voice;
            }))
            {
                sound.Play(false, false);
                TestSuite.Check(voices.Count == 1 && !voices[0].Paused, "one attack starts one voice");
                for (int i = 0; i < 1000; i++) sound.SetPaused(false);
                TestSuite.Check(voices[0].Changes == 1, "frame updates do not restart or call native audio repeatedly");
                sound.SetPaused(true); sound.SetPaused(true);
                TestSuite.Check(voices[0].Paused && voices[0].Changes == 2, "pause applied once");
                sound.SetPaused(false);
                TestSuite.Check(!voices[0].Paused && voices.Count == 1, "resume preserves existing voice");
                sound.Play(true, false);
                TestSuite.Check(voices[0].Disposed && !voices[1].Paused, "dark ram replaces old effect without overlap");
                sound.Play(false, true);
                TestSuite.Check(voices[1].Disposed && voices[2].Paused && loaded.Count == 2 && !loaded[0] && loaded[1], "two clips decoded once, paused start stays silent");
                sound.Stop(); sound.Stop();
                TestSuite.Check(voices[2].Disposed, "exit cleanup is idempotent");
                sound.Dispose(); sound.Play(true, false);
                TestSuite.Check(voices.Count == 3, "disposed sound cannot restart");
            }
            using (var failed = new TohaAttackSound(d => light, pcm => { throw new InvalidOperationException("No device"); }))
            {
                failed.Play(false, false);
                TestSuite.Check(failed.LastError == "No device", "missing audio device does not break combat");
            }
        }
        public static void Embedded()
        {
            string[] hashes = { "7cb88b317647f0a832e00b75f3bb7b091887986f8a974a7450d6bf14894d7135", "b7e27f2ad10a5e1f943f5a9a462840fc86a90ea63bfadb1c0dd005f29e0a7d2e" };
            int[] frames = { 68160, 74400 };
            for (int i = 0; i < 2; i++)
            {
                var pcm = TohaAttackSound.LoadEmbedded(i == 1);
                TestSuite.Check(pcm.SampleRate == 48000 && pcm.Channels == 2 && pcm.Data.Length == frames[i] * 4, "approved duration and format retained");
                using (var sha = SHA256.Create())
                    TestSuite.Check(BitConverter.ToString(sha.ComputeHash(pcm.Data)).Replace("-", "").ToLowerInvariant() == hashes[i], "approved preview samples unchanged");
            }
        }
        public static void NativeOneShot()
        {
            var pcm = new MusicPcm(48000, 2, new byte[48000 * 4 / 5]);
            using (var voice = new LoopingMusicVoice(pcm, false))
            {
                voice.SetPaused(false); Thread.Sleep(75);
                voice.SetPaused(true); Thread.Sleep(70);
                uint position = voice.PlayedBytes;
                Thread.Sleep(80);
                TestSuite.Check(voice.PlayedBytes == position, "native one-shot pauses at its current position");
                voice.SetPaused(false); Thread.Sleep(400);
                uint finished = voice.PlayedBytes;
                Thread.Sleep(200);
                TestSuite.Check(finished >= pcm.Data.Length && voice.PlayedBytes == finished, "native effect ends instead of looping");
            }
        }
    }
    public static partial class UiTests
    {
        public static void TestTohaSoundScenes()
        {
            var voice = new TohaSoundTests.Voice();
            using (var game = new Game())
            {
                game.CreateControl(); Call(game, "StartTestToha");
                Read<TohaAttackSound>(game, "tohaSound").Dispose();
                var sound = new TohaAttackSound(d => new MusicPcm(48000, 2, new byte[16]), pcm => voice);
                Write(game, "tohaSound", sound);
                sound.Play(false, false);
                Call(game, "OnDeactivate", EventArgs.Empty);
                TestSuite.Check(voice.Paused && !voice.Disposed, "focus loss pauses the motor");
                sound.SetPaused(false); Call(game, "SetInventory", "pause");
                TestSuite.Check(voice.Paused, "pause menu suspends motor");
                sound.SetPaused(false); Call(game, "SetDebugConsole", true);
                TestSuite.Check(voice.Paused, "console suspends motor");
                Call(game, "SetDebugConsole", false); Call(game, "SetInventory", "");
                Call(game, "ReturnToMainMenu");
                TestSuite.Check(voice.Disposed, "leaving battle releases effect voice");
                Call(game, "StartTestToha");
                Call(game, "EndCombatTurn");
                TestSuite.Check(voice.Disposed, "hidden test windows never open native effect playback");
            }
        }
    }
}
