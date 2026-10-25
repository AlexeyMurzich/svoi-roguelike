using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static class HedgehogPairTests
    {
        static HeroState Hero(int damage, int block)
        {
            var hero = new HeroState(2, 100, 0, new Balance()); hero.Effects.Choose("feather");
            for (int i = 0; i < 3; i++) hero.AddCard(new CardData { Attack = damage > 0, Skill = block > 0, Damage = damage, Block = block, ManaCost = 1 });
            return hero;
        }
        public static void Rules()
        {
            var hero = Hero(6, 0);
            var fight = new CombatState(hero, new Random(1), EnemyKind.Hedgehogs);
            TestSuite.Check(fight.Enemies.Count == 2 && fight.Enemies[0].Kind == EnemyKind.Hedgehog && fight.Enemies[1].Kind == EnemyKind.RabidHedgehog, "paired encounter contains both distinct hedgehogs");
            TestSuite.Check(fight.SelectTarget(1) && fight.TryPlay(0), "first card can hit rabid hedgehog");
            TestSuite.Check(fight.Enemies[1].Health == 24 && fight.Enemies[0].Health == 30 && hero.Health == 100, "only chosen rabid enemy takes first hit, no foreign thorns");
            TestSuite.Check(fight.SelectTarget(0) && fight.TryPlay(0), "target can change before second card in same turn");
            TestSuite.Check(fight.Turn == 1 && fight.Enemies[0].Health == 24 && fight.Enemies[1].Health == 24 && hero.Health == 98, "both enemies can be hit in one turn, only spiked target retaliates");
            TestSuite.Check(fight.SelectTarget(1) && fight.TryPlay(0) && fight.Enemies[1].Health == 18, "third attack can switch back in the same turn");
            foreach (int block in new[] { 12, 15 })
            {
                hero = Hero(0, block); fight = new CombatState(hero, new Random(1), EnemyKind.Hedgehogs);
                fight.TryPlay(0); fight.EndPlayerTurn();
                TestSuite.Check(!fight.SelectTarget(1) && fight.ActingEnemy == 0, "target locked during enemy phase");
                fight.ResolveNextEnemyAction();
                TestSuite.Check(hero.Health == 100 && fight.Block == block - 10 && fight.ActingEnemy == 1 && fight.Turn == 1, "needle attack consumes its share of block without starting next turn");
                fight.ResolveNextEnemyAction();
                TestSuite.Check(fight.Turn == 2 && hero.Health == (block == 12 ? 97 : 100) && fight.Rabies == (block == 12), "second hedgehog bites remaining block and applies rabies only on health loss");
            }
            hero = Hero(30, 0); fight = new CombatState(hero, new Random(1), EnemyKind.Hedgehogs);
            fight.TryPlay(0);
            TestSuite.Check(fight.Enemies[0].Health == 0 && fight.SelectedTarget == 1 && fight.Phase == CombatPhase.Player && hero.Health == 98, "first death retargets but does not grant victory");
            TestSuite.Check(!fight.SelectTarget(0) && !fight.SelectTarget(-1) && !fight.SelectTarget(2), "invalid and dead targets rejected");
            fight.EndPlayerTurn();
            TestSuite.Check(fight.ActingEnemy == 1, "dead spiked hedgehog is skipped in queue");
            fight.ResolveEnemyTurn();
            TestSuite.Check(hero.Health == 93 && fight.Rabies, "surviving rabid hedgehog still acts alone");
            fight.TryPlay(0);
            TestSuite.Check(fight.Phase == CombatPhase.Victory && fight.Enemies.All(e => !e.IsAlive) && hero.Health == 93, "only second death grants victory, dead thorns never transfer");
        }
    }

    public static partial class UiTests
    {
        public static void TestHedgehogPairUi()
        {
            foreach (var size in Sizes)
                using (var game = new Game())
                {
                    game.CreateControl(); game.ClientSize = size; Call(game, "StartTestHedgehogs");
                    var original = Read<CombatState>(game, "combat");
                    TestSuite.Check(original.Enemy == EnemyKind.Hedgehogs && original.Enemies.Count == 2, "quick launch opens paired encounter");
                    var hero = new HeroState(3, 50, 0, new Balance { BaseDraw = 6 }); hero.Effects.Choose("ring"); StarterDeck.AddTo(hero);
                    var fight = ReplaceFight(game, hero, EnemyKind.Hedgehogs);
                    using (Render(game)) { }
                    float floor = Math.Min(game.DrawingViewport.Height - 295, (160 + game.DrawingViewport.Height - 285) / 2 + 160);
                    Click(game, game.DrawingViewport.Width * .84f, floor - 100);
                    TestSuite.Check(fight.SelectedTarget == 1, "clicking rabid model chooses target");
                    int attack = fight.Hand.ToList().FindIndex(c => c.Attack);
                    Key(game, (Keys)((int)Keys.D1 + attack));
                    Click(game, game.DrawingViewport.Width * .62f, floor - 100);
                    TestSuite.Check(fight.SelectedTarget == 1, "cannot redirect an already committed attack");
                    Call(game, "AdvanceCombatAnimation", 1f);
                    TestSuite.Check(fight.Enemies[1].Health == 24 && fight.Enemies[0].Health == 30 && hero.Health == 50, "first real card hits selected target once");
                    using (Render(game)) { }
                    Click(game, game.DrawingViewport.Width * .62f, floor + 18);
                    TestSuite.Check(fight.SelectedTarget == 0, "can choose other hedgehog immediately after first card");
                    using (var frame = Render(game)) if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "hedgehogs-select-target.png"));
                    attack = fight.Hand.ToList().FindIndex(c => c.Attack);
                    Key(game, (Keys)((int)Keys.D1 + attack)); Call(game, "AdvanceCombatAnimation", 1f);
                    TestSuite.Check(fight.Turn == 1 && fight.Enemies.All(e => e.Health == 24) && hero.Health == 48 && hero.Money == 279, "two cards in one turn can hit both enemies with separate costs and thorns");
                    Call(game, "EndCombatTurn"); Call(game, "AdvanceCombatAnimation", 3f);
                    TestSuite.Check(fight.Turn == 2 && hero.Health == 33 && fight.Rabies, "both different animated attacks resolve in one phase");
                    using (var frame = Render(game)) if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "hedgehogs-pair-after-turn.png"));
                    Call(game, "ReturnToMainMenu");
                }
        }
    }
}
