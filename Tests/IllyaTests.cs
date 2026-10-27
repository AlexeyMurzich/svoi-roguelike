using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace ShpilSvoih
{
    public static class IllyaTests
    {
        sealed class RollRandom : Random
        {
            public int Roll;
            public RollRandom(int roll) { Roll = roll; }
            public override int Next(int maxValue) { return maxValue == 5 ? Roll : maxValue - 1; }
        }

        static HeroState Hero(int id, bool polish)
        {
            var hero = new HeroState(id, 1000, 0, new Balance());
            if (polish) hero.Effects.Choose("polish");
            for (int i = 0; i < 5; i++)
                hero.AddCard(new CardData { Name = "Test " + i, Skill = true, Block = 1, ManaCost = 9, PolishName = polish });
            return hero;
        }

        public static void Rules()
        {
            var random = new RollRandom(4);
            var hero = Hero(1, false);
            var fight = new CombatState(hero, random);
            TestSuite.Check(fight.Energy == 3 && fight.CardsPlayedThisTurn == 2 && fight.DiscardCount == 2 && fight.Block == 2,
                "two expensive automatic cards resolve for free, once each");
            TestSuite.Check(fight.Hand.Count == 3 && fight.Hand.All(c => fight.Cost(c) == 4) && !fight.CanPlay(0),
                "inclusive upper bound is displayed and enforced for manual play");
            var oldHand = fight.Hand.ToArray();
            random.Roll = 0;
            for (int i = 0; i < 10; i++)
                TestSuite.Check(oldHand.All(c => fight.Cost(c) == 4) && !fight.TryPlay(0) && fight.Energy == 3,
                    "queries and rejected plays do not reroll or spend energy");
            TestSuite.Check(fight.EndPlayerTurn() && fight.ResolveEnemyTurn(), "next turn resolves");
            TestSuite.Check(fight.Shuffles > 0 && fight.Hand.Any(c => oldHand.Contains(c)) && fight.Hand.All(c => fight.Cost(c) == 0),
                "redrawn cards reroll after discard reshuffle");
            TestSuite.Check(fight.CardsPlayedThisTurn == 2 && fight.Energy == 3, "automatic cards remain free next turn");
            while (fight.Hand.Count > 0) TestSuite.Check(fight.TryPlay(0) && fight.Energy == 3, "zero-cost manual cards are free");
            TestSuite.Check(hero.Deck.All(c => c.ManaCost == 9), "temporary rolls never modify deck cards");
            fight = new CombatState(Hero(1, false), new RollRandom(2));
            TestSuite.Check(fight.TryPlay(0) && fight.Energy == 1 && !fight.TryPlay(0), "manual play spends the rolled price, once");
            var enchanted = Hero(1, true);
            fight = new CombatState(enchanted, new RollRandom(3));
            TestSuite.Check(fight.Hand.All(c => fight.Cost(c) == 1.5m) && fight.TryPlay(0) && fight.Energy == 1.5m,
                "wieczny halves the temporary rolled price without losing its enchantment");
            TestSuite.Check(enchanted.Deck.All(c => c.ManaCost == 4.5m && c.WiecznyApplied && !c.Removable && !c.Sellable),
                "permanent enchanted card properties remain unchanged");
            for (int id = 0; id < 3; id += 2)
            {
                fight = new CombatState(Hero(id, false), new RollRandom(4));
                TestSuite.Check(fight.Hand.Count == 3 && fight.CardsPlayedThisTurn == 0 && fight.Cost(fight.Hand[0]) == 9,
                    "other energy heroes retain their original costs and draw");
            }
            fight = new CombatState(Hero(1, false), new RollRandom(4), EnemyKind.Mimicrator);
            for (int i = 0; i < 2; i++) { fight.EndPlayerTurn(); fight.ResolveEnemyTurn(); }
            TestSuite.Check(fight.IsStunned && fight.CardsPlayedThisTurn == 1 && fight.Hand.Count == 4 && fight.Energy == 3 && !fight.CanPlay(0),
                "stun limits free automatic plays to one without spending energy");
            var costs = new System.Collections.Generic.HashSet<decimal>();
            for (int seed = 0; seed < 50; seed++)
            {
                fight = new CombatState(Hero(1, false), new Random(seed));
                foreach (var card in fight.Hand) costs.Add(fight.Cost(card));
            }
            TestSuite.Check(costs.SetEquals(new decimal[] { 0, 1, 2, 3, 4 }), "normal RNG reaches all five integer costs");
        }
    }

    public static partial class UiTests
    {
        public static void TestIllyaCosts()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840); Call(game, "StartTestBattle");
                CombatState fight = null;
                for (int seed = 0; seed < 100; seed++)
                {
                    var hero = new HeroState(1, 50, 0, new Balance()); hero.Effects.Choose("polish"); StarterDeck.AddTo(hero);
                    fight = new CombatState(hero, new Random(seed));
                    if (fight.Hand.Any(c => fight.Cost(c) == 4) && fight.Hand.Any(c => fight.Cost(c) <= 3)) break;
                }
                Write(game, "selected", 1); Write(game, "currentHero", fight.Hero); Write(game, "combat", fight);
                var prices = fight.Hand.Select(c => fight.Cost(c)).ToArray();
                using (var image = Render(game)) image.Save(Path.Combine(TestSuite.OutputDirectory, "illya-confusion-costs.png"));
                TestSuite.Check(prices.SequenceEqual(fight.Hand.Select(c => fight.Cost(c))), "drawing live costs never rerolls cards");
                int index = fight.Hand.ToList().FindIndex(c => fight.Cost(c) <= 3);
                decimal price = fight.Cost(fight.Hand[index]);
                int discarded = fight.DiscardCount;
                Call(game, "PlayCombatCard", index); Call(game, "AdvanceCombatAnimation", 2f); Call(game, "AdvanceCombatAnimation", 2f);
                TestSuite.Check(fight.Energy == 3 - price && fight.DiscardCount == discarded + 1,
                    "animated manual card pays its displayed rolled price exactly once");
            }
        }
    }
}
