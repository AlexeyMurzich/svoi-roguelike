using System;

namespace ShpilSvoih
{
    public enum MimicratorForm { Fairy, Beacon, Bear, Mussel }

    public sealed class CombatEnemy
    {
        public int Id { get; private set; }
        public EnemyKind Kind { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public int Block { get; private set; }
        public int CyclePosition { get; private set; }
        public bool IsAlive { get { return Health > 0; } }
        public bool WillHowl { get { return Kind == EnemyKind.StrayDogs && CyclePosition == 2; } }
        public MimicratorForm Form { get { return (MimicratorForm)CyclePosition; } }
        public bool WillStun { get { return Kind == EnemyKind.Mimicrator && Form == MimicratorForm.Beacon; } }
        public bool WillDefend { get { return Kind == EnemyKind.Mimicrator && Form == MimicratorForm.Mussel; } }
        public int AttackDamage
        {
            get
            {
                if (Kind == EnemyKind.Mimicrator)
                    return Form == MimicratorForm.Beacon ? 6 : Form == MimicratorForm.Bear ? 14 : Form == MimicratorForm.Mussel ? 0 : 5;
                return Kind == EnemyKind.Hedgehog ? 10 : 5;
            }
        }
        public int Thorns { get { return Kind == EnemyKind.Hedgehog ? 2 : 0; } }
        public string Name { get { return Kind == EnemyKind.StrayDogs ? (Id == 0 ? "Серый пёс" : "Рыжий пёс") : EnemyCatalog.Name(Kind); } }
        public string Intent
        {
            get
            {
                if (Kind == EnemyKind.Mimicrator)
                    return MimicratorIntent(Form);
                return WillHowl ? "Вой · Слабость" : Kind == EnemyKind.Hedgehog ? "Иглы · 10 / Шипы · 2" :
                    Kind == EnemyKind.RabidHedgehog ? "Укус · 5 / Бешенство" : "Укус · 5";
            }
        }

        public static string MimicratorIntent(MimicratorForm form)
        {
            return form == MimicratorForm.Mussel ? "Мидия · 12 блока" : form == MimicratorForm.Beacon ? "Мигалка · 6 + оглушение" :
                form == MimicratorForm.Bear ? "Мишка · 14 урона" : "Фея · 5 урона";
        }

        internal CombatEnemy(int id, EnemyKind kind, int offset)
        {
            Id = id;
            Kind = kind;
            Health = MaxHealth = kind == EnemyKind.Mimicrator ? 60 : CombatState.EnemyMaxHealth;
            CyclePosition = offset;
        }

        internal void ReceiveDamage(int damage)
        {
            damage = Math.Max(0, damage);
            int absorbed = Math.Min(Block, damage);
            Block -= absorbed;
            Health = Math.Max(0, Health - (damage - absorbed));
        }

        internal void BeginAction() { Block = 0; }
        internal void Defend() { Block = 12; }
        internal void CompleteAction()
        {
            CyclePosition = Kind == EnemyKind.Mimicrator ? (CyclePosition == 3 ? 1 : CyclePosition + 1) : (CyclePosition + 1) % 3;
        }
    }
}
