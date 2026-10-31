using System;
using System.Collections.Generic;

namespace ShpilSvoih
{
    // Progression follows completed ordinary fights, not floors, events or elites.
    public static class OrdinaryEncounters
    {
        static readonly EnemyKind[] EasyPool = { EnemyKind.Rat, EnemyKind.StrayDogs, EnemyKind.Hedgehogs };

        public static EnemyKind Select(int seed, int node, ICollection<EnemyKind> completed)
        {
            if (completed == null) throw new ArgumentNullException("completed");
            // Hard encounters stay in all districts; alternate the two available groups.
            // The easy pool is permanently exhausted after the first three completions.
            if (completed.Count >= EasyPool.Length)
            {
                EnemyKind last = EnemyKind.Rat;
                foreach (var value in completed) last = value;
                if (last == EnemyKind.Babai) return EnemyKind.SkaterMaster;
                if (last == EnemyKind.SkaterMaster) return EnemyKind.Babai;
                return ((uint)seed ^ (uint)node * 0x9E3779B9u) % 2 == 0 ? EnemyKind.Babai : EnemyKind.SkaterMaster;
            }
            int remaining = 0;
            foreach (var enemy in EasyPool)
                if (!completed.Contains(enemy)) remaining++;
            if (remaining == 0) throw new InvalidOperationException("Easy encounter history is inconsistent.");
            uint hash;
            unchecked
            {
                hash = (uint)seed ^ (uint)node * 0x9E3779B9u ^ (uint)completed.Count * 0x27D4EB2Du;
                hash ^= hash >> 16; hash *= 0x85EBCA6Bu; hash ^= hash >> 13;
            }
            int choice = (int)(hash % (uint)remaining);
            foreach (var enemy in EasyPool)
                if (!completed.Contains(enemy) && choice-- == 0) return enemy;
            throw new InvalidOperationException("No unused easy encounter is available.");
        }

        public static int Completed(MapRun run)
        {
            return run.CompletedOrdinaryFights;
        }

        // Concrete contents may change after completing another room. Investigation
        // shows the current cached enemy; it never generates or rerolls on hover.
        public static string Preview(MapRun run, int nodeId)
        {
            if (!run.Visited(nodeId) && !run.Reachable(nodeId)) return "Путь недоступен";
            return EnemyCatalog.Name(run.OrdinaryEnemyAt(nodeId));
        }
    }
}
