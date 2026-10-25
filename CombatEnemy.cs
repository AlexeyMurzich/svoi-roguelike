using System;

namespace ShpilSvoih
{
    public sealed class CombatEnemy
    {
        public int Id { get; private set; }
        public EnemyKind Kind { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public int CyclePosition { get; private set; }
        public bool IsAlive { get { return Health > 0; } }
        public bool WillHowl { get { return Kind == EnemyKind.StrayDogs && CyclePosition == 2; } }
        public int AttackDamage { get { return Kind == EnemyKind.Hedgehog ? 10 : 5; } }
        public int Thorns { get { return Kind == EnemyKind.Hedgehog ? 2 : 0; } }
        public string Name { get { return Kind == EnemyKind.StrayDogs ? (Id == 0 ? "Серый пёс" : "Рыжий пёс") : EnemyCatalog.Name(Kind); } }

        internal CombatEnemy(int id, EnemyKind kind, int offset)
        {
            Id = id;
            Kind = kind;
            Health = MaxHealth = CombatState.EnemyMaxHealth;
            CyclePosition = offset;
        }

        internal void ReceiveDamage(int damage)
        {
            Health = Math.Max(0, Health - Math.Max(0, damage));
        }

        internal void CompleteAction()
        {
            CyclePosition = (CyclePosition + 1) % 3;
        }
    }
}
