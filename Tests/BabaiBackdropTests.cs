using System;
using System.Drawing;
using System.IO;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        public static void TestBabaiGrounding()
        {
            using (var sample = new Bitmap(8, 8))
            {
                sample.SetPixel(3, 5, Color.White); sample.SetPixel(3, 7, Color.FromArgb(10, 255, 255, 255));
                var crop = BabaiStageLayout.FootCrop(sample);
                TestSuite.Check(crop.Bottom == 6, "foot registration ignores transparent canvas and faint fringe");
                var area = BabaiStageLayout.Bed(20, 100, 80, crop.Size);
                TestSuite.Check(area.Bottom == 100 && area.Height == 60 && area.Left == 20, "actual lowest opaque row lands exactly on floor");
            }
            using (var empty = new Bitmap(4, 4))
                TestSuite.Throws<ArgumentException>(() => BabaiStageLayout.FootCrop(empty), "empty sprite rejected");
            using (var game = new Game())
            {
                game.CreateControl(); Call(game, "StartTestBabai");
                Bitmap prior = null; Image wall = null;
                foreach (var size in Sizes)
                {
                    game.ClientSize = size;
                    using (var frame = Render(game))
                        if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "babai-grounded.png"));
                    var backdrop = Read<Bitmap>(game, "babaiBackdrop");
                    if (prior != null) TestSuite.Throws<ArgumentException>(() => prior.GetPixel(0, 0), "resize releases former composed backdrop");
                    float floor = (float)typeof(Game).GetProperty("CombatFloor", PrivateInstance).GetValue(game, null);
                    var bed = (RectangleF)typeof(Game).GetProperty("BabaiBedArea", PrivateInstance).GetValue(game, null);
                    TestSuite.Check(Math.Abs(bed.Bottom - floor) < .0001f && BabaiStageLayout.WallBase(floor) < bed.Bottom - 40, "bed feet and hero share a standing plane below wall seam");
                    TestSuite.Check(backdrop.GetPixel(0, 0).A == 255 && backdrop.GetPixel(backdrop.Width - 1, backdrop.Height - 1).A == 255, "composed wall and ground are opaque");
                    TestSuite.Check((long)backdrop.Width * backdrop.Height <= 1920L * 1080, "background obeys rendering pixel budget");
                    TestSuite.Check(Read<Bitmap>(game, "sceneBackground") == null, "inactive map background is not retained alongside battle cache");
                    wall = Read<Image>(game, "babaiWall");
                    int images = Read<ImageAssets>(game, "assetStore").Count;
                    Call(game, "AdvanceLivingAnimation", .1f);
                    using (Render(game)) { }
                    TestSuite.Check(ReferenceEquals(backdrop, Read<Bitmap>(game, "babaiBackdrop")) && Read<ImageAssets>(game, "assetStore").Count == images, "ordinary frames reuse composed background and original images");
                    Call(game, "SetInventory", "pause"); using (Render(game)) { } Call(game, "SetInventory", "");
                    TestSuite.Check(ReferenceEquals(backdrop, Read<Bitmap>(game, "babaiBackdrop")), "pause does not rebuild static planes");
                    prior = backdrop;
                }
                Call(game, "ReturnToMainMenu");
                TestSuite.Check(Read<Bitmap>(game, "babaiBackdrop") == null, "leaving battle clears scene cache");
                TestSuite.Throws<ArgumentException>(() => prior.GetPixel(0, 0), "leaving battle disposes final cached bitmap");
                TestSuite.Check(wall.Width > 0, "original texture still belongs to ImageAssets");
            }
        }
    }
}
