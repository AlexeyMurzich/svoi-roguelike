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
        bool pendingStun;
        public bool StunPending { get { return pendingStun; } }
        public bool EnemyActionPrepared { get; private set; }
        public int EnemyImpactsResolved { get; private set; }
        public int CardsPlayedThisTurn { get; private set; }
        public int CardPlayLimit { get; private set; }
        public bool IsStunned { get { return CardPlayLimit == 1; } }
        public bool Rabies { get; private set; }
        public int Strength { get { return Rabies ? 2 : 0; } }
        public int Dexterity { get { return Rabies ? -2 : 0; } }
        public int LastRetaliation { get; private set; }
        public int BlockFor(CardData card)
        {
            if (card == null) throw new ArgumentNullException("card");
            return card.Block > 0 ? (int)Math.Min(int.MaxValue, Math.Max(0, (long)card.Block + Dexterity) * Hero.DebugCardMultiplier) : 0;
        }
        int ReceiveHit(int damage)
        {
            int before = Hero.Health;
            int absorbed = Math.Min(Block, damage);
            Block -= absorbed;
            Hero.ReceiveDamage(damage - absorbed);
            return before - Hero.Health;
        }
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
            var enemies = new List<CombatEnemy> { new CombatEnemy(0, enemy == EnemyKind.Hedgehogs ? EnemyKind.Hedgehog : enemy, 0) };
            if (enemy == EnemyKind.StrayDogs) enemies.Add(new CombatEnemy(1, enemy, 1));
            if (enemy == EnemyKind.Hedgehogs) enemies.Add(new CombatEnemy(1, EnemyKind.RabidHedgehog, 0));
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
            CardPlayLimit = pendingStun ? 1 : int.MaxValue;
            CardsPlayedThisTurn = 0;
            pendingStun = false;
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
            long damage = Math.Max(0, (long)card.Damage + Hero.BonusDamage + strength + (card.Attack ? Strength : 0));
            if (card.Attack && WeakTurns > 0) damage = damage * 3 / 4;
            return (int)Math.Min(int.MaxValue, damage * Hero.DebugCardMultiplier);
        }

        public bool CanPlay(int index)
        {
            if (Phase != CombatPhase.Player || Hero.Health <= 0 || CardsPlayedThisTurn >= CardPlayLimit || index < 0 || index >= hand.Count)
                return false;
            var card = hand[index];
            if (!card.IsValid) return false;
            if (card.Family != CardFamily.None && Hero.Hero == 2 && !Hero.CanUseFamily(card.Family))
                return false;
            decimal price = Cost(card);
            return price >= 0 && (Hero.Hero == 3 ? Hero.ShutGod || Hero.CanBorrow || Hero.Money >= price : Energy >= price);
        }

        public bool TryPlay(int index)
        {
            if (!CanPlay(index))
                return false;
            var card = hand[index];
            LastRetaliation = 0;
            CardsPlayedThisTurn++;
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
                var target = Enemies[SelectedTarget];
                target.ReceiveDamage(damage);
                if (target.Thorns > 0) LastRetaliation = ReceiveHit(target.Thorns);
                if (!Enemies[SelectedTarget].IsAlive)
                    SelectedTarget = NextLivingEnemy(0);
            }

            Block = (int)Math.Min(int.MaxValue, (long)Block + BlockFor(card));
            if (Hero.Health <= 0) Phase = CombatPhase.Defeat;
            else if (NextLivingEnemy(0) < 0) Phase = CombatPhase.Victory;
            return true;
        }

        public bool EndPlayerTurn()
        {
            if (Phase != CombatPhase.Player)
                return false;
            discard.AddRange(hand);
            hand.Clear();
            if (WeakTurns > 0) WeakTurns--;
            CardPlayLimit = int.MaxValue;
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

        // Called at form-entry during the enemy turn, before any attack impact.
        public bool PrepareNextEnemyAction()
        {
            if (Phase != CombatPhase.Enemy || ActingEnemy < 0 || EnemyActionPrepared) return false;
            var actor = Enemies[ActingEnemy];
            actor.BeginAction();
            EnemyActionPrepared = true;
            EnemyImpactsResolved = 0;
            if (actor.WillDefend) actor.Defend();
            if (actor.WillStun && Hero.Health > 0 && Hero.Effects.AcceptDebuff()) pendingStun = true;
            return true;
        }

        // Each animation resolves one impact; shell attacks share the same block pool.
        public bool ResolveNextEnemyImpact()
        {
            if (Phase != CombatPhase.Enemy || ActingEnemy < 0) return false;
            if (!EnemyActionPrepared) PrepareNextEnemyAction();
            var actor = Enemies[ActingEnemy];
            int before = Hero.Health;
            if (actor.WillHowl)
            {
                if (Hero.Effects.AcceptDebuff()) WeakTurns = Math.Max(WeakTurns, 1);
            }
            else if (!(actor.Kind == EnemyKind.Mimicrator && actor.Form == MimicratorForm.Fairy))
            {
                int lost = ReceiveHit(actor.AttackDamage);
                if (actor.Kind == EnemyKind.RabidHedgehog && lost > 0 && Hero.Health > 0 && Hero.Effects.AcceptDebuff())
                    Rabies = true;
            }
            EnemyImpactsResolved++;
            LastDamage = before - Hero.Health;
            if (Hero.Health <= 0)
            {
                Phase = CombatPhase.Defeat;
                ActingEnemy = -1;
                pendingStun = false;
                EnemyActionPrepared = false;
                return true;
            }
            if (EnemyImpactsResolved < actor.HitCount) return true;
            actor.CompleteAction();
            ActingEnemy = NextLivingEnemy(ActingEnemy + 1);
            EnemyActionPrepared = false;
            EnemyImpactsResolved = 0;
            if (ActingEnemy < 0)
            {
                Block = 0;
                BeginPlayerTurn();
            }
            return true;
        }

        public bool ResolveNextEnemyAction()
        {
            if (Phase != CombatPhase.Enemy || ActingEnemy < 0) return false;
            int actor = ActingEnemy;
            do { ResolveNextEnemyImpact(); }
            while (Phase == CombatPhase.Enemy && ActingEnemy == actor);
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
