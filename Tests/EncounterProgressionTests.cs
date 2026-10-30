using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static class EncounterProgressionTests
    {
        static string Structure(ActMap map)
        {
            return string.Join(";", map.Nodes.Select(n => n.Id + ":" + n.Kind + ":" + string.Join(",", n.Exits)).ToArray());
        }
        static string Contents(MapRun run)
        {
            return string.Join(";", run.Map.Nodes.Where(n => n.Kind == RoomKind.Fight).Select(n => n.Id + ":" + run.OrdinaryEnemyAt(n.Id)).ToArray());
        }
        public static void Routes()
        {
            bool changed = false, repeatedHard = false, hardOutsideEstate = false, longRoute = false;
            var firstEnemies = new HashSet<EnemyKind>();
            var timings = new List<double>();
            for (int index = 0; index < 2000; index++)
            {
                int seed = index == 1998 ? int.MinValue : index == 1999 ? int.MaxValue : index;
                var map = ActMap.Generate(seed);
                string structure = Structure(map);
                for (int branch = 0; branch < 3; branch++)
                {
                    var run = new MapRun(map);
                    var twin = new MapRun(ActMap.Generate(seed));
                    var random = new Random(seed ^ branch);
                    var history = new List<EnemyKind>();
                    var visitedEnemies = new Dictionary<int, EnemyKind>();
                    var mostFights = new int[map.Nodes.Count];
                    foreach (var node in map.Nodes.Reverse())
                        if (node.Exits.Count > 0) mostFights[node.Id] = node.Exits.Max(id => mostFights[id] + (map.Nodes[id].Kind == RoomKind.Fight ? 1 : 0));
                    while (!run.Finished)
                    {
                        string before = Contents(run);
                        int next = branch == 0 ? run.Current.Exits.OrderByDescending(id => mostFights[id] + (map.Nodes[id].Kind == RoomKind.Fight ? 1 : 0)).First() : run.Current.Exits[random.Next(run.Current.Exits.Count)];
                        var node = map.Nodes[next];
                        EnemyKind actual = default(EnemyKind);
                        if (node.Kind == RoomKind.Fight)
                        {
                            actual = run.OrdinaryEnemyAt(next);
                            TestSuite.Check(actual == twin.OrdinaryEnemyAt(next), "same seed and completed route give identical contents");
                            TestSuite.Check(OrdinaryEncounters.Preview(run, next) == EnemyCatalog.Name(actual), "available room preview names its exact cached opponent");
                            if (history.Count < 3)
                            {
                                TestSuite.Check(!SkaterRules.IsHard(actual) && !history.Contains(actual), "first three fights are easy and never repeat, even across intervening elites/events");
                                if (history.Count == 0) firstEnemies.Add(actual);
                            }
                            else
                            {
                                TestSuite.Check(SkaterRules.IsHard(actual) && (history.Count == 3 || history[history.Count - 1] != actual), "fourth and ALL later fights are hard; easy pool never returns");
                                repeatedHard |= history.Count > 3;
                                hardOutsideEstate |= node.District != 2;
                            }
                        }
                        TestSuite.Check(run.Enter(next) && twin.Enter(next), "legal branch remains enterable");
                        TestSuite.Check(Contents(run) == before && OrdinaryEncounters.Completed(run) == history.Count, "entry neither rerolls content nor advances history");
                        if (node.Kind == RoomKind.Fight) { history.Add(actual); visitedEnemies.Add(next, actual); }
                        long start = Stopwatch.GetTimestamp();
                        TestSuite.Check(run.CompleteRoom(), "room completes normally");
                        timings.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                        TestSuite.Check(twin.CompleteRoom(), "twin follows same completion");
                        TestSuite.Check(OrdinaryEncounters.Completed(run) == history.Count, "only completed ordinary rooms advance difficulty");
                        foreach (var past in visitedEnemies)
                            TestSuite.Check(run.OrdinaryEnemyAt(past.Key) == past.Value, "visited enemies remain unchanged");
                        foreach (var future in map.Nodes.Where(n => n.Kind == RoomKind.Fight && !run.Visited(n.Id) && run.Reachable(n.Id)))
                            TestSuite.Check(history.Count < 3 ? !history.Contains(run.OrdinaryEnemyAt(future.Id)) : SkaterRules.IsHard(run.OrdinaryEnemyAt(future.Id)), "every future reachable fight excludes used easy opponents or uses hard pool");
                        changed |= before != Contents(run);
                        string after = Contents(run);
                        TestSuite.Check(!run.CompleteRoom() && Contents(run) == after, "duplicate completion cannot reroll contents");
                        TestSuite.Check(run.ReopenCurrent() && !run.CompleteRoom() && Contents(run) == after, "revisit preserves contents and history");
                        TestSuite.Check(OrdinaryEncounters.Completed(run) == history.Count, "revisit cannot consume a second opponent");
                    }
                    longRoute |= history.Count > 4;
                    if (history.Count >= 3)
                        TestSuite.Check(new HashSet<EnemyKind>(history.Take(3)).SetEquals(new[] { EnemyKind.Rat, EnemyKind.StrayDogs, EnemyKind.Hedgehogs }), "first three completed fights exhaust exactly the easy pool");
                    TestSuite.Check(Structure(map) == structure, "room kinds and all map connections remain unchanged for the entire run");
                    var fresh = new MapRun(map);
                    TestSuite.Check(fresh.CompletedOrdinaryFights == 0 && fresh.OrdinaryEnemyAt(1) != EnemyKind.Babai, "a new run resets history without changing the map");
                }
            }
            TestSuite.Check(changed && repeatedHard && hardOutsideEstate && longRoute && firstEnemies.Count == 3, "coverage includes regenerated enemies, alternating hard encounters, early-district hard fights and routes beyond four fights");
            timings.Sort();
            TestSuite.Note(string.Format(System.Globalization.CultureInfo.InvariantCulture, "Room completion and cached encounter refresh: {0} transitions; median {1:F4} ms; p95 {2:F4} ms; max {3:F4} ms (includes scheduler/GC). No FPS guarantee.", timings.Count, timings[timings.Count / 2], timings[(int)(timings.Count * .95)], timings.Last()));
        }
        public static void CachedPreviews()
        {
            for (int seed = 0; seed < 120; seed++)
            {
                var run = new MapRun(ActMap.Generate(seed));
                var random = new Random(seed);
                while (!run.Finished)
                {
                    var reachable = new HashSet<int> { run.Current.Id };
                    foreach (var node in run.Map.Nodes)
                        if (reachable.Contains(node.Id)) foreach (int next in node.Exits) reachable.Add(next);
                    string before = Contents(run);
                    foreach (var node in run.Map.Nodes.Where(n => n.Kind == RoomKind.Fight))
                    {
                        string preview = OrdinaryEncounters.Preview(run, node.Id);
                        TestSuite.Check(run.Reachable(node.Id) == reachable.Contains(node.Id), "cached reachability matches current route including merged branches");
                        TestSuite.Check(preview == (run.Visited(node.Id) || reachable.Contains(node.Id) ? EnemyCatalog.Name(run.OrdinaryEnemyAt(node.Id)) : "Путь недоступен"), "preview has one concrete current enemy; unavailable routes are labelled");
                        for (int repeat = 0; repeat < 3; repeat++)
                            TestSuite.Check(OrdinaryEncounters.Preview(run, node.Id) == preview, "repeated hover/read cannot reroll any opponent");
                    }
                    TestSuite.Check(Contents(run) == before, "preview is read-only");
                    run.Enter(run.Current.Exits[random.Next(run.Current.Exits.Count)]); run.CompleteRoom();
                }
            }
            TestSuite.Throws<ArgumentNullException>(() => OrdinaryEncounters.Select(1, 1, null), "missing history rejected");
            var sample = new MapRun(ActMap.Generate(1));
            TestSuite.Throws<ArgumentOutOfRangeException>(() => sample.OrdinaryEnemyAt(0), "non-fight cannot supply an opponent");
            TestSuite.Check(!sample.Reachable(-1) && !sample.Reachable(int.MaxValue), "bad reachability IDs are harmless");
        }
    }

    public static partial class UiTests
    {
        public static void TestEncounterProgression()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                Write(game, "selected", 3); Call(game, "OpenCheckout"); Call(game, "TakeBuff"); Call(game, "OpenActMap");
                // A fixed route fixture makes the UI regression reproducible.
                var run = new MapRun(ActMap.Generate(42));
                Write(game, "mapRun", run);
                // This fixture isolates ordinary-node progression; doctor choices have their own suite.
                var questionRolls = (Dictionary<int, int>)typeof(MapRun).GetField("questionRolls", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(run);
                foreach (int question in questionRolls.Keys.ToArray()) questionRolls[question] = 3;
                var intel = Read<Dictionary<int, IntelNode>>(game, "mapIntel");
                intel.Clear();
                foreach (var node in run.Map.Nodes)
                    intel.Add(node.Id, new IntelNode { Kind = node.Kind == RoomKind.Fight ? IntelKind.Fight : IntelKind.Other, Revealed = node.Id % 2 == 0, Identity = node.Kind == RoomKind.Fight ? OrdinaryEncounters.Preview(run, node.Id) : "fixture" });
                var revealed = intel.ToDictionary(pair => pair.Key, pair => pair.Value.Revealed);
                var futureFights = new int[run.Map.Nodes.Count];
                foreach (var node in run.Map.Nodes.Reverse())
                    if (node.Exits.Count > 0) futureFights[node.Id] = node.Exits.Max(id => futureFights[id] + (run.Map.Nodes[id].Kind == RoomKind.Fight ? 1 : 0));
                var seen = new HashSet<EnemyKind>();
                int completed = 0;
                while (!run.Finished)
                {
                    int id = run.Current.Exits.OrderByDescending(next => futureFights[next] + (run.Map.Nodes[next].Kind == RoomKind.Fight ? 1 : 0)).First();
                    var node = run.Map.Nodes[id];
                    Call(game, "SnapMap", ExpectedFocus(game, node));
                    EnemyKind expected = node.Kind == RoomKind.Fight ? run.OrdinaryEnemyAt(id) : default(EnemyKind);
                    if (node.Kind == RoomKind.Fight)
                    {
                        TestSuite.Check(intel[id].Identity == EnemyCatalog.Name(expected), "updated investigation preview agrees with the room about to open");
                        Write(game, "hoveredMapNode", id);
                        using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "progression-map-before-" + (completed + 1) + ".png"));
                    }
                    Call(game, "EnterMapNode", id);
                    if (node.Kind == RoomKind.Fight || node.Kind == RoomKind.Elite)
                    {
                        var fight = Read<CombatState>(game, "combat");
                        if (node.Kind == RoomKind.Fight)
                        {
                            TestSuite.Check(fight.Enemy == expected, "actual combat consumes the cached enemy, without an entry reroll");
                            TestSuite.Check(completed < 3 ? seen.Add(fight.Enemy) && !SkaterRules.IsHard(fight.Enemy) : SkaterRules.IsHard(fight.Enemy), "real UI route obeys easy-to-hard progression");
                            using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "progression-battle-" + (completed + 1) + ".png"));
                            Key(game, Keys.Escape); Call(game, "AdvanceCombatAnimation", .2f); Key(game, Keys.Escape);
                            using (Render(game)) { }
                            TestSuite.Check(fight.Enemy == expected && run.OrdinaryEnemyAt(id) == expected && run.CompletedOrdinaryFights == completed, "pause, rendering and resume do not reroll or consume a fight");
                        }
                        // Fixture damage keeps this a routing test; play/hits/victory use the game pipeline.
                        foreach (var card in fight.Hero.Deck)
                        { card.Attack = true; card.Skill = false; card.Damage = 1000; card.ManaCost = 0; card.GoldCost = 0; }
                        FinishTestFight(game);
                    }
                    Call(game, "CompleteMapRoom");
                    if (node.Kind == RoomKind.Fight) completed++;
                    TestSuite.Check(run.Current.Id == id && run.CompletedOrdinaryFights == completed, "UI completes exactly one room before refreshing contents");
                    foreach (var future in run.Map.Nodes.Where(n => n.Kind == RoomKind.Fight && !run.Visited(n.Id)))
                    {
                        TestSuite.Check(intel[future.Id].Identity == OrdinaryEncounters.Preview(run, future.Id), "UI updates future cached investigation identities after every room");
                        TestSuite.Check(intel[future.Id].Revealed == revealed[future.Id], "regeneration never reveals hidden rooms");
                    }
                    Call(game, "OpenCurrentMapRoom"); Call(game, "CompleteMapRoom");
                    TestSuite.Check(run.CompletedOrdinaryFights == completed && run.Pending == null, "UI revisit consumes no additional pool entry");
                }
                TestSuite.Check(completed >= 5 && seen.Count == 3, "real route covers all easy fights and at least two hard fights");
                Call(game, "OpenCheckout"); Call(game, "TakeBuff"); Call(game, "OpenActMap");
                TestSuite.Check(Read<MapRun>(game, "mapRun").CompletedOrdinaryFights == 0, "starting another hero clears encounter history");
            }
        }
    }
}
