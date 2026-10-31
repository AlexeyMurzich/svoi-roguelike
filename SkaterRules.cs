namespace ShpilSvoih
{
    public static class SkaterRules
    {
        public const int MasterHealth = 80, MasterDamage = 12;
        public const int PadawanHealth = 20, PadawanDamage = 4;
        public static bool IsHard(EnemyKind enemy) { return enemy == EnemyKind.Babai || enemy == EnemyKind.SkaterMaster; }
    }
}
