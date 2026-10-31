using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        static void PaintClient(Game game, Bitmap image, Rectangle clip)
        {
            using (var graphics = Graphics.FromImage(image))
            using (var paint = new PaintEventArgs(graphics, clip))
            {
                graphics.SetClip(clip);
                Call(game, "OnPaint", paint);
            }
        }
        static bool SamePixels(Bitmap a, Bitmap b)
        {
            var rect = new Rectangle(Point.Empty, a.Size);
            var left = a.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            var right = b.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                var x = new byte[a.Width * 4]; var y = new byte[x.Length];
                for (int row = 0; row < a.Height; row++)
                {
                    Marshal.Copy(IntPtr.Add(left.Scan0, row * left.Stride), x, 0, x.Length);
                    Marshal.Copy(IntPtr.Add(right.Scan0, row * right.Stride), y, 0, y.Length);
                    if (!x.SequenceEqual(y)) return false;
                }
                return true;
            }
            finally { a.UnlockBits(left); b.UnlockBits(right); }
        }
        public static void TestPartialRepaint()
        {
            foreach (var size in new[] { new Size(1280, 840), new Size(1920, 1080) })
                foreach (string launch in new[] { "StartTestBattle", "StartTestDogs", "StartTestHedgehogs", "StartTestMimicrator", "StartTestIllyaToha", "StartTestBabai", "StartTestDoctors", "StartTestSkaters", "StartTestFomatreshka" })
                    using (var game = new Game())
                    using (var partial = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb))
                    using (var full = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb))
                    {
                        game.ClientSize = size; Call(game, launch);
                        if (launch == "StartTestFomatreshka")
                        {
                            // Summon the fans, then replay their run-in: the actor band must contain it.
                            Call(game, "EndCombatTurn");
                            for (int n = 0; n < 200 && (Read<CombatState>(game, "combat").Phase != CombatPhase.Player || (bool)typeof(Game).GetProperty("CombatBusy", PrivateInstance).GetValue(game, null)); n++)
                                Call(game, "AdvanceCombatAnimation", .05f);
                            TestSuite.Check(Read<CombatState>(game, "combat").Enemies.Count == 4, "fans summoned before the repaint check");
                            Write(game, "fomatreshkaEntrance", 0f);
                        }
                        var timeline = Read<AttackTimeline>(game, "attackTimeline"); timeline.Cancel();
                        var all = new Rectangle(Point.Empty, size);
                        PaintClient(game, partial, all);
                        foreach (bool animated in new[] { false, true })
                        {
                            if (animated)
                            {
                                var kind = launch == "StartTestBabai" ? AttackAnimation.BabaiLasso : launch == "StartTestIllyaToha" ? AttackAnimation.TohaRam : launch == "StartTestMimicrator" ? AttackAnimation.MimicratorBeacon : launch == "StartTestDogs" ? AttackAnimation.DogBite :
                                    launch == "StartTestDoctors" ? AttackAnimation.RatBite : launch == "StartTestSkaters" ? AttackAnimation.SkaterSlash : launch == "StartTestFomatreshka" ? AttackAnimation.FanStrike : AttackAnimation.HeroStrike;
                                Write(game, "actingEnemyIndex", launch == "StartTestFomatreshka" ? 1 : 0); timeline.Start(kind, () => { });
                                PaintClient(game, partial, all);
                            }
                            for (int frame = 0; frame < 5; frame++)
                            {
                                Call(game, "AdvanceLivingAnimation", .06f); Call(game, "AdvanceCombatAmbient", .06f);
                                if (animated) Call(game, "AdvanceCombatAnimation", .06f);
                                var damage = (Rectangle)typeof(Game).GetProperty("MotionDamage", PrivateInstance).GetValue(game, null);
                                PaintClient(game, partial, damage); PaintClient(game, full, all);
                                bool same = SamePixels(partial, full);
                                if (!same) { partial.Save(System.IO.Path.Combine(TestSuite.OutputDirectory, "partial-failure.png")); full.Save(System.IO.Path.Combine(TestSuite.OutputDirectory, "full-failure.png")); }
                                TestSuite.Check(same, "partial frame matches every pixel of full frame: " + launch + " " + size + " animated=" + animated + " frame=" + frame);
                            }
                        }
                    }
        }
        public static void TestHoverInvalidation()
        {
            using (var game = new Game())
            {
                var handle = game.Handle; game.ClientSize = new Size(1280, 840); Call(game, "StartTestBattle");
                using (Render(game)) { }
                int paints = 0; game.Invalidated += (s, e) => paints++;
                for (int i = 0; i < 1000; i++) Call(game, "UpdateCombatHover", new PointF(100 + i % 50, 200), true);
                TestSuite.Check(paints == 0, "one thousand empty mouse movements request no paints");
                var areas = Read<System.Collections.Generic.List<Tuple<RectangleF, CombatHint>>>(game, "combatHintAreas");
                var area = areas[0].Item1;
                Call(game, "UpdateCombatHover", new PointF(area.X + 5, area.Y + 5), true);
                TestSuite.Check(paints == 1, "entering a tooltip requests one paint");
                Call(game, "UpdateCombatHover", new PointF(area.X + 8, area.Y + 8), true);
                TestSuite.Check(paints == 1, "moving inside same tooltip does not redraw");
                Call(game, "UpdateCombatHover", new PointF(110, 200), true);
                TestSuite.Check(paints == 2, "leaving tooltip redraws to erase it");
                bool full = false;
                game.Invalidated += (s, e) => { if (e.InvalidRect == game.ClientRectangle) full = true; };
                Call(game, "StartCombatAnimation", AttackAnimation.HeroGuard, (Action)(() => { }));
                full = false;
                var timeline = Read<AttackTimeline>(game, "attackTimeline");
                Call(game, "AdvanceCombatAnimation", (timeline.HitTime + .01f) / AttackTimeline.Speed);
                TestSuite.Check(full, "impact requests a full repaint for changed resources and cards");
                timeline.Cancel(); timeline.Start(AttackAnimation.TohaTransform, () => { });
                var damage = (Rectangle)typeof(Game).GetProperty("MotionDamage", PrivateInstance).GetValue(game, null);
                TestSuite.Check(damage == game.ClientRectangle, "background transformations always redraw the whole viewport");
            }
        }
    }
}
