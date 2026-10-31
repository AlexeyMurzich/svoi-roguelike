using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ShpilSvoih
{
    public static class SkaterAudioTests
    {
        sealed class Voice : IMusicVoice, IAudioVolumeVoice
        {
            public bool Paused = true, Disposed;
            public double Gain;
            public void SetPaused(bool value) { Paused = value; }
            public void SetVolume(double value) { Gain = value; }
            public void Dispose() { Disposed = true; }
        }
        public static void ClipsAndLifetime()
        {
            var sword = SkaterAttackSound.LoadEmbedded(true); var blaster = SkaterAttackSound.LoadEmbedded(false);
            TestSuite.Check(sword.SampleRate == 24000 && sword.Channels == 2 && sword.Data.Length == 54720 * 4, "entire supplied saber clip embedded");
            TestSuite.Check(blaster.SampleRate == 24000 && blaster.Channels == 2 && blaster.Data.Length == 46080 * 4, "entire supplied blaster clip embedded");
            int loads = 0; var voices = new List<Voice>();
            using (var sound = new SkaterAttackSound(master => { loads++; return master ? sword : blaster; }, pcm => { var v = new Voice(); voices.Add(v); return v; }))
            {
                sound.Play(0, false); sound.Play(1, false); sound.Play(2, false);
                TestSuite.Check(sound.PlayCount == 3 && loads == 2 && sound.ActiveVoices == 3, "one voice per actor, shared blaster decode");
                TestSuite.Check(voices.TrueForAll(v => v.Gain == AudioLevels.Enemy && !v.Paused), "fixed existing enemy volume");
                sound.SetPaused(true); sound.Advance(10);
                TestSuite.Check(sound.ActiveVoices == 3 && voices.TrueForAll(v => v.Paused), "pause holds finite voices without consuming duration");
                sound.SetPaused(false); sound.Advance(1);
                TestSuite.Check(sound.ActiveVoices == 3, "short native tails are not cut by next actor");
                sound.Play(1, false);
                TestSuite.Check(sound.ActiveVoices == 3 && voices[1].Disposed && loads == 2, "replacing same actor is bounded and cached");
                sound.Advance(5); TestSuite.Check(sound.ActiveVoices == 0 && voices.TrueForAll(v => v.Disposed), "finite clips release all voices after EOF");
                sound.Play(0, true); sound.Stop(); TestSuite.Check(sound.ActiveVoices == 0, "scene reset stops paused voice");
            }
            TestSuite.Check(voices.TrueForAll(v => v.Disposed), "close disposes every voice");
        }
    }
    public static partial class UiTests
    {
        public static void TestSkaterAudioAndLabels()
        {
            using (var game = new Game())
            {
                game.CreateControl(); Call(game, "StartTestSkaters");
                TestSuite.Check(MusicChoice(game) == "skaters", "skater encounter selects supplied main title theme");
                foreach (var size in new[] { new Size(1280, 840), new Size(1920, 1080), new Size(900, 1400) })
                {
                    game.ClientSize = size;
                    using (var shot = Render(game)) shot.Save(Path.Combine(TestSuite.OutputDirectory, "skater-labels-" + size.Width + ".png"));
                }
                var sounds = Read<SkaterAttackSound>(game, "skaterSounds");
                TestSuite.Check(sounds.PlayCount == 0, "repaint and offscreen tests do not emit attack sounds");
                Call(game, "EndCombatTurn"); Call(game, "AdvanceCombatAnimation", 5f);
                TestSuite.Check(MusicChoice(game) == "skaters" && Read<CombatState>(game, "combat").Hero.Health == 30, "music identity and exact attack damage preserved");
                Call(game, "SetInventory", "pause"); Call(game, "SetInventory", "");
                TestSuite.Check(MusicChoice(game) == "skaters", "pause does not select a different theme");
                Call(game, "StartTestDogs"); TestSuite.Check(MusicChoice(game) == "", "other ordinary encounter does not inherit skater song");
                Call(game, "ReturnToMainMenu"); TestSuite.Check(sounds.ActiveVoices == 0, "leaving combat releases effects");
            }
        }
    }
}
