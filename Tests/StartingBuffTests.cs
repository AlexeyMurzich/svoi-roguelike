using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace ShpilSvoih
{
    public static class StartingBuffTests
    {
        static void Check(bool ok, string message)
        {
            TestSuite.Check(ok, message);
        }

        static HeroState Hero(int id, string buff)
        {
            var h = new HeroState(id, 50, 0, new Balance());
            Check(h.Effects.Choose(buff), "choose " + buff);
            return h;
        }

        public static void Run()
        {
            for (int i = 0; i < 4; i++)
                Check(StartingBuff.For(i).Length == 3, "three choices");
            Check(StartingBuff.All.Select(b => b.Id).Distinct().Count() == 12, "unique choices");
            var h = Hero(3, "ring");
            Check(h.Money == 300, "gold ring");
            Check(!h.Effects.Choose("ring") && h.Money == 300, "no duplicate reward");
            Check(!h.Effects.Choose("phone") && !h.CanBorrow, "one exclusive buff");
            Check(!new HeroState(0, 50, 0, new Balance()).Effects.Choose("ring"), "hero restriction");
            h = Hero(3, "coupon");
            Check(h.Effects.GoldPrice(10) == 9 && h.Effects.GoldPrice(1) == .9m, "exact discount");
            h.ChangeMoney(9);
            Check(h.PayMoneyCard(10, 0) && h.Money == 0, "discount at payment");
            Check(!h.PayMoneyCard(1, 0) && h.Money == 0, "no debt without phone");
            h.BeginTurn();
            Check(h.Money == 0 && h.Health == 50, "passive loss cannot borrow");
            h = Hero(3, "phone");
            Check(h.PayMoneyCard(5, 0) && h.Debt == 5 && h.Health == 45, "phone debt");
            Check(h.PayMoneyCard(2) && h.Debt == 7 && h.Health == 43, "phone allows further basic spending while indebted");
            Check(!h.PayMoneyCard(2, 10), "wealth gate still applies to powerful cards");
            h = Hero(1, "polish");
            var card = new CardData
            {
                PolishName = true,
                ManaCost = 3
            };
            h.Effects.OnReceived(card);
            h.Effects.OnReceived(card);
            Check(card.ManaCost == 1.5m && !card.Removable && !card.Sellable && card.Enchantment == "wieczny", "eternal enchantment exact half once");
            card = new CardData
            {
                ManaCost = 2
            };
            h.Effects.OnReceived(card);
            Check(card.ManaCost == 2 && card.Removable, "non polish unaffected");
            h = Hero(1, "baton");
            h.Effects.StartCombat();
            h.BeginTurn();
            Check(h.Effects.Strength == 3 && h.Effects.TargetMultiplier(true) == 2 && h.Effects.TargetMultiplier(false) == 1, "baton");
            h.BeginTurn();
            Check(h.Effects.Strength == 3, "no accumulating strength");
            h = Hero(1, "armor");
            card = new CardData
            {
                Skill = true,
                Block = 5
            };
            h.Effects.OnReceived(card);
            h.Effects.OnReceived(card);
            Check(card.Block == 7, "armor once");
            card = new CardData
            {
                Skill = false,
                Block = 5
            };
            h.Effects.OnReceived(card);
            Check(card.Block == 5, "non skill unchanged");
            h = Hero(2, "feather");
            Check(h.Effects.AppliesIrritation(new CardData { Attack = true, Family = CardFamily.Feminist }), "feminist irritation");
            Check(!h.Effects.AppliesIrritation(new CardData { Attack = true, Family = CardFamily.Patriarchal }), "other family unaffected");
            h = Hero(2, "processor");
            h.Effects.CompanionHp = 3;
            h.Effects.CompanionMaxHp = 5;
            h.BeginTurn();
            h.BeginTurn();
            Check(h.Effects.CompanionHp == 7 && h.Effects.CompanionMaxHp == 9, "companion grows every turn");
            h = Hero(2, "investigation");
            var nodes = new List<IntelNode>();
            foreach (var kind in new[]
            {
                IntelKind.Question,
                IntelKind.Elite,
                IntelKind.Fight,
                IntelKind.Other
            }

            )
                for (int i = 0; i < 4; i++)
                    nodes.Add(new IntelNode { Kind = kind, Identity = "known" });
            Check(h.Effects.Reveal(nodes, new Random(42)) == 6, "six revealed");
            foreach (var kind in new[]
            {
                IntelKind.Question,
                IntelKind.Elite,
                IntelKind.Fight
            }

            )
                Check(nodes.Count(n => n.Kind == kind && n.Revealed) == 2, "two each");
            Check(!nodes.Any(n => n.Kind == IntelKind.Other && n.Revealed), "other nodes untouched");
            Check(h.Effects.Reveal(new List<IntelNode> { new IntelNode { Kind = IntelKind.Question } }, new Random()) == 1, "small map reveal");
            h = Hero(0, "shield");
            h.Effects.StartCombat();
            h.BeginTurn();
            h.ReceiveDamage(12);
            Check(h.Health == 50 && h.Tears == 0 && !h.Effects.AcceptDebuff(), "first turn shield");
            h.BeginTurn();
            h.ReceiveDamage(7);
            Check(h.Health == 43 && h.Tears == 7 && h.Effects.AcceptDebuff(), "shield expires");
            h.Effects.StartCombat();
            h.BeginTurn();
            h.ReceiveDamage(9);
            Check(h.Health == 43, "shield resets each combat");
            h = Hero(0, "zalt");
            var hand = h.Effects.StartCombat();
            Check(hand.Count == 1 && hand[0].Damage == 6 && hand[0].Block == 6 && hand[0].ManaCost == 0, "special universal card");
            Check(h.Effects.StartCombat().Count == 1, "new card each combat");
            h = Hero(0, "exchange");
            string reward;
            Check(!h.Effects.TryExchangeCombatReward<string>(() => null, out reward) && !h.Effects.RewardExchangeUsed, "cancel does not consume");
            Check(h.Effects.TryExchangeCombatReward<string>(() => "new reward", out reward) && reward == "new reward", "replace reward");
            Check(!h.Effects.TryExchangeCombatReward<string>(() => "second", out reward), "one exchange per run");
        }
    }
}
