using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ShpilSvoih
{
    public enum CombatPhase
    {
        Player,
        Enemy,
        Victory,
        Defeat
    }

    public static class StarterDeck
    {
        public static void AddTo(HeroState hero)
        {
            for (int i = 0; i < 6; i++)
                hero.AddCard(new CardData { Name = i < 3 ? "Удар" : "Защита", Attack = i < 3, Skill = i >= 3, Damage = i < 3 ? 6 : 0, Block = i < 3 ? 0 : 5, ManaCost = 1, GoldCost = 10, Family = CardFamily.None });
        }
    }

    public sealed class CombatState
    {
        readonly List<CardData> draw = new List<CardData>(), discard = new List<CardData>(), hand = new List<CardData>();
        readonly Random random;
        public readonly HeroState Hero;
        public readonly EnemyKind Enemy;
        public int WeakTurns { get; private set; }
        public ReadOnlyCollection<CombatEnemy> Enemies { get; private set; }
        public int SelectedTarget { get; private set; }
        public int ActingEnemy { get; private set; }
        public bool EnemyWillHowl { get { return ActingEnemy >= 0 && ActingEnemy < Enemies.Count ? Enemies[ActingEnemy].WillHowl : Enemies.Any(e => e.IsAlive && e.WillHowl); } }
        public const int EnemyMaxHealth = 30, EnemyAttack = 5;
        public int EnemyHealth { get { return SelectedTarget >= 0 ? Enemies[SelectedTarget].Health : 0; } }
        public int Block { get; private set; }
        public decimal Energy { get; private set; }
        public int Turn { get; private set; }
        public int Shuffles { get; private set; }
        public CombatPhase Phase { get; private set; }
        public ReadOnlyCollection<CardData> Hand { get; private set; }

        public int DrawCount
        {
            get
            {
                return draw.Count;
            }
        }

        public int DiscardCount
        {
            get
            {
                return discard.Count;
            }
        }

        public int LastDamage { get; private set; }

        public CombatState(HeroState hero, Random random) : this(hero, random, EnemyKind.Rat) { }

        public CombatState(HeroState hero, Random random, EnemyKind enemy)
        {
            if (hero == null || random == null)
                throw new ArgumentNullException();
            Hero = hero;
            if (!Enum.IsDefined(typeof(EnemyKind), enemy)) throw new ArgumentOutOfRangeException("enemy");
            Enemy = enemy;
            this.random = random;
            Hand = hand.AsReadOnly();
            var enemies = new List<CombatEnemy> { new CombatEnemy(0, enemy, 0) };
            if (enemy == EnemyKind.StrayDogs) enemies.Add(new CombatEnemy(1, enemy, 1));
            Enemies = enemies.AsReadOnly();
            SelectedTarget = 0;
            ActingEnemy = -1;
            draw.AddRange(hero.Deck);
            Shuffle(draw);
            var bonus = hero.Effects.StartCombat();
            BeginPlayerTurn();
            if (Phase == CombatPhase.Player)
                hand.AddRange(bonus);
        }

        void Shuffle(List<CardData> cards)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var card = cards[i];
                cards[i] = cards[j];
                cards[j] = card;
            }
        }

        void BeginPlayerTurn()
        {
            Turn++;
            Energy = 3;
            Hero.BeginTurn();
            if (Hero.Health <= 0)
            {
                Phase = CombatPhase.Defeat;
                return;
            }

            Phase = CombatPhase.Player;
            int count = Hero.CardsToDraw;
            for (int i = 0; i < count; i++)
            {
                if (draw.Count == 0 && discard.Count > 0)
                {
                    draw.AddRange(discard);
                    discard.Clear();
                    Shuffle(draw);
                    Shuffles++;
                }

                if (draw.Count == 0)
                    break;
                int last = draw.Count - 1;
                hand.Add(draw[last]);
                draw.RemoveAt(last);
            }

            if (Hero.Hero == 1)
            {
                var selected = new List<CardData>();
                foreach (int index in Hero.ChooseAutomaticCards(hand.Count, random))
                    selected.Add(hand[index]);
                foreach (var card in selected)
                    TryPlay(hand.IndexOf(card));
            }
        }

        public decimal Cost(CardData card)
        {
            if (card == null)
                throw new ArgumentNullException("card");
            return Hero.Hero == 3 ? Hero.Effects.GoldPrice(card.GoldCost) : card.ManaCost;
        }

        public int DamageFor(CardData card)
        {
            if (card == null)
                throw new ArgumentNullException("card");
            int strength = Turn == 1 ? Hero.Effects.Strength : 0;
            long damage = Math.Max(0, (long)card.Damage + Hero.BonusDamage + strength);
            if (card.Attack && WeakTurns > 0) damage = damage * 3 / 4;
            return (int)Math.Min(int.MaxValue, damage);
        }

        public bool CanPlay(int index)
        {
            if (Phase != CombatPhase.Player || Hero.Health <= 0 || index < 0 || index >= hand.Count)
                return false;
            var card = hand[index];
            if (!card.IsValid) return false;
            if (card.Family != CardFamily.None && Hero.Hero == 2 && !Hero.CanUseFamily(card.Family))
                return false;
            decimal price = Cost(card);
            return price >= 0 && (Hero.Hero == 3 ? Hero.CanBorrow || Hero.Money >= price : Energy >= price);
        }

        public bool TryPlay(int index)
        {
            if (!CanPlay(index))
                return false;
            var card = hand[index];
            if (Hero.Hero == 3)
                Hero.ChangeMoney(-Cost(card));
            else
                Energy -= Cost(card);
            hand.RemoveAt(index);
            discard.Add(card);
            if (Hero.Health <= 0)
            {
                Phase = CombatPhase.Defeat;
                return true;
            }

            if (Hero.Hero == 2 && card.Family != CardFamily.None)
                Hero.UseFamily(card.Family);
            if (card.Attack)
            {
                int damage = DamageFor(card);
                Enemies[SelectedTarget].ReceiveDamage(damage);
                if (!Enemies[SelectedTarget].IsAlive)
                    SelectedTarget = NextLivingEnemy(0);
            }

            Block = (int)Math.Min(int.MaxValue, (long)Block + card.Block);
            if (NextLivingEnemy(0) < 0)
                Phase = CombatPhase.Victory;
            return true;
        }

        public bool EndPlayerTurn()
        {
            if (Phase != CombatPhase.Player)
                return false;
            discard.AddRange(hand);
            hand.Clear();
            if (WeakTurns > 0) WeakTurns--;
            Phase = CombatPhase.Enemy;
            ActingEnemy = NextLivingEnemy(0);
            return true;
        }

        public bool SelectTarget(int index)
        {
            if (Phase != CombatPhase.Player || index < 0 || index >= Enemies.Count || !Enemies[index].IsAlive)
                return false;
            SelectedTarget = index;
            return true;
        }

        int NextLivingEnemy(int start)
        {
            for (int i = start; i < Enemies.Count; i++)
                if (Enemies[i].IsAlive) return i;
            return -1;
        }

        public bool ResolveNextEnemyAction()
        {
            if (Phase != CombatPhase.Enemy || ActingEnemy < 0)
                return false;
            var actor = Enemies[ActingEnemy];
            int before = Hero.Health;
            if (actor.WillHowl)
            {
                if (Hero.Effects.AcceptDebuff()) WeakTurns = Math.Max(WeakTurns, 1);
            }
            else
            {
                int absorbed = Math.Min(Block, EnemyAttack);
                Block -= absorbed;
                Hero.ReceiveDamage(EnemyAttack - absorbed);
            }
            actor.CompleteAction();
            LastDamage = before - Hero.Health;
            ActingEnemy = NextLivingEnemy(ActingEnemy + 1);
            if (Hero.Health <= 0)
            {
                Phase = CombatPhase.Defeat;
                ActingEnemy = -1;
            }
            else if (ActingEnemy < 0)
            {
                Block = 0;
                BeginPlayerTurn();
            }
            return true;
        }

        // Model-only convenience: the UI resolves and animates each creature separately.
        public bool ResolveEnemyTurn()
        {
            if (Phase != CombatPhase.Enemy) return false;
            while (Phase == CombatPhase.Enemy) ResolveNextEnemyAction();
            return true;
        }
    }
}
