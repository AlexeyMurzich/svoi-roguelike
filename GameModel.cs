using System;
using System.Collections.Generic;
using System.Linq;

namespace ShpilSvoih
{
    // Provisional numerical balance; intentionally separate from the UI.
    public sealed class Balance
    {
        public int MoneyLostPerTurn = 1;
        public int HealthLostPerDebtUnit = 1;
        public int TearsPerDamage = 1;
        public int BonusDamagePerTear = 1;
        public int BaseDraw = 3;
        public int ExtraDrawIllya = 2;
        public int AutoCardsIllya = 2;
        internal void Validate()
        {
            if (MoneyLostPerTurn < 0 || HealthLostPerDebtUnit < 0 || TearsPerDamage < 0 || BonusDamagePerTear < 0 || BaseDraw < 0 || ExtraDrawIllya < 0 || AutoCardsIllya < 0)
                throw new ArgumentOutOfRangeException("balance", "Balance values must not be negative.");
        }
    }

    public enum CardFamily
    {
        None,
        Patriarchal,
        Feminist
    }

    public sealed class HeroState
    {
        public readonly int Hero;
        public readonly StartingEffects Effects;
        public readonly Balance Settings;
        public readonly DoctorProgress Doctors = new DoctorProgress();
        public decimal Money { get; private set; }
        public bool ShutGod { get; private set; }
        public string MoneyDisplay { get { return ShutGod ? "∞" : Money.ToString(); } }
        public int DebugCardMultiplier { get { return ShutGod ? 3 : 1; } }
        public bool EnableShutGod()
        {
            if (Hero != 3 || Health <= 0) return false;
            ShutGod = true;
            Money = 0;
            return true;
        }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }

        private readonly List<CardData> deck = new List<CardData>();
        public System.Collections.ObjectModel.ReadOnlyCollection<CardData> Deck { get; private set; }
        public int Tears { get; private set; }
        public CardFamily FamilyThisTurn { get; private set; }

        public bool CanBorrow
        {
            get
            {
                return Effects.Has("phone");
            }
        }

        public HeroState(int hero, int health, int money, Balance settings)
        {
            if (hero < 0 || hero > 3)
                throw new ArgumentOutOfRangeException("hero");
            if (health < 0)
                throw new ArgumentOutOfRangeException("health");
            if (money < 0)
                throw new ArgumentOutOfRangeException("money");
            if (settings == null)
                throw new ArgumentNullException("settings");
            settings.Validate();
            Hero = hero;
            Health = health;
            MaxHealth = health;
            Deck = deck.AsReadOnly();
            Money = money;
            Settings = settings;
            Effects = new StartingEffects(this);
        }

        public void AddCard(CardData card)
        {
            if (card == null)
                throw new ArgumentNullException("card");
            Effects.OnReceived(card);
            deck.Add(card);
        }

        public decimal Debt
        {
            get
            {
                return Math.Max(0, -Money);
            }
        }

        public int BonusDamage
        {
            get
            {
                return Hero == 0 ? Saturate((long)Tears * Settings.BonusDamagePerTear) : 0;
            }
        }

        public int CardsToDraw
        {
            get
            {
                return Saturate((long)Settings.BaseDraw + (Hero == 1 ? Settings.ExtraDrawIllya : 0));
            }
        }

        public void BeginTurn()
        {
            Settings.Validate();
            FamilyThisTurn = CardFamily.None;
            Effects.BeginTurn();
            if (Hero == 3)
                ChangeMoney(-Settings.MoneyLostPerTurn);
        }

        public void ChangeMoney(decimal amount)
        {
            Settings.Validate();
            if (ShutGod) return;
            decimal previousDebt = Debt;
            decimal nextMoney = checked(Money + amount);
            if (Hero == 3 && !CanBorrow)
                nextMoney = Math.Max(0, nextMoney);
            int damage = Hero == 3 ? DebtDamage(previousDebt, Math.Max(0, -nextMoney)) : 0;
            Money = nextMoney;
            Health -= damage;
            ReviveIfNeeded();
        }

        int DebtDamage(decimal previousDebt, decimal nextDebt)
        {
            int coefficient = Settings.HealthLostPerDebtUnit;
            if (nextDebt <= previousDebt || coefficient == 0 || Health == 0)
                return 0;
            decimal increase = nextDebt - previousDebt;
            if (increase >= (Health + 1m) / coefficient)
                return Health;
            // The integer portion cancels. Keeping only the old fractional charge
            // preserves ceiling behavior without multiplying an enormous total debt.
            decimal previousFraction = (previousDebt % 1) * coefficient;
            decimal damage = Math.Ceiling(previousFraction + increase * coefficient) - Math.Ceiling(previousFraction);
            return (int)Math.Min(Health, damage);
        }

        public bool CanAffordMoneyCard(int price, int? minimumMoney = null)
        {
            return Hero == 3 && Health > 0 && price >= 0 && (ShutGod || ((!minimumMoney.HasValue || Money >= minimumMoney.Value) && (CanBorrow || Money >= Effects.GoldPrice(price))));
        }

        public bool PayMoneyCard(int price, int? minimumMoney = null)
        {
            if (!CanAffordMoneyCard(price, minimumMoney))
                return false;
            ChangeMoney(-Effects.GoldPrice(price));
            return true;
        }

        internal void DefeatByCapture() { Health = 0; ReviveIfNeeded(); }
        internal void ReduceMaximumHealth()
        {
            MaxHealth = Math.Max(1, (int)((long)MaxHealth * 3 / 4));
            Health = Math.Min(Health, MaxHealth);
        }
        public void HealFully() { if (Health > 0) Health = MaxHealth; }
        void ReviveIfNeeded()
        {
            if (Health == 0 && Doctors.UseHeart()) Health = Math.Max(1, (int)(((long)MaxHealth + 3) / 4));
        }
        internal void ReceiveStatusLoss(int amount)
        {
            if (Effects.NegatesFirstTurn) return;
            LoseHealth(amount);
        }
        void LoseHealth(int amount)
        {
            int lost = Math.Min(Health, Math.Max(0, amount));
            Health -= lost;
            if (Hero == 0) Tears = Saturate((long)Tears + (long)lost * Settings.TearsPerDamage);
            ReviveIfNeeded();
        }

        public void ReceiveDamage(int actualHealthLost)
        {
            Settings.Validate();
            if (Effects.NegatesFirstTurn)
                return;
            LoseHealth(actualHealthLost);
        }

        public bool CanUseFamily(CardFamily family)
        {
            return Hero == 2 && (family == CardFamily.Patriarchal || family == CardFamily.Feminist) && (FamilyThisTurn == CardFamily.None || FamilyThisTurn == family);
        }

        public bool UseFamily(CardFamily family)
        {
            if (!CanUseFamily(family))
                return false;
            FamilyThisTurn = family;
            return true;
        }

        public int[] ChooseAutomaticCards(int handSize, Random random)
        {
            if (Hero != 1 || handSize <= 0)
                return new int[0];
            if (random == null)
                throw new ArgumentNullException("random");
            var indices = Enumerable.Range(0, handSize).ToList();
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int value = indices[i];
                indices[i] = indices[j];
                indices[j] = value;
            }

            return indices.Take(Settings.AutoCardsIllya).ToArray();
        }

        private static int Saturate(long value)
        {
            return (int)Math.Min(int.MaxValue, value);
        }
    }
}
