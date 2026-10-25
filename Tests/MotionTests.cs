using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static class MotionTests
    {
        public static void Timing()
        {
            foreach (int hz in new[] { 30, 60, 120 })
            {
                float position = 0, velocity = 0;
                for (int i = 0; i < hz * 2; i++)
                {
                    float next = Motion.Damp(position, 110, ref velocity, 1f / hz, .18f);
                    TestSuite.Check(next >= position && next <= 110, "camera converges without overshoot at " + hz);
                    if (i == 0) TestSuite.Check(next > 0 && next < 30, "first frame moves only part of wheel step");
                    position = next;
                }
                TestSuite.Check(position == 110 && velocity == 0, "same resting position at every frame rate");
                for (int i = 0; i < hz * 2; i++) position = Motion.Damp(position, -50, ref velocity, 1f / hz, .18f);
                TestSuite.Check(position == -50 && velocity == 0, "reverse scroll settles");
            }
            float speed = 10;
            TestSuite.Check(Motion.Damp(5, 20, ref speed, 0, .18f) == 5 && speed == 10, "zero elapsed time preserves motion");
            TestSuite.Check(Motion.Ease(-1) == 0 && Motion.Ease(2) == 1, "pose easing clamps endpoints");
        }
    }

    public static partial class UiTests
    {
        public static void TestSmoothMotion()
        {
            using (var game = new Game())
            {
                game.CreateControl();
                game.ClientSize = new Size(1280, 840);
                Write(game, "selected", 3);
                Call(game, "OpenCheckout"); Call(game, "TakeBuff"); Call(game, "OpenActMap");
                float start = Read<float>(game, "mapScroll");
                Call(game, "ScrollMap", -110f);
                TestSuite.Check(Read<float>(game, "mapScroll") == start, "wheel changes destination without jumping camera");
                Call(game, "AdvanceMapMotion", .016f);
                float first = Read<float>(game, "mapScroll");
                TestSuite.Check(first < start && first > start - 30, "camera advances smoothly on next frame");
                Key(game, Keys.Escape);
                Call(game, "AdvanceMapMotion", .05f);
                TestSuite.Check(Read<float>(game, "mapScroll") == first, "pause freezes camera");
                Key(game, Keys.Escape);
                SettleMap(game);
                TestSuite.Check(Read<float>(game, "mapScroll") == start - 110, "camera resumes and reaches wheel destination");
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < 20; i++)
                {
                    Call(game, "ScrollMap", -5f); Call(game, "AdvanceMapMotion", .016f);
                    using (Render(game)) { }
                }
                watch.Stop();
                TestSuite.Note("Map render + camera update at 1280x840: " + (watch.Elapsed.TotalMilliseconds / 20).ToString("F1") + " ms/frame (offscreen sample, not a live FPS guarantee).");
                Key(game, Keys.D1);
                TestSuite.Check(Read<float>(game, "mapScrollVelocity") == 0, "entering battle cancels camera momentum");
                Call(game, "ReturnToMainMenu");
                TestSuite.Check(!Read<Timer>(game, "combatTimer").Enabled, "menu releases idle animation timer");
                Call(game, "StartTestBattle"); Call(game, "EndCombatTurn");
                foreach (float delta in new[] { .1f, .16f, .14f, .14f, .18f, .34f })
                {
                    Call(game, "AdvanceCombatAnimation", delta);
                    using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "bite-" + Read<AttackTimeline>(game, "attackTimeline").Time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ".png"));
                }
                TestSuite.Check(Read<CombatState>(game, "combat").Hero.Health == 45, "entire bite animation hits exactly once");
                TestSuite.Check(Read<ImageAssets>(game, "assetStore").Count == 31, "pose changes reuse cached atlases");
            }
        }
    }
}
