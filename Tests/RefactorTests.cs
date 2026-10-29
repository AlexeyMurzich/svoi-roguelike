using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        public static void TestRefactorBoundaries()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                Call(game, "StartTestDogs");
                var hero = new HeroState(3, 50, 0, new Balance()); hero.Effects.Choose("ring");
                hero.AddCard(new CardData { Attack = true, Damage = 30, GoldCost = 0 });
                var fight = ReplaceFight(game, hero, EnemyKind.StrayDogs);
                Call(game, "PlayCombatCard", 0);
                var timeline = Read<AttackTimeline>(game, "attackTimeline");
                foreach (float bad in new[] { float.NaN, float.PositiveInfinity, -1f, 0f })
                    Call(game, "AdvanceCombatAnimation", bad);
                TestSuite.Check(timeline.Time == 0 && fight.Enemies[0].Health == 30, "invalid elapsed time does not corrupt pose or combat");
                Call(game, "AdvanceCombatAnimation", .5f);
                var deaths = Read<DeathVisuals>(game, "deathVisuals");
                TestSuite.Check(deaths.SnapshotCount == 1, "death is captured at impact before any repaint");
                game.ClientSize = new Size(1920, 1080);
                using (Render(game)) { }
                TestSuite.Check(deaths.SnapshotCount == 1, "resizing reuses impact snapshot");
                Call(game, "AdvanceCombatAnimation", 1f);
                TestSuite.Check(deaths.SnapshotCount == 0 && !deaths.IsRunning, "impact snapshot releases after duration");

                var card = new CardData { Attack = true, Damage = 6, GoldCost = 10 };
                hero = new HeroState(3, 50, 0, new Balance()); hero.Effects.Choose("ring"); hero.AddCard(card);
                fight = new CombatState(hero, new Random(1));
                decimal money = hero.Money;
                card.Block = -10;
                TestSuite.Check(!fight.CanPlay(0) && !fight.TryPlay(0) && fight.Block == 0 && hero.Money == money && fight.Hand.Count == 1,
                    "mutated invalid card cannot consume resources or create negative block");
                card.Block = 0;
                TestSuite.Check(fight.TryPlay(0), "valid card can still be played");
                game.Dispose(); game.Dispose();
                TestSuite.Check(Read<ImageAssets>(game, "assetStore").Count == 0, "repeated dispose is safe");
            }
            using (var deaths = new DeathVisuals())
            using (var bitmap = new Bitmap(400, 400))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                deaths.Start(1);
                TestSuite.Throws<InvalidOperationException>(() => deaths.Capture(1, new RectangleF(20, 20, 80, 100), g => { throw new InvalidOperationException(); }), "capture failure propagates");
                TestSuite.Check(deaths.SnapshotCount == 0, "failed capture does not retain a partial bitmap");
                deaths.Capture(1, new RectangleF(20, 20, 80, 100), g => g.FillRectangle(Brushes.Red, 20, 20, 80, 100));
                deaths.Draw(graphics, 1, new RectangleF(100, 100, 160, 200), null);
                TestSuite.Check(bitmap.GetPixel(250, 290).R > 200 && bitmap.GetPixel(80, 80).A == 0, "snapshot scales with resized bounds and follows new anchor");
            }
        }

        public static void TestRefactorTiming()
        {
            using (var game = new Game())
            {
                game.CreateControl();
                foreach (int hz in new[] { 10, 30, 60, 120 })
                {
                    Call(game, "StartTestDogs");
                    var fight = Read<CombatState>(game, "combat");
                    Call(game, "EndCombatTurn");
                    int frames = 0;
                    while (Read<AttackTimeline>(game, "attackTimeline").IsRunning && frames < hz * 3)
                    {
                        Call(game, "AdvanceCombatAnimation", 1f / hz); frames++;
                    }
                    TestSuite.Check(fight.Turn == 2 && fight.Hero.Health == 40, "two enemy impacts at " + hz + " Hz");
                    TestSuite.Check(Math.Abs(frames / (float)hz - 1.75f) <= 1.01f / hz,
                        "queue carries frame remainder at " + hz + " Hz");
                }
            }
        }

        public static void TestRefactorArt()
        {
            using (var game = new Game())
            using (var sheet = new Bitmap(1280, 1140))
            using (var graphics = Graphics.FromImage(sheet))
            using (var font = new Font("Segoe UI", 14))
            {
                game.CreateControl(); Call(game, "StartTestBattle");
                var atlas = Read<object>(game, "shutenPoses");
                TestSuite.Check((int)atlas.GetType().GetField("FrameCount").GetValue(atlas) == 4, "idle, draw, release and guard share a four-frame atlas");
                var timeline = Read<AttackTimeline>(game, "attackTimeline");
                graphics.Clear(Color.FromArgb(28, 36, 40));
                int index = 0;
                int referenceFloor = -1;
                foreach (var kind in new[] { AttackAnimation.HeroStrike, AttackAnimation.HeroGuard })
                    foreach (float time in new[] { 0f, .1f, .25f, .4f, .55f, .75f })
                    {
                        timeline.Cancel(); timeline.Start(kind, () => { });
                        timeline.Advance(Math.Min(time, timeline.Duration - .001f) / AttackTimeline.Speed);
                        int x = index % 4 * 320, y = index / 4 * 380;
                        graphics.DrawString(kind + " " + timeline.Time.ToString("F2"), font, Brushes.White, x + 8, y + 8);
                        var saved = graphics.Save();
                        graphics.InterpolationMode = InterpolationMode.Bilinear;
                        Call(game, "DrawShutenPose", graphics, new RectangleF(x + 5, y + 35, 310, 335));
                        graphics.Restore(saved); index++;
                        using (var pose = new Bitmap(340, 360, PixelFormat.Format32bppPArgb))
                        using (var poseGraphics = Graphics.FromImage(pose))
                        {
                            Call(game, "DrawShutenPose", poseGraphics, new RectangleF(10, 10, 310, 335));
                            int floor = -1;
                            for (int py = 359; py >= 0 && floor < 0; py--)
                                for (int px = 0; px < pose.Width; px++)
                                    if (pose.GetPixel(px, py).A > 128) { floor = py; break; }
                            if (referenceFloor < 0) referenceFloor = floor;
                            TestSuite.Check(Math.Abs(floor - referenceFloor) <= 1, "bike base stays on the floor through " + kind + " at " + time);
                        }
                    }
                timeline.Cancel();
                sheet.Save(Path.Combine(TestSuite.OutputDirectory, "shuten-animation-review.png"));
                TestSuite.Check((Read<ImageAssets>(game, "assetStore").Count - HeroArtCount(game)) == 37, "one shared Shuten atlas replaces two source bitmaps");
            }
        }
    }
}
