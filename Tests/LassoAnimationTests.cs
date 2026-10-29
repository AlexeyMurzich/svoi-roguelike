using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        public static void TestBabaiSilhouette()
        {
            using (var game = new Game())
            using (var layer = new Bitmap(1280, 840))
            using (var graphics = Graphics.FromImage(layer))
            {
                game.CreateControl(); game.ClientSize = Sizes[2]; Call(game, "StartTestBabai");
                using (Render(game)) { }
                Call(game, "DrawBabaiBody", graphics);
                var area = (RectangleF)typeof(Game).GetProperty("BabaiCrouchArea", PrivateInstance).GetValue(game, null);
                int naturalEdge = 0;
                for (int y = (int)(area.Y + area.Height * .81f); y < (int)area.Bottom; y++)
                    for (int x = (int)area.Left; x < (int)area.Right; x++)
                        if (layer.GetPixel(x, y).A > 180) naturalEdge++;
                int lowest = -1;
                for (int y = (int)area.Top; y < Math.Min(layer.Height, (int)area.Bottom); y++)
                    for (int x = (int)area.Left; x < (int)area.Right; x++)
                        if (layer.GetPixel(x, y).A > 180) lowest = y;
                var bed = (RectangleF)typeof(Game).GetProperty("BabaiBedArea", PrivateInstance).GetValue(game, null);
                TestSuite.Check(Math.Abs(lowest - (bed.Y + bed.Height * .93f)) <= 12, "actual lower alpha edge rests near the rear bed foot plane, not in mid-air");
                TestSuite.Check(naturalEdge > 100, "hands and lower silhouette survive below former straight .80 crop");
                TestSuite.Check(!typeof(Game).Assembly.GetManifestResourceNames().Any(n => n.Contains("babai-arm")), "giant arm is absent from shipped resources");
                var cache = Read<object>(game, "actorArtCache");
                using (var source = new Bitmap(8, 8))
                using (var output = new Bitmap(8, 8))
                using (var paint = Graphics.FromImage(source))
                using (var target = Graphics.FromImage(output))
                {
                    paint.Clear(Color.FromArgb(180, 200, 160, 120));
                    var rect = new RectangleF(0, 0, 8, 8);
                    var draw = cache.GetType().GetMethod("DrawShaded");
                    draw.Invoke(cache, new object[] { target, source, rect, rect, 1f });
                    var shaded = output.GetPixel(4, 4);
                    TestSuite.Check(Math.Abs(shaded.A - 180) <= 1 && shaded.R < 180 && shaded.G < 140, "under-bed shade preserves alpha and darkens the actual sprite");
                    long misses = (long)cache.GetType().GetProperty("Misses").GetValue(cache, null);
                    target.Clear(Color.Transparent); draw.Invoke(cache, new object[] { target, source, rect, rect, 1f });
                    TestSuite.Check((long)cache.GetType().GetProperty("Misses").GetValue(cache, null) == misses, "shade is prepared once and reused");
                    target.Clear(Color.Transparent); cache.GetType().GetMethod("Draw").Invoke(cache, new object[] { target, source, rect, rect, 1f, false });
                    TestSuite.Check(output.GetPixel(4, 4).R > shaded.R && source.GetPixel(4, 4).R >= 199, "shade key does not contaminate ordinary sprites or original pixels");
                }
            }
        }
        public static void TestLowBabaiLasso()
        {
            foreach (var size in Sizes)
                using (var game = new Game())
                {
                    game.CreateControl(); game.ClientSize = size; Call(game, "StartTestBabai");
                    using (Render(game)) { }
                    var idle = (RectangleF)typeof(Game).GetProperty("BabaiCrouchArea", PrivateInstance).GetValue(game, null);
                    var battle = Read<CombatState>(game, "combat");
                    int artCount = Read<ImageAssets>(game, "assetStore").Count;
                    var backdrop = Read<Bitmap>(game, "districtBackdrop");
                    Call(game, "EndCombatTurn");
                    var timeline = Read<AttackTimeline>(game, "attackTimeline");
                    foreach (float time in new[] { .1f, .35f, .65f, .89f, 1.05f, 1.35f, 1.65f })
                    {
                        Call(game, "AdvanceCombatAnimation", (time - timeline.Time) / AttackTimeline.Speed);
                        var area = (RectangleF)typeof(Game).GetProperty("BabaiCrouchArea", PrivateInstance).GetValue(game, null);
                        TestSuite.Check(area.Size == idle.Size && area.Bottom == idle.Bottom, "Babai never grows or rises during a grab");
                        TestSuite.Check(Math.Abs(area.X - idle.X) <= 12.01f, "only a slight peek from the bed opening");
                        if (time < BabaiMotion.Hit) TestSuite.Check(battle.Babai.Distance == 5, "lasso does not pull before contact");
                        else TestSuite.Check(battle.Babai.Distance == 4, "lasso contact pulls exactly once");
                        using (var frame = Render(game))
                            if (size == Sizes[2] || time == .65f) frame.Save(Path.Combine(TestSuite.OutputDirectory, "grab-" + size.Width + "-" + time.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ".png"));
                        if (size == Sizes[2] && time == .89f)
                            using (var frame = Render(game))
                            using (var detail = frame.Clone(new Rectangle(650, 380, 600, 230), System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                                detail.Save(Path.Combine(TestSuite.OutputDirectory, "babai-closeup.png"));
                        TestSuite.Check(ReferenceEquals(backdrop, Read<Bitmap>(game, "districtBackdrop")) && Read<ImageAssets>(game, "assetStore").Count == artCount, "lasso reuses background and source art");
                    }
                }
        }
    }
}
