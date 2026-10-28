using System;
using System.Collections.Generic;
using System.Linq;

namespace ShpilSvoih
{
    // Room type stays Fight. Pool progression counts completed ordinary fights, never floors/elites.
    public static class OrdinaryEncounters
    {
        public static EnemyKind Select(int seed, int node, int completedFights)
        {
            if (completedFights < 0) throw new ArgumentOutOfRangeException("completedFights");
            if (completedFights >= 3)
            {
                unchecked
                {
                    uint hash = (uint)seed * 0x27D4EB2Du ^ (uint)node * 0x85EBCA6Bu;
                    hash ^= hash >> 15;
                    if (hash % 4 == 0) return EnemyKind.Babai;
                }
            }
            return EnemyCatalog.ForNode(seed, node);
        }
        public static int Completed(MapRun run)
        {
            return run.Map.Nodes.Count(n => n.Kind == RoomKind.Fight && run.Visited(n.Id));
        }
        // At a future fork the number of preceding fights can differ. Reveal all
        // possible identities rather than promise the wrong enemy for another route.
        public static string Preview(MapRun run, int nodeId)
        {
            int completed = Completed(run);
            var counts = new Dictionary<int, HashSet<int>> { { run.Current.Id, new HashSet<int> { 0 } } };
            foreach (var node in run.Map.Nodes.OrderBy(n => n.Floor))
            {
                HashSet<int> before;
                if (!counts.TryGetValue(node.Id, out before) || node.Id == nodeId) continue;
                foreach (int next in node.Exits)
                {
                    if (run.Map.Nodes[next].Floor > run.Map.Nodes[nodeId].Floor) continue;
                    HashSet<int> after;
                    if (!counts.TryGetValue(next, out after)) counts.Add(next, after = new HashSet<int>());
                    foreach (int count in before) after.Add(count + (run.Map.Nodes[next].Kind == RoomKind.Fight ? 1 : 0));
                }
            }
            HashSet<int> options;
            if (!counts.TryGetValue(nodeId, out options) || options.Count == 0) return "Путь недоступен";
            var names = options.Select(count => EnemyCatalog.Name(Select(run.Map.Seed, nodeId, Math.Max(0, completed + count - 1)))).Distinct().OrderBy(s => s).ToArray();
            return string.Join(" / ", names);
        }
    }
}
