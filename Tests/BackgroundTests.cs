using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        public static void TestDistrictBackgrounds()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = Sizes[2];
                Call(game, "StartTestBattle");
                var run = Read<MapRun>(game, "mapRun");
                run.CompleteRoom(); Call(game, "ResetCombat");
                Image[] sources = new Image[3];
                foreach (int floor in new[] { 1, 5, 6, 10, 11, 15 })
                {
                    var node = run.Map.Nodes.First(n => n.Floor == floor);
                    typeof(MapNode).GetProperty("Kind").SetValue(node, RoomKind.Fight, null);
                    var before = run.Map.Nodes.First(n => n.Exits.Contains(node.Id));
                    typeof(MapRun).GetProperty("Current").SetValue(run, before, null);
                    Write(game, "testEnemyOverride", (EnemyKind?)EnemyKind.Rat);
                    Call(game, "EnterMapNode", node.Id);
                    using (var frame = Render(game))
                        frame.Save(Path.Combine(TestSuite.OutputDirectory, "district-rat-floor-" + floor + ".png"));
                    int district = (floor - 1) / 5;
                    Image source = Read<Image>(game, "sceneSource");
                    if (sources[district] == null) sources[district] = source;
                    TestSuite.Check(ReferenceEquals(sources[district], source), "same district shares source at both floor boundaries");
                    TestSuite.Check(!ReferenceEquals(source, Read<Image>(game, "mapBackground")), "battle uses eye-level regional scene");
                    foreach (var size in Sizes)
                    {
                        game.ClientSize = size;
                        using (Render(game)) { }
                        var cached = Read<Bitmap>(game, "sceneBackground");
                        using (Render(game)) { }
                        TestSuite.Check(ReferenceEquals(cached, Read<Bitmap>(game, "sceneBackground")) && ReferenceEquals(source, Read<Image>(game, "sceneSource")), "regional source and raster cache survive repeat paints at every size");
                    }
                    game.ClientSize = Sizes[2];
                    Write(game, "mapScroll", 900f);
                    using (Render(game)) { }
                    TestSuite.Check(ReferenceEquals(source, Read<Image>(game, "sceneSource")), "scroll cannot change battle location");
                    // A different ordinary encounter, including Babai, shares this room's district.
                    foreach (var enemy in new[] { EnemyKind.StrayDogs, EnemyKind.Hedgehogs, EnemyKind.Babai })
                    {
                        Write(game, "combat", new CombatState(Read<HeroState>(game, "currentHero"), new Random(1), enemy));
                        using (var frame = Render(game))
                            if (enemy == EnemyKind.Babai && floor == 11) frame.Save(Path.Combine(TestSuite.OutputDirectory, "district-babai-estate.png"));
                        var actual = Read<Image>(game, enemy == EnemyKind.Babai ? "babaiBackdropSource" : "sceneSource");
                        TestSuite.Check(ReferenceEquals(actual, source), "all ordinary enemies share region, including Babai");
                    }
                    run.CompleteRoom(); Call(game, "ResetCombat");
                }
                TestSuite.Check(!ReferenceEquals(sources[0], sources[1]) && !ReferenceEquals(sources[1], sources[2]) && !ReferenceEquals(sources[0], sources[2]), "three distinct regional scenes");
                TestSuite.Check(Read<ImageAssets>(game, "assetStore").Warnings.Length == 0, "regional assets decode without fallback warnings");
            }
        }

        public static void TestBattleBackground()
        {
            foreach (var size in Sizes)
                using (var game = new Game())
                {
                    game.CreateControl(); game.ClientSize = size;
                    Call(game, "StartTestBattle");
                    using (var frame = Render(game))
                        frame.Save(Path.Combine(TestSuite.OutputDirectory, "social-battle-" + size.Width + "x" + size.Height + ".png"));
                    var ordinary = Read<Image>(game, "ordinaryCombatBackground");
                    TestSuite.Check(object.ReferenceEquals(Read<Image>(game, "sceneSource"), ordinary), "ordinary battle selects social art");
                    var cache = Read<Bitmap>(game, "sceneBackground");
                    using (Render(game)) { }
                    TestSuite.Check(object.ReferenceEquals(cache, Read<Bitmap>(game, "sceneBackground")), "idle paint reuses background cache");
                    Call(game, "StartTestMimicrator");
                    using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-courtyard-" + size.Width + "x" + size.Height + ".png"));
                    TestSuite.Check(object.ReferenceEquals(Read<Image>(game, "sceneSource"), Read<Image>(game, "eliteCombatBackground")), "elite uses approved courtyard");
                    TestSuite.Check(!object.ReferenceEquals(cache, Read<Bitmap>(game, "sceneBackground")), "same viewport with changed source rebuilds cache");
                    Call(game, "ReturnToMainMenu");
                    Call(game, "OpenCheckout"); Call(game, "TakeBuff"); Call(game, "OpenActMap");
                    using (Render(game)) { }
                    TestSuite.Check(object.ReferenceEquals(Read<Image>(game, "sceneSource"), Read<Image>(game, "mapBackground")), "world map keeps original art");
                    TestSuite.Check(!Read<bool>(game, "sceneIsCombat"), "world map retains scrolling background mode");
                }
        }
    }
}
