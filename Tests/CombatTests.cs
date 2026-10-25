using System;
using System.Linq;

namespace ShpilSvoih
{
    public static class CombatTests
    {
        static HeroState Hero(int hero, string relic)
        {
            var state = new HeroState(hero, 50, 0, new Balance());
            state.Effects.Choose(relic);
            StarterDeck.AddTo(state);
            return state;
        }

        public static void Rules()
        {
            for (int seed = 0; seed < 80; seed++)
            {
                var hero = Hero(3, "ring");
                var fight = new CombatState(hero, new Random(seed));
                TestSuite.Check(hero.Deck.Count == 6 && hero.Deck.Count(c => c.Attack) == 3 && hero.Deck.Count(c => c.Skill) == 3, "shared six-card starter deck");
                TestSuite.Check(fight.Hand.Count == 3 && fight.DrawCount == 3 && fight.DiscardCount == 0 && fight.Hand.Distinct().Count() == 3, "three unique cards drawn from shuffled deck");
                TestSuite.Check(hero.Money == 299, "Shuten loses one gold at turn start");
                for (int turn = 0; turn < 3; turn++)
                {
                    int before = hero.Health;
                    TestSuite.Check(fight.EndPlayerTurn() && !fight.EndPlayerTurn(), "enemy turn starts once");
                    TestSuite.Check(fight.ResolveEnemyTurn() && !fight.ResolveEnemyTurn(), "enemy resolves once");
                    TestSuite.Check(hero.Health == before - 5, "rat deals exactly five without block");
                    TestSuite.Check(fight.Hand.Count == 3 && fight.Hand.Count + fight.DrawCount + fight.DiscardCount == 6, "draw and reshuffle conserve all six cards");
                }

                TestSuite.Check(fight.Shuffles > 0, "discard recycled when draw pile runs out");
            }

            for (int seed = 0; seed < 50; seed++)
            {
                var hero = Hero(3, "ring");
                var fight = new CombatState(hero, new Random(seed));
                int block = fight.Hand.ToList().FindIndex(c => c.Skill);
                if (block < 0)
                {
                    fight.EndPlayerTurn();
                    fight.ResolveEnemyTurn();
                    block = fight.Hand.ToList().FindIndex(c => c.Skill);
                }

                var used = fight.Hand[block];
                decimal money = hero.Money;
                int discardedBefore = fight.DiscardCount;
                TestSuite.Check(fight.TryPlay(block) && !fight.Hand.Contains(used) && fight.DiscardCount == discardedBefore + 1 && hero.Money == money - 10, "playing consumes exact card and ten gold");
                int hp = hero.Health;
                fight.EndPlayerTurn();
                fight.ResolveEnemyTurn();
                TestSuite.Check(hero.Health == hp && fight.Block == 0, "five block prevents five damage and expires");
            }

            var poor = Hero(3, "coupon");
            var poorFight = new CombatState(poor, new Random(3));
            TestSuite.Check(!poorFight.TryPlay(0) && poorFight.Hand.Count == 3, "unaffordable cards cannot be played");
            poor.ChangeMoney(20);
            TestSuite.Check(poorFight.TryPlay(0) && poor.Money == 11, "coupon charges nine gold");
            var debt = Hero(3, "phone");
            var debtFight = new CombatState(debt, new Random(5));
            int debtHp = debt.Health;
            TestSuite.Check(debtFight.TryPlay(0) && debt.Health == debtHp - 10 && debt.Money == -11, "phone permits debt and exact HP cost");
            var shield = Hero(0, "shield");
            var shieldFight = new CombatState(shield, new Random(2));
            shieldFight.EndPlayerTurn();
            shieldFight.ResolveEnemyTurn();
            TestSuite.Check(shield.Health == 50, "first-turn relic cancels rat attack");
            shieldFight.EndPlayerTurn();
            shieldFight.ResolveEnemyTurn();
            TestSuite.Check(shield.Health == 45 && shield.Tears == 5, "later damage creates tears");
            var zalt = new CombatState(Hero(0, "zalt"), new Random(4));
            TestSuite.Check(zalt.Hand.Count == 4 && zalt.Hand.Any(c => c.Name == "Зальтраак" && c.ManaCost == 0), "combat-start special card retained");
            var illya = new CombatState(Hero(1, "polish"), new Random(4));
            TestSuite.Check(illya.Hand.Count == 3 && illya.DiscardCount == 2 && illya.DrawCount == 1, "Illya draws two extra and automatically plays two");
            var victim = Hero(2, "feather");
            var dying = new CombatState(victim, new Random(7));
            for (int i = 0; i < 10; i++)
            {
                dying.EndPlayerTurn();
                dying.ResolveEnemyTurn();
            }

            TestSuite.Check(victim.Health == 0 && dying.Phase == CombatPhase.Defeat && !dying.TryPlay(0) && !dying.EndPlayerTurn(), "defeat stops play and turn advancement");
            var winner = new CombatState(Hero(3, "ring"), new Random(8));
            for (int steps = 0; steps < 50 && winner.Phase == CombatPhase.Player; steps++)
            {
                int attack = winner.Hand.ToList().FindIndex(c => c.Attack);
                if (attack >= 0)
                    winner.TryPlay(attack);
                else
                {
                    winner.EndPlayerTurn();
                    winner.ResolveEnemyTurn();
                }
            }

            TestSuite.Check(winner.Phase == CombatPhase.Victory && winner.EnemyHealth == 0 && !winner.EndPlayerTurn(), "victory blocks retaliatory attack");
        }
    }
}
