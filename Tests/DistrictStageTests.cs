using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        static void SetSceneDistrict(Game game, int district)
        {
            var run = Read<MapRun>(game, "mapRun");
            // The generator may legally use all optional nodes for questions. This
            // renderer fixture supplies its own ordinary battle in that district.
            var node = run.Map.Nodes.FirstOrDefault(n => n.Kind == RoomKind.Fight && n.District == district);
            if (node == null)
            {
                node = run.Map.Nodes.First(n => n.District == district && n.Kind != RoomKind.Start && n.Kind != RoomKind.Boss);
                typeof(MapNode).GetProperty("Kind").SetValue(node, RoomKind.Fight, null);
            }
            typeof(MapRun).GetProperty("Pending").SetValue(run, node, null);
            Write(game, "combatNode", node.Id);
        }
        public static void TestDistrictPlanes()
        {
            for (int district = 0; district < 3; district++)
                foreach (var size in Sizes)
                    foreach (bool wall in new[] { true, false })
                    {
                        var target = new SizeF(size.Width, wall ? size.Height * .52f : size.Height * .48f);
                        var source = DistrictStageLayout.SourcePlane(new Size(1672, 941), district, wall, target);
                        TestSuite.Check(source.Left >= 0 && source.Top >= 0 && source.Right <= 1672.01f && source.Bottom <= 941.01f, "registered source never reads outside original art");
                        TestSuite.Check(Math.Abs(source.Width / source.Height - target.Width / target.Height) < .00001f, "wall and floor keep proportions at every aspect ratio");
                        var other = DistrictStageLayout.SourcePlane(new Size(1672, 941), district, !wall, target);
                        TestSuite.Check(Math.Abs((wall ? source.Bottom - other.Top : other.Bottom - source.Top)) < .001f, "two planes meet at the same original junction");
                    }
        }
        public static void TestDistrictBattles()
        {
            foreach (var size in Sizes)
                using (var game = new Game())
                {
                    game.CreateControl(); game.ClientSize = size; Call(game, "StartTestBattle");
                    if (size == Sizes[0])
                    {
                        var fixture = Read<MapRun>(game, "mapRun");
                        foreach (var optionalFight in fixture.Map.Nodes.Where(n => n.District == 2 && n.Kind == RoomKind.Fight))
                            typeof(MapNode).GetProperty("Kind").SetValue(optionalFight, RoomKind.Question, null);
                        TestSuite.Check(!fixture.Map.Nodes.Any(n => n.District == 2 && n.Kind == RoomKind.Fight), "scene fixture explicitly covers a district without optional fights");
                    }
                    Bitmap prior = null;
                    foreach (int district in new[] { 0, 1, 2, 0 })
                    {
                        SetSceneDistrict(game, district);
                        using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "district-" + district + "-" + size.Width + "x" + size.Height + ".png"));
                        var backdrop = Read<Bitmap>(game, "districtBackdrop");
                        if (prior != null) TestSuite.Throws<ArgumentException>(() => prior.GetPixel(0, 0), "district change disposes previous composed frame");
                        TestSuite.Check(Read<int>(game, "districtBackdropIndex") == district, "district change invalidates backdrop even at same size");
                        float floor = (float)typeof(Game).GetProperty("CombatFloor", PrivateInstance).GetValue(game, null);
                        var hero = (RectangleF)typeof(Game).GetProperty("HeroDeathArea", PrivateInstance).GetValue(game, null);
                        var enemy = (RectangleF)typeof(Game).GetMethod("EnemyArea", PrivateInstance).Invoke(game, new object[] { 0 });
                        TestSuite.Check(hero.Bottom == floor && enemy.Bottom == floor && floor > DistrictStageLayout.WallBase(floor, district), "actors share support plane below building wall");
                        TestSuite.Check((long)backdrop.Width * backdrop.Height <= 1920L * 1080, "district scene obeys pixel budget");
                        int count = Read<ImageAssets>(game, "assetStore").Count;
                        Call(game, "AdvanceLivingAnimation", .15f); using (Render(game)) { }
                        TestSuite.Check(ReferenceEquals(backdrop, Read<Bitmap>(game, "districtBackdrop")) && count == Read<ImageAssets>(game, "assetStore").Count, "living animation does not rebuild scene or decode images");
                        prior = backdrop;
                    }
                }
            for (int hero = 0; hero < 4; hero++)
                using (var game = new Game())
                {
                    game.CreateControl(); game.ClientSize = Sizes[2];
                    typeof(Game).GetMethod("StartTestEncounter", PrivateInstance, null, new[] { typeof(EnemyKind), typeof(int) }, null).Invoke(game, new object[] { EnemyKind.Hedgehogs, hero });
                    for (int district = 0; district < 3; district++)
                    {
                        SetSceneDistrict(game, district);
                        using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "hero-" + hero + "-district-" + district + ".png"));
                    }
                }
        }
    }
}
