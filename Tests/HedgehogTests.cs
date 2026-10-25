using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static class HedgehogTests
    {
        static HeroState Hero(int health, int block, int damage)
        {
            var hero = new HeroState(2, health, 0, new Balance()); hero.Effects.Choose("feather");
            for (int i = 0; i < 3; i++) hero.AddCard(new CardData { Attack = damage > 0, Skill = block > 0, Damage = damage, Block = block, ManaCost = 0 });
            return hero;
        }
        public static void Rules()
        {
            var kinds = new System.Collections.Generic.HashSet<EnemyKind>();
            for (int seed = 0; seed < 1000; seed++) kinds.Add(EnemyCatalog.ForNode(seed, 1));
            TestSuite.Check(kinds.SetEquals(new[] { EnemyKind.Rat, EnemyKind.StrayDogs, EnemyKind.Hedgehogs }), "rat, dogs and paired hedgehogs are generated");
            foreach (int block in new[] { 0, 4, 5, 9 })
            {
                var hero = Hero(50, block, 0);
                var fight = new CombatState(hero, new Random(1), EnemyKind.RabidHedgehog);
                if (block > 0) fight.TryPlay(0);
                fight.EndPlayerTurn(); fight.ResolveEnemyTurn();
                bool pierced = block < 5;
                TestSuite.Check(hero.Health == 50 - Math.Max(0, 5 - block) && fight.Rabies == pierced, "rabies requires real health loss through block " + block);
                TestSuite.Check(fight.DamageFor(new CardData { Attack = true, Damage = 6 }) == (pierced ? 8 : 6), "rabies gives exactly two attack strength");
                TestSuite.Check(fight.BlockFor(new CardData { Block = 5 }) == (pierced ? 3 : 5), "rabies reduces generated block by two");
                TestSuite.Check(fight.BlockFor(new CardData { Block = 1 }) == (pierced ? 0 : 1) && fight.BlockFor(new CardData()) == 0, "dexterity never creates negative block");
                if (pierced)
                {
                    fight.EndPlayerTurn(); fight.ResolveEnemyTurn();
                    TestSuite.Check(fight.Strength == 2 && fight.Dexterity == -2 && fight.Rabies, "rabies persists without repeatedly stacking");
                    TestSuite.Check(fight.TryPlay(0) && fight.Block == Math.Max(0, block - 2), "actual defense uses combat dexterity");
                }
                var next = new CombatState(hero, new Random(1));
                TestSuite.Check(!next.Rabies && next.Strength == 0 && next.Dexterity == 0, "rabies resets between fights");
            }
            var spikedHero = Hero(50, 0, 6);
            var spiked = new CombatState(spikedHero, new Random(1), EnemyKind.Hedgehog);
            spiked.TryPlay(0);
            TestSuite.Check(spiked.EnemyHealth == 24 && spikedHero.Health == 48 && spiked.LastRetaliation == 2, "each attacking card triggers two thorns damage");
            spiked.TryPlay(0);
            TestSuite.Check(spikedHero.Health == 46, "thorns trigger again on next attack");
            spiked.EndPlayerTurn(); spiked.ResolveEnemyTurn();
            TestSuite.Check(spikedHero.Health == 36 && spiked.Enemies[0].AttackDamage == 10, "needle attack deals twice the five-damage bite");
            var protectedHero = Hero(50, 5, 0);
            protectedHero.AddCard(new CardData { Attack = true, Damage = 6, ManaCost = 0 });
            protectedHero.Settings.BaseDraw = 4;
            var protectedFight = new CombatState(protectedHero, new Random(1), EnemyKind.Hedgehog);
            for (int i = protectedFight.Hand.Count - 1; i >= 0; i--) if (!protectedFight.Hand[i].Attack) protectedFight.TryPlay(i);
            int attack = protectedFight.Hand.ToList().FindIndex(c => c.Attack);
            if (attack < 0) { protectedFight.EndPlayerTurn(); protectedFight.ResolveEnemyTurn(); attack = protectedFight.Hand.ToList().FindIndex(c => c.Attack); }
            protectedFight.TryPlay(attack);
            TestSuite.Check(protectedHero.Health == 50, "existing block can absorb thorn retaliation");
            var doomed = Hero(2, 0, 30);
            var trade = new CombatState(doomed, new Random(1), EnemyKind.Hedgehog); trade.TryPlay(0);
            TestSuite.Check(trade.Enemies[0].Health == 0 && doomed.Health == 0 && trade.Phase == CombatPhase.Defeat, "lethal blow still triggers thorns and mutual death is defeat");
            TestSuite.Check(!trade.EndPlayerTurn() && !trade.TryPlay(0), "death cannot queue another action");
        }
    }

    public static partial class UiTests
    {
        public static void TestHedgehogUi()
        {
            foreach (bool rabid in new[] { false, true })
                foreach (var size in Sizes)
                    using (var game = new Game())
                    {
                        game.CreateControl(); game.ClientSize = size;
                        Call(game, rabid ? "StartTestRabidHedgehog" : "StartTestHedgehog");
                        var singleHero = new HeroState(3, 50, 0, new Balance()); singleHero.Effects.Choose("ring"); StarterDeck.AddTo(singleHero);
                        var fight = ReplaceFight(game, singleHero, rabid ? EnemyKind.RabidHedgehog : EnemyKind.Hedgehog);
                        var field = Field(rabid ? "rabidHedgehogPoses" : "hedgehogPoses");
                        var atlas = field.GetValue(game);
                        int components = (int)atlas.GetType().GetProperty("LargeComponents").GetValue(atlas, null);
                        TestSuite.Check(components == 6, "six complete separated silhouettes in hedgehog atlas; got " + components);
                        using (var frame = Render(game)) if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, rabid ? "rabid-hedgehog-idle.png" : "hedgehog-idle.png"));
                        Call(game, "EndCombatTurn"); Call(game, "AdvanceCombatAnimation", .15f);
                        float paused = Read<AttackTimeline>(game, "attackTimeline").Time;
                        Key(game, Keys.Escape); Call(game, "AdvanceCombatAnimation", 1f);
                        TestSuite.Check(fight.Hero.Health == 50 && Read<AttackTimeline>(game, "attackTimeline").Time == paused, "pause freezes new hedgehog attacks");
                        Key(game, Keys.Escape);
                        Call(game, "AdvanceCombatAnimation", rabid ? .32f : .68f);
                        using (var frame = Render(game)) if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, rabid ? "rabid-hedgehog-bite.png" : "hedgehog-needles.png"));
                        Call(game, "AdvanceCombatAnimation", 2f);
                        TestSuite.Check(fight.Turn == 2 && fight.Hero.Health == (rabid ? 45 : 40) && fight.Rabies == rabid, "one hedgehog impact and correct effect");
                        if (size == Sizes[2])
                        {
                            var killer = new HeroState(2, 50, 0, new Balance()); killer.Effects.Choose("feather");
                            killer.AddCard(new CardData { Attack = true, Damage = 30, ManaCost = 0 });
                            fight = ReplaceFight(game, killer, rabid ? EnemyKind.RabidHedgehog : EnemyKind.Hedgehog);
                            Call(game, "PlayCombatCard", 0); Call(game, "AdvanceCombatAnimation", .5f);
                            var deaths = Read<DeathVisuals>(game, "deathVisuals");
                            TestSuite.Check(fight.Phase == CombatPhase.Victory && deaths.IsRunning && deaths.SnapshotCount == 1, "hedgehog death captured on lethal impact");
                            Call(game, "AdvanceCombatAnimation", 1f);
                            TestSuite.Check(!deaths.IsRunning && deaths.SnapshotCount == 0, "hedgehog death releases snapshot");
                        }
                        Call(game, "ReturnToMainMenu");
                        TestSuite.Check(Read<CombatState>(game, "combat") == null, "new encounter resets cleanly");
                    }
        }
    }
}
