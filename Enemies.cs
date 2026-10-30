using System;

namespace ShpilSvoih
{
    public enum EnemyKind { Rat, StrayDogs, Hedgehog, RabidHedgehog, Hedgehogs, Mimicrator, Toha, Babai, Fomatreshka, Fan, Doctors, FemaleDoctor, MaleDoctor }

    public static class EnemyCatalog
    {
        public static string Name(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Doctors: return "Чёрные трансплантологи";
                case EnemyKind.FemaleDoctor: return "Чёрный трансплантолог";
                case EnemyKind.MaleDoctor: return "Чёрный трансплантолог";
                case EnemyKind.Fomatreshka: return "Фоматрешка";
                case EnemyKind.Fan: return "Фанатка";
                case EnemyKind.Babai: return "Бабай";
                case EnemyKind.Toha: return "Тоха";
                case EnemyKind.Mimicrator: return "Мимикратор";
                case EnemyKind.Hedgehogs: return "Ежи";
                case EnemyKind.StrayDogs: return "Бродячие собаки";
                case EnemyKind.Hedgehog: return "Колючий ёж";
                case EnemyKind.RabidHedgehog: return "Безумный ёж";
                default: return "Большая крыса";
            }
        }

        public static EnemyKind ForEliteNode(int seed, int node, int district)
        {
            if (district == 2)
            {
                unchecked
                {
                    uint hash = (uint)seed * 0x27D4EB2Du ^ (uint)node * 0x85EBCA6Bu;
                    hash ^= hash >> 15;
                    if (hash % 3 == 0) return EnemyKind.Fomatreshka;
                }
            }
            return ForEliteNode(seed, node);
        }

        public static EnemyKind ForEliteNode(int seed, int node)
        {
            unchecked
            {
                uint hash = (uint)seed * 0x85EBCA6Bu ^ (uint)node * 0x9E3779B9u;
                hash ^= hash >> 16;
                return (hash & 1) == 0 ? EnemyKind.Mimicrator : EnemyKind.Toha;
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
