using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static class MapTests
    {
        static string Signature(ActMap map)
        {
            return string.Join(";", map.Nodes.Select(n => n.Id + ":" + n.Floor + ":" + n.Lane + ":" + n.Offset + ":" + n.Kind + ":" + string.Join(",", n.Exits)).ToArray());
        }

        public static void Generation()
        {
            var signatures = new HashSet<string>();
            for (int index = 0; index < 2000; index++)
            {
                int seed = index == 1998 ? int.MinValue : index == 1999 ? int.MaxValue : index;
                var map = ActMap.Generate(seed);
                string signature = Signature(map);
                TestSuite.Check(signature == Signature(ActMap.Generate(seed)), "same seed yields identical map");
                signatures.Add(signature);
                TestSuite.Check(map.Nodes.Count(n => n.Kind == RoomKind.Start) == 1, "single start");
                TestSuite.Check(map.Nodes.Count(n => n.Kind == RoomKind.Boss) == 1 && map.Nodes.Last().Floor == ActMap.LevelCount + 1, "boss after fifteen floors");
                TestSuite.Check(map.Nodes.Count(n => n.Floor == 1) == 1 && map.Nodes[1].Kind == RoomKind.Fight, "mandatory first fight");
                TestSuite.Check(map.Nodes.Count(n => n.Kind == RoomKind.Question) >= 10, "at least ten questions");
                TestSuite.Check(map.Nodes.Count(n => n.Kind == RoomKind.Fight) >= 2, "two investigation fight targets exist");
                for (int district = 0; district < 3; district++)
                {
                    var nodes = map.Nodes.Where(n => n.Floor >= 1 && n.Floor <= 15 && n.District == district).ToList();
                    TestSuite.Check(nodes.Select(n => n.Floor).Distinct().Count() == 5, "five levels per district");
                    foreach (var kind in new[]
                    {
                        RoomKind.Elite,
                        RoomKind.Rest,
                        RoomKind.Shop
                    }

                    )
                        TestSuite.Check(nodes.Any(n => n.Kind == kind), "required " + kind + " in district " + district);
                }

                var reachable = new HashSet<int>
                {
                    0
                };
                var safe = new HashSet<int>
                {
                    0
                };
                foreach (var node in map.Nodes)
                {
                    TestSuite.Check(reachable.Contains(node.Id), "no unreachable nodes");
                    TestSuite.Check(node.Exits.Count == node.Exits.Distinct().Count(), "unique edges");
                    TestSuite.Check(node.Exits.Count <= 3, "at most three choices per node");
                    TestSuite.Check(node.Exits.Count > 0 || node.Kind == RoomKind.Boss, "no dead ends");
                    foreach (int id in node.Exits)
                    {
                        var next = map.Nodes[id];
                        TestSuite.Check(next.Floor == node.Floor + 1, "no skips or backward movement");
                        TestSuite.Check(Math.Abs(next.Lane - node.Lane) <= 1, "paths only connect neighbouring columns");
                        TestSuite.Check(next.District == node.District || next.District == node.District + 1, "district order preserved");
                        reachable.Add(id);
                        if (safe.Contains(node.Id) && next.Kind != RoomKind.Elite)
                            safe.Add(id);
                    }

                    foreach (var peer in map.Nodes.Where(n => n.Floor == node.Floor && n.Lane > node.Lane))
                        foreach (int left in node.Exits)
                            foreach (int right in peer.Exits)
                                TestSuite.Check(map.Nodes[left].Lane <= map.Nodes[right].Lane, "edges never cross");
                }

                TestSuite.Check(safe.Contains(map.Nodes.Last().Id), "an elite-free route exists");
            }

            TestSuite.Check(signatures.Count > 1990, "seed changes routes and rooms");
            TestSuite.Throws<ArgumentOutOfRangeException>(() => ActMap.DistrictName(3), "bad district rejected");
            var sample = ActMap.Generate(42);
            TestSuite.Throws<NotSupportedException>(() => ((IList<MapNode>)sample.Nodes).Clear(), "node list immutable");
            TestSuite.Throws<NotSupportedException>(() => ((IList<int>)sample.Nodes[0].Exits).Clear(), "edge list immutable");
        }

        public static void Traversal()
        {
            TestSuite.Throws<ArgumentNullException>(() => new MapRun(null), "null map rejected");
            var run = new MapRun(ActMap.Generate(42));
            TestSuite.Check(!run.Enter(-1) && !run.Enter(int.MaxValue) && !run.Enter(0) && !run.Enter(2), "invalid moves ignored");
            TestSuite.Check(!run.CompleteRoom(), "cannot complete without entering");
            for (int floor = 1; floor <= 16; floor++)
            {
                var previous = run.Current;
                int next = previous.Exits[previous.Exits.Count - 1];
                TestSuite.Check(run.Enter(next) && run.Pending.Floor == floor, "legal move opens room");
                TestSuite.Check(run.Current == previous && !run.Visited(next), "room not completed early");
                TestSuite.Check(!run.Enter(next), "double entry blocked");
                TestSuite.Check(run.CompleteRoom() && run.Current.Id == next && run.Visited(next), "completion advances exactly once");
                TestSuite.Check(!run.CompleteRoom() && !run.Enter(previous.Id), "duplicate completion and backtracking blocked");
            }

            TestSuite.Check(run.Finished && run.Pending == null && !run.Enter(0), "boss finishes map");
        }
    }

    public static partial class UiTests
    {
        public static void TestMapInteraction()
        {
            foreach (var size in Sizes)
                using (var game = new Game())
                {
                    game.CreateControl();
                    game.ClientSize = size;
                    Write(game, "selected", 2);
                    Call(game, "OpenCheckout");
                    Call(game, "OpenActMap");
                    TestSuite.Check(!Read<bool>(game, "mapScreen"), "relic required before map");
                    Write(game, "buffCursor", 2);
                    Call(game, "TakeBuff");
                    var hero = Read<HeroState>(game, "currentHero");
                    using (Render(game))
                    {
                    }

                    Click(game, 1010, 741);
                    var run = Read<MapRun>(game, "mapRun");
                    TestSuite.Check(Read<bool>(game, "mapScreen") && run != null && !Read<bool>(game, "checkout"), "checkout opens map");
                    var intel = Read<Dictionary<int, IntelNode>>(game, "mapIntel");
                    foreach (var kind in new[]
                    {
                        IntelKind.Question,
                        IntelKind.Elite,
                        IntelKind.Fight
                    }

                    )
                        TestSuite.Check(intel.Values.Count(n => n.Kind == kind && n.Revealed) == 2, "investigation two each");
                    string before;
                    using (var frame = Render(game))
                    {
                        before = Digest(frame);
                        frame.Save(Path.Combine(TestSuite.OutputDirectory, "map-" + size.Width + "x" + size.Height + ".png"));
                    }

                    using (var frame = Render(game))
                        TestSuite.Check(before == Digest(frame), "map repaint deterministic");
                    var node = run.Map.Nodes[1];
                    Call(game, "OnResize", EventArgs.Empty);
                    var firstPoint = MapPoint(game, node);
                    Click(game, firstPoint.X, firstPoint.Y);
                    TestSuite.Check(run.Pending == null, "resize clears stale targets");
                    using (Render(game))
                    {
                    }

                    Click(game, firstPoint.X, firstPoint.Y);
                    TestSuite.Check(run.Pending == node, "first node hit at all sizes");
                    using (var frame = Render(game))
                        if (size == Sizes[2])
                            frame.Save(Path.Combine(TestSuite.OutputDirectory, "map-room.png"));
                    Click(game, 486, 676);
                    TestSuite.Check(run.Pending == node && run.Current.Floor == 0, "modal blocks underlying nodes");
                    FinishTestFight(game);
                    Key(game, Keys.Enter);
                    TestSuite.Check(run.Current.Floor == 1 && run.Pending == null, "keyboard completes room");
                    Key(game, Keys.Escape);
                    TestSuite.Check(Read<string>(game, "inventoryView") == "pause" && !Read<bool>(game, "checkout"), "escape opens pause instead of checkout");
                    Key(game, Keys.Escape);
                    Call(game, "OpenActMap");
                    TestSuite.Check(object.ReferenceEquals(run, Read<MapRun>(game, "mapRun")), "reopening preserves seed and progress");
                    TestSuite.Check(intel.Values.Count(n => n.Revealed) == 6, "reopening cannot reroll investigation");
                    while (!run.Finished)
                    {
                        using (Render(game))
                        {
                        }

                        Key(game, Keys.D1);
                        TestSuite.Check(run.Pending != null, "keyboard selects legal branch");
                        using (Render(game))
                        {
                        }

                        // Routing fixture: real combat is verified separately; later fights close at the route model.
                        if (run.Pending.Kind == RoomKind.Fight || run.Pending.Kind == RoomKind.Elite)
                        {
                            run.CompleteRoom();
                            Call(game, "ResetCombat");
                        }
                        else
                            Click(game, 650, 564);
                        TestSuite.Check(run.Pending == null, "mouse completes room");
                    }

                    TestSuite.Check(hero.Health > 0 && hero.Health <= 50 && hero.Money == 0, "route rooms grant no invented rewards");
                    using (var frame = Render(game))
                        if (size == Sizes[2])
                            frame.Save(Path.Combine(TestSuite.OutputDirectory, "map-finished.png"));
                    Key(game, Keys.Enter);
                    TestSuite.Check(run.Finished && run.Pending == null, "completed map cannot restart on enter");
                    TestSuite.Check((Read<ImageAssets>(game, "assetStore").Count == 33 || Read<ImageAssets>(game, "assetStore").Count == 36), "elite art is lazy and shared");
                    Call(game, "OpenCheckout");
                    TestSuite.Check(Read<MapRun>(game, "mapRun") == null && !Read<bool>(game, "mapScreen"), "new character run resets route");
                }
        }
    }
}
