namespace ShpilSvoih
{
    public static class BabaiRules
    {
        public const int MaxHealth = 40, Damage = 10, InitialDistance = 5;
    }
    // Battle-local state. One pull per resolved lasso, independent of damage/block.
    public sealed class BabaiTrap
    {
        public int Distance { get; private set; }
        public bool Caught { get { return Distance == 0; } }
        public BabaiTrap() { Distance = BabaiRules.InitialDistance; }
        internal void Pull(HeroState hero)
        {
            if (Caught) return;
            Distance--;
            if (Caught) { hero.DefeatByCapture(); if (hero.Health > 0) Distance = 1; }
        }
    }
}
