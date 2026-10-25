using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ShpilSvoih
{
    public enum RoomKind
    {
        Start,
        Fight,
        Question,
        Elite,
        Rest,
        Shop,
        Boss
    }

    public sealed class MapNode
    {
        public int Id { get; private set; }
        public int Floor { get; private set; }
        public int Lane { get; private set; }

        public int District
        {
            get
            {
                return Floor <= 0 ? 0 : Math.Min(2, (Floor - 1) / ActMap.LevelsPerDistrict);
            }
        }

        public RoomKind Kind { get; internal set; }
        public int Offset { get; private set; }

        private readonly List<int> exits = new List<int>();
        public ReadOnlyCollection<int> Exits { get; private set; }

        internal MapNode(int id, int floor, int lane, RoomKind kind, int offset)
        {
            Id = id;
            Floor = floor;
            Lane = lane;
            Kind = kind;
            Offset = offset;
            Exits = exits.AsReadOnly();
        }

        internal void Connect(MapNode next)
        {
            if (!exits.Contains(next.Id))
                exits.Add(next.Id);
        }
    }

    public sealed class ActMap
    {
        public const int LevelsPerDistrict = 5;
        public const int LevelCount = 15;
        public static string DistrictName(int index)
        {
            if (index < 0 || index > 2)
                throw new ArgumentOutOfRangeException("index");
            return new[]
            {
                "Социал",
                "Старый центр",
                "Поместье Фомченко"
            }[index];
        }

        public int Seed { get; private set; }
        public ReadOnlyCollection<MapNode> Nodes { get; private set; }

        private ActMap(int seed, List<MapNode> nodes)
        {
            Seed = seed;
            Nodes = nodes.AsReadOnly();
        }

        public static ActMap Generate(int seed)
        {
            var random = new Random(seed);
            var nodes = new List<MapNode>();
            var rows = new List<List<MapNode>>();
            for (int floor = 0; floor <= LevelCount + 1; floor++)
            {
                var row = new List<MapNode>();
                var lanes = new List<int>();
                if (floor <= 1 || floor == LevelCount + 1)
                    lanes.Add(3);
                else if (floor == 2 || floor == LevelCount)
                    lanes.AddRange(new[] { 2, 3, 4 });
                else
                {
                    lanes.Add(random.Next(1, 3));
                    lanes.Add(3);
                    lanes.Add(random.Next(4, 6));
                    if (random.Next(3) == 0)
                        lanes.Add(lanes[0] == 1 ? 2 : 1);
                    if (random.Next(3) == 0)
                        lanes.Add(lanes[2] == 4 ? 5 : 4);
                    lanes.Sort();
                }

                foreach (int lane in lanes)
                {
                    RoomKind kind = floor == 0 ? RoomKind.Start : floor == LevelCount + 1 ? RoomKind.Boss : RoomKind.Fight;
                    var node = new MapNode(nodes.Count, floor, lane, kind, lanes.Count == 1 ? 0 : random.Next(-22, 23));
                    nodes.Add(node);
                    row.Add(node);
                }

                rows.Add(row);
            }

            // Ordered lanes guarantee a connected graph without crossing edges.
            // The central lane stays free of elites, preserving an optional elite route.
            for (int floor = 0; floor <= LevelCount; floor++)
            {
                var from = rows[floor];
                var to = rows[floor + 1];
                if (from.Count == 1)
                    foreach (var next in to)
                        from[0].Connect(next);
                else if (to.Count == 1)
                    foreach (var node in from)
                        node.Connect(to[0]);
                else
                {
                    // Every node has an incoming and outgoing path. Nearest neighbours
                    // preserve ordering; optional branches may move only one column.
                    foreach (var node in from)
                        node.Connect(Nearest(to, node.Lane, random));
                    foreach (var next in to)
                        Nearest(from, next.Lane, random).Connect(next);
                    foreach (var node in from)
                        foreach (var next in to)
                        {
                            if (Math.Abs(node.Lane - next.Lane) > 1 || random.Next(100) >= 18)
                                continue;
                            bool crosses = from.Any(peer => peer.Exits.Any(id => (peer.Lane < node.Lane && nodes[id].Lane > next.Lane) || (peer.Lane > node.Lane && nodes[id].Lane < next.Lane)));
                            if (!crosses)
                                node.Connect(next);
                        }
                }
            }

            for (int district = 0; district < 3; district++)
            {
                var available = nodes.Where(n => n.Floor >= 2 && n.Floor <= LevelCount && n.District == district).ToList();
                var eliteCandidates = available.Where(n => n.Lane != 3).ToList();
                var elite = eliteCandidates[random.Next(eliteCandidates.Count)];
                elite.Kind = RoomKind.Elite;
                available.Remove(elite);
                foreach (var kind in new[]
                {
                    RoomKind.Rest,
                    RoomKind.Shop
                }

                )
                {
                    var chosen = available[random.Next(available.Count)];
                    chosen.Kind = kind;
                    available.Remove(chosen);
                }

                foreach (var node in available)
                {
                    int roll = random.Next(100);
                    node.Kind = roll < 40 ? RoomKind.Question : roll < 48 ? RoomKind.Rest : roll < 55 ? RoomKind.Shop : roll < 65 && node.Lane != 3 ? RoomKind.Elite : RoomKind.Fight;
                }
            }

            // Retain two ordinary fights so investigation always has two targets.
            if (nodes.Count(n => n.Kind == RoomKind.Fight) < 2)
            {
                var spare = nodes.First(n => n.Floor >= 2 && n.Floor <= LevelCount && (n.Kind == RoomKind.Question || nodes.Count(other => other.District == n.District && other.Kind == n.Kind) > 1));
                spare.Kind = RoomKind.Fight;
            }

            // Fill a shuffled finite pool: guarantees never depend on repeated rerolls.
            var pool = nodes.Where(n => n.Floor >= 2 && n.Floor <= LevelCount && n.Kind == RoomKind.Fight).ToList();
            int questions = nodes.Count(n => n.Kind == RoomKind.Question);
            while (questions < 10 && pool.Count > 1)
            {
                int index = random.Next(pool.Count);
                pool[index].Kind = RoomKind.Question;
                pool.RemoveAt(index);
                questions++;
            }

            // Plenty of fights normally remain. For any seed, optional extra service
            // nodes may also become questions, retaining one of every required kind.
            if (questions < 10)
            {
                foreach (var node in nodes.Where(n => n.Floor >= 2 && n.Floor <= LevelCount))
                {
                    if (questions >= 10)
                        break;
                    if (node.Kind == RoomKind.Question || node.Kind == RoomKind.Fight)
                        continue;
                    if (nodes.Count(n => n.District == node.District && n.Kind == node.Kind) <= 1)
                        continue;
                    node.Kind = RoomKind.Question;
                    questions++;
                }
            }

            if (questions < 10)
                throw new InvalidOperationException("Insufficient question nodes.");
            return new ActMap(seed, nodes);
        }

        private static MapNode Nearest(List<MapNode> row, int lane, Random random)
        {
            int distance = row.Min(n => Math.Abs(n.Lane - lane));
            var choices = row.Where(n => Math.Abs(n.Lane - lane) == distance).ToList();
            return choices[random.Next(choices.Count)];
        }
    }

    public sealed class MapRun
    {
        public ActMap Map { get; private set; }
        public MapNode Current { get; private set; }
        public MapNode Pending { get; private set; }
        public bool Finished { get; private set; }
        public bool PendingIsRevisit { get; private set; }

        private readonly HashSet<int> visited = new HashSet<int>();
        public MapRun(ActMap map)
        {
            if (map == null)
                throw new ArgumentNullException("map");
            Map = map;
            Current = map.Nodes[0];
            visited.Add(Current.Id);
        }

        public bool Visited(int id)
        {
            return visited.Contains(id);
        }

        public bool CanEnter(int id)
        {
            return !Finished && Pending == null && Current.Exits.Contains(id);
        }

        public bool Enter(int id)
        {
            if (!CanEnter(id))
                return false;
            Pending = Map.Nodes[id];
            return true;
        }

        public bool CompleteRoom()
        {
            if (Pending != null && PendingIsRevisit)
            {
                Pending = null;
                PendingIsRevisit = false;
                return false;
            }

            if (Pending == null || Finished)
                return false;
            Current = Pending;
            Pending = null;
            visited.Add(Current.Id);
            Finished = Current.Kind == RoomKind.Boss;
            return true;
        }

        public bool ReopenCurrent()
        {
            if (Pending != null || Current.Kind == RoomKind.Start)
                return false;
            Pending = Current;
            PendingIsRevisit = true;
            return true;
        }
    }
}
