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
        public int ActionsTaken { get; private set; }
        public bool IsDarkToha { get; private set; }
        public bool IsActing { get; private set; }
        public bool Fled { get; private set; }
        public int Vigor { get; private set; }
        public bool WillSummon { get { return Kind == EnemyKind.Fomatreshka && ActionsTaken == 0; } }
        public bool WillDrink { get { return Kind == EnemyKind.Fomatreshka && ActionsTaken % 2 == 1; } }
        public bool IsAlive { get { return Health > 0 && !Fled; } }
        public bool WillHowl { get { return Kind == EnemyKind.StrayDogs && CyclePosition == 2; } }
        public MimicratorForm Form { get { return (MimicratorForm)CyclePosition; } }
        public MimicratorForm NextForm { get { return ActionsTaken == 0 ? MimicratorForm.Fairy : (MimicratorForm)((CyclePosition + 1) % 4); } }
        public int HitCount { get { return Kind == EnemyKind.Mimicrator && Form == MimicratorForm.Mussel ? 2 : 1; } }
        public bool WillStun { get { return Kind == EnemyKind.Mimicrator && Form == MimicratorForm.Beacon; } }
        public bool WillDefend { get { return Kind == EnemyKind.Mimicrator && Form == MimicratorForm.Mussel; } }
        public int AttackDamage
        {
            get
            {
                if (Kind == EnemyKind.Fomatreshka) return WillSummon || WillDrink ? 0 : FomatreshkaRules.ShotDamage + Vigor;
                if (Kind == EnemyKind.Fan) return FomatreshkaRules.FanDamage;
                if (Kind == EnemyKind.Babai) return BabaiRules.Damage;
                if (Kind == EnemyKind.Toha) return IsDarkToha ? TohaRules.DarkDamage : TohaRules.LightDamage;
                if (Kind == EnemyKind.Mimicrator)
                    return Form == MimicratorForm.Beacon ? 6 : Form == MimicratorForm.Bear ? 14 : Form == MimicratorForm.Mussel ? 5 : 0;
                return Kind == EnemyKind.Hedgehog ? 10 : 5;
            }
        }
        public int Thorns { get { return Kind == EnemyKind.Hedgehog ? 2 : 0; } }
        public string Name { get { return Kind == EnemyKind.StrayDogs ? (Id == 0 ? "Серый пёс" : "Рыжий пёс") : Kind == EnemyKind.Toha ? "Тоха · " + (IsDarkToha ? "Тёмная сторона" : "Светлая сторона") : EnemyCatalog.Name(Kind); } }
        public string Intent
        {
            get
            {
                if (Kind == EnemyKind.Fomatreshka) return WillSummon ? "Призыв · 3 фанатки" : WillDrink ? "Gorilla · Бодрость +" + FomatreshkaRules.Vigor : "Императрица · " + AttackDamage;
                if (Kind == EnemyKind.Fan) return "Атака · " + AttackDamage;
                if (Kind == EnemyKind.Babai) return "Лассо · " + BabaiRules.Damage + " / Притягивание · 1";
                if (Kind == EnemyKind.Toha) return (IsDarkToha ? "Тёмный наезд · " : "Наезд · ") + AttackDamage;
                if (Kind == EnemyKind.Mimicrator)
                    return MimicratorIntent(IsActing ? Form : NextForm);
                return WillHowl ? "Вой · Слабость" : Kind == EnemyKind.Hedgehog ? "Иглы · 10 / Шипы · 2" :
                    Kind == EnemyKind.RabidHedgehog ? "Укус · 5 / Бешенство" : "Укус · 5";
            }
        }

        public static string MimicratorIntent(MimicratorForm form)
        {
            return form == MimicratorForm.Mussel ? "Мидия · 5 × 2 + 12 блока" : form == MimicratorForm.Beacon ? "Мигалка · 6 + оглушение" :
                form == MimicratorForm.Bear ? "Мишка · 14 урона" : "Фея · Отдых";
        }

        internal CombatEnemy(int id, EnemyKind kind, int offset)
        {
            Id = id;
            Kind = kind;
            Health = MaxHealth = kind == EnemyKind.Fomatreshka ? FomatreshkaRules.MaxHealth : kind == EnemyKind.Fan ? FomatreshkaRules.FanHealth : kind == EnemyKind.Babai ? BabaiRules.MaxHealth : kind == EnemyKind.Toha ? TohaRules.MaxHealth : kind == EnemyKind.Mimicrator ? 60 : CombatState.EnemyMaxHealth;
            CyclePosition = offset;
        }

        internal void ReceiveDamage(int damage)
        {
            damage = Math.Max(0, damage);
            int absorbed = Math.Min(Block, damage);
            Block -= absorbed;
            Health = Math.Max(0, Health - (damage - absorbed));
            if (Kind == EnemyKind.Toha && Health > 0 && (long)Health * 100 <= (long)MaxHealth * TohaRules.DarkThresholdPercent)
                IsDarkToha = true;
        }

        internal void BeginAction()
        {
            Block = 0;
            if (Kind == EnemyKind.Mimicrator) CyclePosition = (int)NextForm;
            IsActing = true;
        }
        internal void Defend() { Block = 12; }
        internal void DrinkGorilla() { Vigor = FomatreshkaRules.Vigor; }
        internal void ConsumeVigor() { Vigor = 0; }
        internal void Flee() { if (Health > 0) Fled = true; }
        internal void CompleteAction()
        {
            IsActing = false;
            ActionsTaken++;
            if (Kind != EnemyKind.Mimicrator) CyclePosition = (CyclePosition + 1) % 3;
        }
    }
}
