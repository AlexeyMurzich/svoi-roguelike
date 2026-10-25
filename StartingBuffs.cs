using System;
using System.Collections.Generic;
using System.Linq;

namespace ShpilSvoih
{
    public sealed class StartingBuff
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string Icon { get; private set; }
        public int Hero { get; private set; }

        public StartingBuff(int hero, string id, string name, string description, string icon)
        {
            Hero = hero;
            Id = id;
            Name = name;
            Description = description;
            Icon = icon;
        }

        public static readonly System.Collections.ObjectModel.ReadOnlyCollection<StartingBuff> All = Array.AsReadOnly(new StartingBuff[]
        {
            new StartingBuff(0, "shield", "Саттен-киссюн", "Отменяет весь урон и все дебафы, полученные в первом ходу боя.", "placeholder"),
            new StartingBuff(0, "zalt", "Зальтраак", "В начале боя добавляет в руку особую универсальную карту: 6 защиты, 6 урона, стоимость 0.", "placeholder"),
            new StartingBuff(0, "exchange", "Равноценный обмен", "Один раз за забег позволяет заменить любую выбранную награду за бой.", "exchange"),
            new StartingBuff(1, "polish", "Карта поляка", "При получении карты с польским названием она получает зачарование «wieczny»: её нельзя удалить или продать, зато она стоит на 50% маны меньше.", "id"),
            new StartingBuff(1, "baton", "Полицейская дубинка", "В начале первого раунда боя даёт +3 силы. Змагары получают двойной урон.", "baton"),
            new StartingBuff(1, "armor", "Армейский бронежилет", "Все навыки, дающие защиту, при добавлении в колоду получают +2 защиты.", "armor"),
            new StartingBuff(2, "feather", "Рыцарь пера", "Феминистические атаки накладывают раздражение.", "feather"),
            new StartingBuff(2, "processor", "Кухонный комбайн", "В начале каждого хода брюнет получает +2 HP. Его моделька растёт вместе с максимальным HP.", "processor"),
            new StartingBuff(2, "investigation", "Журналистское расследование", "Раскрывает, кто находится в двух случайных вопросах, двух элитках и двух обычных боях.", "investigation"),
            new StartingBuff(3, "ring", "Золотое кольцо", "Сразу даёт +300 золота.", "ring"),
            new StartingBuff(3, "coupon", "Скидочный талон", "Даёт скидку 10% на все карты.", "coupon"),
            new StartingBuff(3, "phone", "Мобильный телефон", "Разрешает уходить в долг. Рост долга уменьшает здоровье Шутена.", "phone")
        });
        public static StartingBuff Find(string id)
        {
            return All.FirstOrDefault(b => b.Id == id);
        }

        public static StartingBuff[] For(int hero)
        {
            return All.Where(b => b.Hero == hero).ToArray();
        }
    }

    // Data contracts for future user-authored cards and maps; not a card catalogue.
    public sealed class CardData
    {
        public string Name = "", Enchantment = "";
        public decimal ManaCost, GoldCost;
        public bool PolishName, Skill, Attack;
        private bool removable = true, sellable = true;
        public bool WiecznyApplied { get; internal set; }
        public bool ArmorApplied { get; internal set; }

        public bool Removable
        {
            get
            {
                return removable && !WiecznyApplied;
            }

            set
            {
                removable = value;
            }
        }

        public bool Sellable
        {
            get
            {
                return sellable && !WiecznyApplied;
            }

            set
            {
                sellable = value;
            }
        }

        public int Damage, Block;
        public CardFamily Family;

        internal bool IsValid
        {
            get { return ManaCost >= 0 && GoldCost >= 0 && Damage >= 0 && Block >= 0 && Family >= CardFamily.None && Family <= CardFamily.Feminist; }
        }

        internal void Validate()
        {
            if (!IsValid)
                throw new ArgumentOutOfRangeException("card", "Card costs, damage, block and family must be valid.");
        }
    }

    public enum IntelKind
    {
        Question,
        Elite,
        Fight,
        Other
    }

    public sealed class IntelNode
    {
        public IntelKind Kind;
        public string Identity = "";
        public bool Revealed;
    }

    public sealed class StartingEffects
    {
        public readonly HeroState Owner;
        public StartingBuff Chosen { get; private set; }
        public int Turn { get; private set; }
        public int Strength { get; private set; }

        public int CompanionHp, CompanionMaxHp;
        public bool RewardExchangeUsed { get; private set; }

        private bool exchangeInProgress;
        private readonly HashSet<IList<IntelNode>> revealedMaps = new HashSet<IList<IntelNode>>();
        public StartingEffects(HeroState owner)
        {
            if (owner == null)
                throw new ArgumentNullException("owner");
            Owner = owner;
        }

        public bool Has(string id)
        {
            return Chosen != null && Chosen.Id == id;
        }

        public bool Choose(string id)
        {
            var b = StartingBuff.Find(id);
            if (Chosen != null || b == null || b.Hero != Owner.Hero)
                return false;
            Chosen = b;
            if (id == "ring")
                Owner.ChangeMoney(300);
            return true;
        }

        public decimal GoldPrice(decimal price)
        {
            if (price < 0)
                throw new ArgumentOutOfRangeException("price");
            return price * (Has("coupon") ? .9m : 1m);
        }

        public void OnReceived(CardData card)
        {
            if (card == null)
                throw new ArgumentNullException("card");
            card.Validate();
            if (Has("polish") && card.PolishName)
            {
                if (!card.WiecznyApplied)
                {
                    card.ManaCost *= .5m;
                    card.WiecznyApplied = true;
                }

                card.Enchantment = "wieczny";
                card.Removable = card.Sellable = false;
            }

            if (Has("armor") && card.Skill && card.Block > 0 && !card.ArmorApplied)
            {
                card.Block = card.Block > int.MaxValue - 2 ? int.MaxValue : card.Block + 2;
                card.ArmorApplied = true;
            }
        }

        public List<CardData> StartCombat()
        {
            Turn = 0;
            Strength = Has("baton") ? 3 : 0;
            var hand = new List<CardData>();
            if (Has("zalt"))
                hand.Add(new CardData { Name = "Зальтраак", Damage = 6, Block = 6, ManaCost = 0, Attack = true, Skill = true });
            return hand;
        }

        public void BeginTurn()
        {
            if (Turn < int.MaxValue)
                Turn++;
            if (Has("processor"))
            {
                CompanionMaxHp = (int)Math.Min(int.MaxValue, (long)CompanionMaxHp + 2);
                CompanionHp = (int)Math.Min(CompanionMaxHp, (long)CompanionHp + 2);
            }
        }

        public bool NegatesFirstTurn
        {
            get
            {
                return Has("shield") && Turn == 1;
            }
        }

        public bool AcceptDebuff()
        {
            return !NegatesFirstTurn;
        }

        public int TargetMultiplier(bool zmagar)
        {
            return Has("baton") && zmagar ? 2 : 1;
        }

        // Irritation's numeric effect awaits the user's rule; only application is defined.
        public bool AppliesIrritation(CardData card)
        {
            if (card == null)
                throw new ArgumentNullException("card");
            return Has("feather") && card.Attack && card.Family == CardFamily.Feminist;
        }

        // Call once after constructing a map. Re-entering the same map cannot reveal more nodes.
        public int Reveal(IList<IntelNode> nodes, Random rng)
        {
            if (nodes == null)
                throw new ArgumentNullException("nodes");
            if (rng == null)
                throw new ArgumentNullException("rng");
            if (!Has("investigation") || revealedMaps.Contains(nodes))
                return 0;
            int count = 0;
            foreach (var kind in new[]
            {
                IntelKind.Question,
                IntelKind.Elite,
                IntelKind.Fight
            }

            )
            {
                var pool = nodes.Where(n => n != null && n.Kind == kind && !n.Revealed).Distinct().ToList();
                for (int i = 0; i < 2 && pool.Count > 0; i++)
                {
                    int at = rng.Next(pool.Count);
                    pool[at].Revealed = true;
                    pool.RemoveAt(at);
                    count++;
                }
            }

            revealedMaps.Add(nodes);
            return count;
        }

        public bool TryExchangeCombatReward<T>(Func<T> replacement, out T result)
            where T : class
        {
            if (replacement == null)
                throw new ArgumentNullException("replacement");
            result = null;
            if (!Has("exchange") || RewardExchangeUsed || exchangeInProgress)
                return false;
            exchangeInProgress = true;
            try
            {
                result = replacement();
                if (result == null)
                    return false;
                RewardExchangeUsed = true;
                return true;
            }
            finally
            {
                exchangeInProgress = false;
            }
        }
    }
}
