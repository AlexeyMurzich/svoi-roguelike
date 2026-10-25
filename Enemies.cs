using System;

namespace ShpilSvoih
{
    public enum EnemyKind { Rat, StrayDogs }

    public static class EnemyCatalog
    {
        public static string Name(EnemyKind kind)
        {
            return kind == EnemyKind.StrayDogs ? "Бродячие собаки" : "Большая крыса";
        }

        public static EnemyKind ForNode(int seed, int node)
        {
            unchecked
            {
                uint value = (uint)seed ^ (uint)node * 0x9E3779B9u;
                value ^= value >> 16; value *= 0x85EBCA6Bu; value ^= value >> 13;
                return (value & 1) == 0 ? EnemyKind.Rat : EnemyKind.StrayDogs;
            }
        }
    }
}
