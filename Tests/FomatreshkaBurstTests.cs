using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace ShpilSvoih
{
    public static class FomatreshkaBurstTests
    {
        static CombatState Ready(int block)
        {
            var hero = new HeroState(2, 300, 0, new Balance()); hero.Effects.Choose("feather");
            for (int i = 0; i < 6; i++) hero.AddCard(new CardData { Block = block > 0 ? block + 9 : 0, ManaCost = 0 });
            var fight = new CombatState(hero, new Random(3), EnemyKind.Fomatreshka);
            fight.EndPlayerTurn(); fight.ResolveEnemyTurn(); fight.EndPlayerTurn(); fight.ResolveEnemyTurn();
            if (block > 0) fight.TryPlay(0);
            fight.EndPlayerTurn();
            for (int i = 0; i < 3; i++) fight.ResolveNextEnemyAction();
            return fight;
        }
        public static void Rules()
        {
            foreach (int block in new[] { 0, 5, 10, 24, 30 })
            {
                var fight = Ready(block); var actor = fight.Enemies[0]; int start = fight.Hero.Health;
                int remaining = fight.Block;
                TestSuite.Check(remaining == block, "minions leave the intended shared block pool");
                TestSuite.Check(actor.HitCount == 3 && actor.Vigor == 6 && actor.AttackDamage == 8, "intent is three boosted hits, not one total");
                TestSuite.Check(CombatHints.Intent(actor).Single().Number == "8 × 3", "multihit intent shows per-hit damage and count");
                fight.PrepareNextEnemyAction();
                for (int shot = 1; shot <= 3; shot++)
                {
                    int before = fight.Hero.Health, pool = fight.Block;
                    fight.ResolveNextEnemyImpact();
                    TestSuite.Check(before - fight.Hero.Health == Math.Max(0, 8 - pool), "each bullet consumes shared block before HP");
                    TestSuite.Check(actor.Vigor == (shot < 3 ? 6 : 0), "vigor survives early bullets and expires only after burst");
                    TestSuite.Check(shot == 3 ? fight.Phase == CombatPhase.Player : fight.Phase == CombatPhase.Enemy && fight.ActingEnemy == 0, "no early player turn between bullets");
                }
                TestSuite.Check(fight.Hero.Health == start - Math.Max(0, 24 - remaining), "burst total agrees with sequential shared block");
                TestSuite.Check(!fight.ResolveNextEnemyImpact(), "no fourth bullet");
            }
            var lethal = Ready(0);
            lethal.Hero.ReceiveDamage(lethal.Hero.Health - 12);
            lethal.ResolveNextEnemyImpact();
            TestSuite.Check(lethal.Hero.Health == 4 && lethal.Phase == CombatPhase.Enemy, "first bullet is not an upfront aggregate hit");
            lethal.ResolveNextEnemyImpact();
            TestSuite.Check(lethal.Phase == CombatPhase.Defeat && lethal.Hero.Health == 0 && lethal.Enemies[0].Vigor == 0 && !lethal.ResolveNextEnemyImpact(), "death on second bullet cancels remaining damage and consumes buff");
        }
    }

    public static partial class UiTests
    {
        public static void TestFomatreshkaBurst()
        {
            foreach (int hz in new[] { 10, 30, 60, 120 })
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840); Call(game, "StartTestFomatreshka");
                var fight = Read<CombatState>(game, "combat");
                fight.EndPlayerTurn(); fight.ResolveEnemyTurn(); fight.EndPlayerTurn(); fight.ResolveEnemyTurn();
                using (Render(game)) { }
                TestSuite.Check(Read<System.Collections.Generic.List<Tuple<RectangleF, CombatHint>>>(game, "combatHintAreas").Any(x => x.Item2.Icon == 21 && x.Item2.Title == "Бодрость"), "actual buff has its own hoverable icon");
                fight.EndPlayerTurn(); for (int i = 0; i < 3; i++) fight.ResolveNextEnemyAction();
                int before = fight.Hero.Health, hits = 0, previous = before;
                Call(game, "StartNextEnemyAnimation");
                for (int frame = 0; frame < hz * 3 && (fight.Phase == CombatPhase.Enemy || Read<AttackTimeline>(game, "attackTimeline").IsRunning); frame++)
                {
                    Call(game, "AdvanceCombatAnimation", 1f / hz);
                    if (fight.Hero.Health != previous)
                    {
                        TestSuite.Check(previous - fight.Hero.Health == 8, "native timeline exposes each HP tick at " + hz + " Hz");
                        hits++; previous = fight.Hero.Health;
                        if (hits == 1)
                        {
                            float time = Read<AttackTimeline>(game, "attackTimeline").Time;
                            Call(game, "SetInventory", "pause"); Call(game, "AdvanceCombatAnimation", 2f);
                            TestSuite.Check(fight.Hero.Health == previous && Read<AttackTimeline>(game, "attackTimeline").Time == time && fight.Enemies[0].Vigor == 6, "pause between bullets freezes damage and keeps vigor");
                            Call(game, "SetInventory", "");
                        }
                    }
                }
                TestSuite.Check(hits == 3 && fight.Hero.Health == before - 24 && fight.Turn == 4 && fight.Enemies[0].Vigor == 0, "three visible hits at every tested frame rate");
                using (var frame = Render(game)) if (hz == 60) frame.Save(Path.Combine(TestSuite.OutputDirectory, "fomatreshka-burst-complete.png"));
            }
        }
    }
}
