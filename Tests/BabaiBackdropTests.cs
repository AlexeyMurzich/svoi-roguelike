using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        public static void TestEstateSceneRouting()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = Sizes[2]; Call(game, "StartTestBattle");
                var run = Read<MapRun>(game, "mapRun");
                foreach (int district in new[] { 0, 1, 2 })
                {
                    // Scene fixture: isolate rendering from map traversal and encounter rolls.
                    var node = run.Map.Nodes.First(n => n.Kind == RoomKind.Fight && n.District == district);
                    typeof(MapRun).GetProperty("Pending").SetValue(run, node, null);
                    Write(game, "combatNode", node.Id);
                    using (var frame = Render(game))
                        if (district == 2) frame.Save(Path.Combine(TestSuite.OutputDirectory, "estate-rat.png"));
                    bool estate = (bool)typeof(Game).GetProperty("UsesDistrictBackdrop", PrivateInstance).GetValue(game, null);
                    TestSuite.Check(estate && Read<int>(game, "districtBackdropIndex") == district, "ordinary encounters use their own registered district scene");
                }
                TestSuite.Check(Read<Image>(game, "babaiBodies") == null && !Read<bool>(game, "districtBackdropHasBed"), "Estate rats never load Babai art or bake a bed shadow");
                var backdrop = Read<Bitmap>(game, "districtBackdrop");
                using (Render(game)) { }
                TestSuite.Check(ReferenceEquals(backdrop, Read<Bitmap>(game, "districtBackdrop")), "Estate ordinary scene reuses cached planes");
                foreach (var enemy in new[] { EnemyKind.Toha, EnemyKind.Mimicrator })
                {
                    Write(game, "combat", new CombatState(Read<HeroState>(game, "currentHero"), new Random(1), enemy));
                    TestSuite.Check(!(bool)typeof(Game).GetProperty("UsesDistrictBackdrop", PrivateInstance).GetValue(game, null), "elite keeps its approved scene in Estate");
                }
                Write(game, "combat", new CombatState(Read<HeroState>(game, "currentHero"), new Random(1), EnemyKind.Babai));
                using (Render(game)) { }
                TestSuite.Check(Read<bool>(game, "districtBackdropHasBed") && !ReferenceEquals(backdrop, Read<Bitmap>(game, "districtBackdrop")), "scene cache includes bed contacts only for Babai");
            }
        }
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
                    var backdrop = Read<Bitmap>(game, "districtBackdrop");
                    if (prior != null) TestSuite.Throws<ArgumentException>(() => prior.GetPixel(0, 0), "resize releases former composed backdrop");
                    float floor = (float)typeof(Game).GetProperty("CombatFloor", PrivateInstance).GetValue(game, null);
                    var bed = (RectangleF)typeof(Game).GetProperty("BabaiBedArea", PrivateInstance).GetValue(game, null);
                    TestSuite.Check(Math.Abs(bed.Bottom - floor) < .0001f && DistrictStageLayout.WallBase(floor, 2) < bed.Bottom - 40, "bed feet and hero share a standing plane below wall seam");
                    TestSuite.Check(backdrop.GetPixel(0, 0).A == 255 && backdrop.GetPixel(backdrop.Width - 1, backdrop.Height - 1).A == 255, "composed wall and ground are opaque");
                    TestSuite.Check((long)backdrop.Width * backdrop.Height <= 1920L * 1080, "background obeys rendering pixel budget");
                    TestSuite.Check(Read<Bitmap>(game, "sceneBackground") == null, "inactive map background is not retained alongside battle cache");
                    wall = Read<Image[]>(game, "districtArt")[2];
                    int images = Read<ImageAssets>(game, "assetStore").Count;
                    Call(game, "AdvanceLivingAnimation", .1f);
                    using (Render(game)) { }
                    TestSuite.Check(ReferenceEquals(backdrop, Read<Bitmap>(game, "districtBackdrop")) && Read<ImageAssets>(game, "assetStore").Count == images, "ordinary frames reuse composed background and original images");
                    Call(game, "SetInventory", "pause"); using (Render(game)) { } Call(game, "SetInventory", "");
                    TestSuite.Check(ReferenceEquals(backdrop, Read<Bitmap>(game, "districtBackdrop")), "pause does not rebuild static planes");
                    prior = backdrop;
                }
                Call(game, "ReturnToMainMenu");
                TestSuite.Check(Read<Bitmap>(game, "districtBackdrop") == null, "leaving battle clears scene cache");
                TestSuite.Throws<ArgumentException>(() => prior.GetPixel(0, 0), "leaving battle disposes final cached bitmap");
                TestSuite.Check(wall.Width > 0, "original texture still belongs to ImageAssets");
            }
        }
    }
}
