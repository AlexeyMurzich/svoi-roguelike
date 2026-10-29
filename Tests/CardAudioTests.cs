using System;
using System.Collections.Generic;
using System.Threading;

namespace ShpilSvoih
{
    public static class CardAudioTests
    {
        sealed class Voice : IMusicVoice, IAudioVolumeVoice
        {
            public bool Paused = true, Disposed;
            public double Gain;
            public void SetPaused(bool value) { Paused = value; }
            public void SetVolume(double gain) { Gain = gain; }
            public void Dispose() { Disposed = true; }
        }
        public static void Voices()
        {
            var made = new List<Voice>(); int loads = 0;
            using (var player = new CardSoundPlayer((hero, attack) => { loads++; return new MusicPcm(44100, 2, new byte[44100 * 4]); },
                pcm => { var v = new Voice(); made.Add(v); return v; }))
            {
                player.Prepare(3); player.Prepare(3);
                TestSuite.Check(loads == 2, "both hero clips cached before click");
                player.Play(3, true, false);
                TestSuite.Check(!made[0].Paused && made[0].Gain == AudioLevels.Card, "foreground starts once at central quiet level");
                player.Advance(.2); player.Play(3, false, false);
                TestSuite.Check(player.ActiveVoices == 2, "new sound keeps only one previous tail");
                player.Advance(.045);
                TestSuite.Check(made[0].Gain < AudioLevels.Card * .6 && made[1].Gain == AudioLevels.Card, "old tail fades without reducing new transient");
                player.SetPaused(true); player.Advance(10);
                TestSuite.Check(player.ActiveVoices == 2 && made[0].Paused && made[1].Paused, "pause freezes voices and fade time");
                player.SetPaused(false); player.Advance(.05);
                TestSuite.Check(made[0].Disposed && player.ActiveVoices == 1, "tail ends after 90 ms of active time");
                player.Play(3, true, false); player.Play(3, true, false);
                TestSuite.Check(player.ActiveVoices == 2 && made[1].Disposed, "rapid bursts cannot grow voice count");
                player.SoftenPrevious(); player.Advance(.10);
                TestSuite.Check(player.ActiveVoices == 0, "enemy action clears hero tails before impact");
                player.Play(3, true, true); player.Advance(2);
                TestSuite.Check(player.ActiveVoices == 1 && made[made.Count - 1].Paused, "paused start stays silent");
                player.SetPaused(false); player.Advance(1.01);
                TestSuite.Check(player.ActiveVoices == 0, "completed one-shot releases native resources");
                player.Stop(); player.Stop(); player.Dispose(); player.Play(3, true, false);
                TestSuite.Check(player.ActiveVoices == 0 && loads == 2, "dispose and reset are idempotent");
            }
            foreach (var v in made) TestSuite.Check(v.Disposed, "every voice disposed");
            using (var failed = new CardSoundPlayer((hero, attack) => new MusicPcm(44100, 2, new byte[16]),
                pcm => { throw new InvalidOperationException("No audio device"); }))
            {
                failed.Play(0, true, false);
                TestSuite.Check(failed.ActiveVoices == 0 && failed.LastError == "No audio device", "device failure does not change gameplay");
            }
        }
        public static void Embedded()
        {
            double[] attack = { 1.85, .95, 1, 1.05 }, guard = { 1.8, .85, .8, .8 };
            for (int hero = 0; hero < 4; hero++)
                foreach (bool isAttack in new[] { true, false })
                {
                    var pcm = CardSoundPlayer.LoadEmbedded(hero, isAttack);
                    TestSuite.Check(pcm.SampleRate == 44100 && pcm.Channels == 2, "stereo format embedded");
                    TestSuite.Check(pcm.Data.Length == Math.Round((isAttack ? attack[hero] : guard[hero]) * 44100) * 4, "full preparation and tail preserved");
                    TestSuite.Check(Array.Exists(pcm.Data, x => x != 0), "non-silent original effect");
                }
            TestSuite.Throws<ArgumentOutOfRangeException>(() => CardSoundPlayer.LoadEmbedded(4, true), "invalid hero rejected");
            MusicPcm automatic = null;
            using (var player = new CardSoundPlayer(CardSoundPlayer.LoadEmbedded, pcm => { automatic = pcm; return new Voice(); }))
            {
                player.Play(1, true, false, true);
                var full = CardSoundPlayer.LoadEmbedded(1, true);
                int skip = (int)Math.Round(.5 / 1.2 * 44100) * 4;
                TestSuite.Check(automatic.Data.Length == full.Data.Length - skip, "automatic accent skips already-resolved preparation");
                for (int at = 66 * 4; at < automatic.Data.Length; at++)
                    TestSuite.Check(automatic.Data[at] == full.Data[at + skip], "automatic tail remains sample-exact after fade");
                TestSuite.Check(automatic.Data[0] == 0 && automatic.Data[1] == 0, "trimmed accent starts without a discontinuity");
            }
        }
        public static void MusicGainAndNativeMix()
        {
            var made = new Voice();
            using (var player = new MusicPlayer((id, token) => new MusicPcm(44100, 2, new byte[17640]), pcm => made))
            {
                player.SetVolume(AudioLevels.Music); player.SetTrack("shuten", false);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                while (player.IsLoading && watch.ElapsedMilliseconds < 5000) Thread.Sleep(5);
                TestSuite.Check(!player.IsLoading && made.Gain == AudioLevels.Music && !made.Paused, "late load starts at fixed central level");
                player.SetVolume(AudioLevels.Music);
                TestSuite.Check(made.Gain == AudioLevels.Music, "repeated synchronization cannot change background level");
            }
            var pcmSilence = new MusicPcm(44100, 2, new byte[17640]);
            using (var music = new LoopingMusicVoice(pcmSilence))
            using (var effect = new LoopingMusicVoice(pcmSilence, false))
            {
                music.SetVolume(AudioLevels.Music); effect.SetVolume(AudioLevels.Card);
                TestSuite.Check(LoopingMusicVoice.OpenDeviceCount == 1, "music and effect share one output device");
                music.SetPaused(false); Thread.Sleep(130);
                uint before = music.PlayedBytes;
                uint actualBefore = LoopingMusicVoice.OutputPlayedBytes;
                effect.SetPaused(false); Thread.Sleep(200);
                var deadline = System.Diagnostics.Stopwatch.StartNew();
                while (effect.PlayedBytes < pcmSilence.Data.Length && deadline.ElapsedMilliseconds < 500) Thread.Sleep(5);
                TestSuite.Note("Mixer positions: music " + before + " -> " + music.PlayedBytes + "; effect " + effect.PlayedBytes + "/" + pcmSilence.Data.Length + "; error=" + LoopingMusicVoice.OutputError);
                TestSuite.Check(music.Volume == AudioLevels.Music, "effect level cannot change music channel");
                TestSuite.Check(music.PlayedBytes > before && effect.PlayedBytes >= pcmSilence.Data.Length, "native effect finishes without stopping looping music");
                TestSuite.Check(LoopingMusicVoice.OutputPlayedBytes > actualBefore, "real Windows output advances, not only the software cursor");
                music.SetPaused(true); effect.SetPaused(true); Thread.Sleep(60);
                uint held = music.PlayedBytes; Thread.Sleep(70);
                TestSuite.Check(music.PlayedBytes == held, "native mix pauses exactly");
                TestSuite.Throws<ArgumentOutOfRangeException>(() => music.SetVolume(double.NaN), "NaN volume rejected");
            }
        }
    }
}
