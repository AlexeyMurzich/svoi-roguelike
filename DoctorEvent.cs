using System;
using System.Collections.Generic;

namespace ShpilSvoih
{
    public enum Organ { Eye, Heart, Liver }
    public enum DoctorDrop { Eye, Heart, Liver, Chocolate }
    public enum QuestionKind { Quiet, Fight, Doctors }
    public enum DoctorChoice { None, Treatment, Battle, Victory }

    public static class DoctorRules
    {
        public const int FemaleHealth = 40, MaleHealth = 50;
        public const int Attack = 5, Poison = 2, Bleeding = 2;
        public static string NegativeName(Organ organ)
        {
            return organ == Organ.Eye ? "Потерянный глаз" : organ == Organ.Heart ? "Потерянное сердце" : "Потерянная печень";
        }
        public static string Name(DoctorDrop drop)
        {
            return drop == DoctorDrop.Eye ? "Глаз" : drop == DoctorDrop.Heart ? "Сердце" : drop == DoctorDrop.Liver ? "Печень" : "Шоколад «Бабаевский»";
        }
        public static string Description(DoctorDrop drop, bool used)
        {
            return drop == DoctorDrop.Eye ? "В комнатах «?» больше не выпадают обычные бои." :
                drop == DoctorDrop.Heart ? used ? "Сердце израсходовано. Второго возрождения не будет." : "Один раз за забег при нуле HP восстанавливает 25% максимального здоровья." :
                drop == DoctorDrop.Liver ? "Полная невосприимчивость к яду. От кровотечения не защищает." : "Ничего не делает, но всё равно приятно.";
        }
    }

    // Run-owned acquired relics and operation penalties; no UI or randomness on redraw.
    public sealed class DoctorProgress
    {
        readonly List<DoctorDrop> items = new List<DoctorDrop>();
        readonly List<Organ> losses = new List<Organ>();
        public System.Collections.ObjectModel.ReadOnlyCollection<Organ> LostOrgans { get; private set; }
        public int HeartHealthLost { get; private set; }
        public int RemainingFights(Organ organ) { return organ == Organ.Eye ? BlindFights : organ == Organ.Liver ? LiverFights : 0; }
        public System.Collections.ObjectModel.ReadOnlyCollection<DoctorDrop> Items { get; private set; }
        public int BlindFights { get; private set; }
        public int LiverFights { get; private set; }
        public bool HeartUsed { get; private set; }
        public DoctorProgress() { Items = items.AsReadOnly(); LostOrgans = losses.AsReadOnly(); }
        public bool Has(DoctorDrop drop) { return items.Contains(drop); }
        public bool Give(DoctorDrop drop)
        {
            if (!Enum.IsDefined(typeof(DoctorDrop), drop)) throw new ArgumentOutOfRangeException("drop");
            if (Has(drop)) return false;
            items.Add(drop);
            return true;
        }
        public void Lose(Organ organ, HeroState hero)
        {
            if (hero == null) throw new ArgumentNullException("hero");
            if (!Enum.IsDefined(typeof(Organ), organ)) throw new ArgumentOutOfRangeException("organ");
            if (losses.Contains(organ)) return;
            losses.Add(organ);
            if (organ == Organ.Eye) BlindFights = 3;
            else if (organ == Organ.Liver) LiverFights = 3;
            else if (organ == Organ.Heart) { int before = hero.MaxHealth; hero.ReduceMaximumHealth(); HeartHealthLost = before - hero.MaxHealth; }
            else throw new ArgumentOutOfRangeException("organ");
        }
        internal bool StartFight(out int poison)
        {
            bool hidden = BlindFights > 0;
            poison = LiverFights > 0 && !Has(DoctorDrop.Liver) ? 5 : 0;
            if (BlindFights > 0) BlindFights--;
            if (LiverFights > 0) LiverFights--;
            return hidden;
        }
        internal bool UseHeart()
        {
            if (!Has(DoctorDrop.Heart) || HeartUsed) return false;
            HeartUsed = true;
            return true;
        }
    }

    public sealed class DoctorEvent
    {
        public readonly Organ LostOrgan;
        public readonly DoctorDrop Drop;
        public DoctorChoice Choice { get; private set; }
        public bool RewardClaimed { get; private set; }
        public DoctorEvent(int seed)
        {
            var random = new Random(seed);
            LostOrgan = (Organ)random.Next(3);
            Drop = random.Next(5) == 0 ? DoctorDrop.Chocolate : (DoctorDrop)random.Next(3);
        }
        public bool Treat(HeroState hero)
        {
            if (hero == null) throw new ArgumentNullException("hero");
            if (Choice != DoctorChoice.None || hero.Health <= 0) return false;
            Choice = DoctorChoice.Treatment;
            hero.Doctors.Lose(LostOrgan, hero);
            hero.HealFully();
            return true;
        }
        public bool Fight()
        {
            if (Choice != DoctorChoice.None) return false;
            Choice = DoctorChoice.Battle;
            return true;
        }
        public bool ClaimReward(CombatState fight)
        {
            if (fight == null || fight.Enemy != EnemyKind.Doctors || fight.Phase != CombatPhase.Victory || Choice != DoctorChoice.Victory || RewardClaimed) return false;
            RewardClaimed = true;
            fight.Hero.Doctors.Give(Drop);
            return true;
        }
        public bool Reward(CombatState fight)
        {
            if (fight == null || fight.Enemy != EnemyKind.Doctors || fight.Phase != CombatPhase.Victory || Choice != DoctorChoice.Battle) return false;
            Choice = DoctorChoice.Victory;
            fight.Hero.HealFully();
            return true;
        }
    }
}
