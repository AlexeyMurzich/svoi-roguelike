using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShpilSvoih
{
    // Issue #6: animation/sound synchronisation and motion-clock regressions.
    public static partial class UiTests
    {
        sealed class SilentVoice : IMusicVoice, IAudioVolumeVoice
        {
            public void SetPaused(bool value) { }
            public void SetVolume(double value) { }
            public void Dispose() { }
        }

        static void MotionTick(Game game, float step)
        {
            // The same order as OnMotionTick in a live fight.
            Call(game, "AdvanceAudio", step);
            Call(game, "AdvanceLivingAnimation", step);
            Call(game, "AdvanceCombatAmbient", step);
            Call(game, "AdvanceCombatAnimation", step);
        }

        public static void TestFanEntranceFrameRate()
        {
            float expected = 1f - .5f / AttackTimeline.Speed;
            foreach (int fps in new[] { 10, 30, 60, 120 })
                using (var game = new Game())
                {
                    game.CreateControl(); game.ClientSize = new Size(1280, 840); Call(game, "StartTestFomatreshka");
                    Call(game, "EndCombatTurn");
                    TestSuite.Check(Read<AttackTimeline>(game, "attackTimeline").Kind == AttackAnimation.FomatreshkaSummon, "first enemy action is the summon");
                    for (int frame = 0; frame < fps; frame++) MotionTick(game, 1f / fps);
                    float entrance = Read<float>(game, "fomatreshkaEntrance");
                    TestSuite.Check(Read<CombatState>(game, "combat").Enemies.Count == 4, "summon impact applied once");
                    TestSuite.Check(Math.Abs(entrance - expected) < .002f,
                        "fan run-in starts exactly at the summon impact at " + fps + " Hz: " + entrance + " expected " + expected);
                }
        }

        static int OpaquePixels(Bitmap image)
        {
            int count = 0;
            for (int y = 0; y < image.Height; y += 2)
                for (int x = 0; x < image.Width; x += 2)
                    if (image.GetPixel(x, y).A > 0) count++;
            return count;
        }

        public static void TestSkaterProjectileEndsAtImpact()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840); Call(game, "StartTestSkaters");
                var timeline = Read<AttackTimeline>(game, "attackTimeline");
                foreach (var kind in new[] { AttackAnimation.SkaterSlash, AttackAnimation.PadawanShot })
                {
                    timeline.Cancel();
                    Write(game, "actingEnemyIndex", kind == AttackAnimation.SkaterSlash ? 0 : 1);
                    int impacts = 0;
                    timeline.Start(kind, () => impacts++);
                    float hit = timeline.HitTime;
                    foreach (float at in new[] { (hit + .18f) / 2, hit - .01f, hit + .01f, hit + .1f, timeline.Duration - .01f })
                    {
                        timeline.Advance((at - timeline.Time) / AttackTimeline.Speed);
                        using (var layer = new Bitmap(1280, 840, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                        {
                            using (var graphics = Graphics.FromImage(layer)) Call(game, "DrawSkaterAttack", graphics);
                            int pixels = OpaquePixels(layer);
                            if (timeline.Time <= hit) TestSuite.Check(pixels > 0, kind + " projectile visible in flight at " + timeline.Time);
                            else TestSuite.Check(pixels == 0, kind + " projectile gone after impact at " + timeline.Time);
                        }
                    }
                    TestSuite.Check(impacts == 1, kind + " applies one impact");
                }
                timeline.Cancel();
            }
        }

        public static void TestConsoleResumesSoundClock()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                Call(game, "ReturnToMainMenu"); Write(game, "selected", 3);
                Call(game, "OpenCheckout"); Call(game, "TakeBuff"); Call(game, "OpenActMap");
                var clip = new MusicPcm(44100, 2, new byte[44100 * 4]);
                var sounds = new CardSoundPlayer((hero, attack) => clip, pcm => new SilentVoice());
                Write(game, "cardSounds", sounds);
                sounds.Play(3, true, false);
                TestSuite.Check(!(bool)typeof(Game).GetProperty("LivingScene", PrivateInstance).GetValue(game, null) &&
                    !(bool)typeof(Game).GetProperty("MapMoving", PrivateInstance).GetValue(game, null), "quiet map: only the sound tail needs the clock");
                Call(game, "SetDebugConsole", true);
                var timer = Read<Timer>(game, "combatTimer");
                TestSuite.Check(!timer.Enabled, "console stops the motion clock");
                Call(game, "SetDebugConsole", false);
                TestSuite.Check(timer.Enabled, "closing the console resumes the clock that releases finite sound tails");
                Call(game, "ReturnToMainMenu");
                TestSuite.Check(sounds.ActiveVoices == 0, "leaving the run releases the card voice");
                sounds.Dispose();
            }
        }
    }
}
