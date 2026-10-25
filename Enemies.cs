using System;

namespace ShpilSvoih
{
    public enum EnemyKind { Rat, StrayDogs, Hedgehog, RabidHedgehog, Hedgehogs }

    public static class EnemyCatalog
    {
        public static string Name(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Hedgehogs: return "Ежи";
                case EnemyKind.StrayDogs: return "Бродячие собаки";
                case EnemyKind.Hedgehog: return "Колючий ёж";
                case EnemyKind.RabidHedgehog: return "Безумный ёж";
                default: return "Большая крыса";
            }
        }

        public static EnemyKind ForNode(int seed, int node)
        {
            unchecked
            {
                uint value = (uint)seed ^ (uint)node * 0x9E3779B9u;
                value ^= value >> 16; value *= 0x85EBCA6Bu; value ^= value >> 13;
                uint encounter = value % 3;
                return encounter == 0 ? EnemyKind.Rat : encounter == 1 ? EnemyKind.StrayDogs : EnemyKind.Hedgehogs;
            }
        }
    }
}
