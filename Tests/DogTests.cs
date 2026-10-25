using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static class DogTests
    {
        public static void Rules()
        {
            int dogs = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                var kind = EnemyCatalog.ForNode(seed, 1);
                TestSuite.Check(kind == EnemyCatalog.ForNode(seed, 1), "enemy selection is stable for map node");
                if (kind == EnemyKind.StrayDogs) dogs++;
                var hero = new HeroState(2, 200, 0, new Balance()); StarterDeck.AddTo(hero);
                var fight = new CombatState(hero, new Random(seed), EnemyKind.StrayDogs);
                var attack = hero.Deck.First(c => c.Attack);
                TestSuite.Check(fight.Enemies.Count == 2 && fight.Enemies.All(e => e.Health == 30), "two independent thirty-hp dogs");
                for (int turn = 1; turn <= 9; turn++)
                {
                    bool firstHowl = turn % 3 == 0, secondHowl = turn % 3 == 2;
                    TestSuite.Check(fight.Enemies[0].WillHowl == firstHowl && fight.Enemies[1].WillHowl == secondHowl, "individual cycles are staggered by one action");
                    int hp = hero.Health;
                    fight.EndPlayerTurn();
                    TestSuite.Check(fight.ActingEnemy == 0 && !fight.SelectTarget(1), "enemy queue starts with dog one and locks targeting");
                    fight.ResolveNextEnemyAction();
                    TestSuite.Check(fight.Phase == CombatPhase.Enemy && fight.ActingEnemy == 1 && fight.Turn == turn, "first action does not draw next hand");
                    fight.ResolveNextEnemyAction();
                    bool howled = firstHowl || secondHowl;
                    TestSuite.Check(hero.Health == hp - (howled ? 5 : 10), "each living dog resolves its own bite or howl");
                    TestSuite.Check(fight.WeakTurns == (howled ? 1 : 0), "weakness refreshes for next player turn only");
                    TestSuite.Check(fight.DamageFor(attack) == (howled ? 4 : 6), "weakness reduces attacks by twenty-five percent");
                    TestSuite.Check(fight.Hand.Count == 3 && fight.Hand.Count + fight.DrawCount + fight.DiscardCount == 6, "two actions draw only one new hand");
                    TestSuite.Check(!fight.ResolveEnemyTurn() && !fight.ResolveNextEnemyAction(), "queue cannot resolve twice");
                }
            }
            TestSuite.Check(dogs > 25 && dogs < 80, "both enemy encounters appear across seeds");
            var guarded = new HeroState(2, 50, 0, new Balance());
            guarded.AddCard(new CardData { Block = 7, Skill = true, ManaCost = 0 });
            var shieldFight = new CombatState(guarded, new Random(1), EnemyKind.StrayDogs);
            shieldFight.TryPlay(0); shieldFight.EndPlayerTurn(); shieldFight.ResolveNextEnemyAction();
            TestSuite.Check(guarded.Health == 50 && shieldFight.Block == 2, "first bite consumes five of seven block");
            shieldFight.ResolveNextEnemyAction();
            TestSuite.Check(guarded.Health == 47 && shieldFight.Block == 0, "second bite consumes remaining block before health");
            var slayer = new HeroState(2, 50, 0, new Balance());
            for (int i = 0; i < 3; i++) slayer.AddCard(new CardData { Attack = true, Damage = 30, ManaCost = 0 });
            var battle = new CombatState(slayer, new Random(2), EnemyKind.StrayDogs);
            TestSuite.Check(!battle.SelectTarget(-1) && !battle.SelectTarget(2) && battle.SelectTarget(1), "target range validated");
            battle.TryPlay(0);
            TestSuite.Check(battle.Enemies[1].Health == 0 && battle.Enemies[0].Health == 30 && battle.SelectedTarget == 0 && battle.Phase == CombatPhase.Player, "killing chosen dog retargets without granting victory");
            TestSuite.Check(!battle.SelectTarget(1), "dead dogs cannot be selected");
            battle.EndPlayerTurn(); battle.ResolveEnemyTurn();
            TestSuite.Check(slayer.Health == 45 && battle.Enemies[1].CyclePosition == 1, "dead dog never acts or advances cycle");
            battle.TryPlay(0);
            TestSuite.Check(battle.Phase == CombatPhase.Victory && !battle.EndPlayerTurn(), "only both deaths grant victory");
            var fragile = new HeroState(0, 4, 0, new Balance());
            var fatal = new CombatState(fragile, new Random(1), EnemyKind.StrayDogs);
            fatal.EndPlayerTurn(); fatal.ResolveEnemyTurn();
            TestSuite.Check(fatal.Phase == CombatPhase.Defeat && fatal.Enemies[1].CyclePosition == 1 && fragile.Tears == 4, "hero death cancels remaining enemy action");
        }
    }

    public static partial class UiTests
    {
        static void FinishEnemyAnimations(Game game)
        {
            for (int i = 0; i < 8 && (Read<AttackTimeline>(game, "attackTimeline").IsRunning || Read<DeathVisuals>(game, "deathVisuals").IsRunning); i++)
                Call(game, "AdvanceCombatAnimation", 1f);
        }
        public static void TestDogUi()
        {
            foreach (var size in Sizes)
                using (var game = new Game())
                {
                    game.CreateControl(); game.ClientSize = size; Call(game, "StartTestDogs");
                    var battle = Read<CombatState>(game, "combat");
                    TestSuite.Check(battle.Enemies.Count == 2 && battle.Hero.Money == 299, "dog test starts two independent enemies");
                    using (Render(game)) { }
                    float height = game.DrawingViewport.Height;
                    float floor = Math.Min(height - 295, (160 + height - 285) / 2f + 160);
                    Click(game, game.DrawingViewport.Width * .84f, floor - 100);
                    TestSuite.Check(battle.SelectedTarget == 1, "clicking second dog selects it at " + size);
                    Call(game, "EndCombatTurn"); Call(game, "AdvanceCombatAnimation", .34f);
                    TestSuite.Check(battle.Hero.Health == 50, "bite does not hit before jaws close");
                    using (var frame = Render(game)) if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "dog-bite.png"));
                    Call(game, "AdvanceCombatAnimation", .11f);
                    TestSuite.Check(battle.Hero.Health == 45 && battle.ActingEnemy == 1, "first bite applies once and queues second dog");
                    Call(game, "AdvanceCombatAnimation", .44f);
                    TestSuite.Check(Read<AttackTimeline>(game, "attackTimeline").IsRunning && Read<int>(game, "actingEnemyIndex") == 1, "second dog starts after first animation finishes");
                    Key(game, Keys.Escape); Call(game, "AdvanceCombatAnimation", 1f);
                    TestSuite.Check(battle.Hero.Health == 45 && battle.Turn == 1, "pause freezes the second queued action");
                    Key(game, Keys.Escape); FinishEnemyAnimations(game);
                    TestSuite.Check(battle.Hero.Health == 40 && battle.Turn == 2, "two bites finish one enemy phase");
                    Call(game, "EndCombatTurn"); Call(game, "AdvanceCombatAnimation", 1f); Call(game, "AdvanceCombatAnimation", .3f);
                    Key(game, Keys.Escape); Call(game, "AdvanceCombatAnimation", 1f);
                    TestSuite.Check(battle.WeakTurns == 0, "pause freezes howl before debuff");
                    Key(game, Keys.Escape); Call(game, "AdvanceCombatAnimation", .25f);
                    TestSuite.Check(battle.WeakTurns == 1 && battle.Hero.Health == 35, "second dog howls on its third individual action");
                    using (var frame = Render(game)) if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "dogs-howl.png"));
                    FinishEnemyAnimations(game);
                    Call(game, "ReturnToMainMenu");
                    TestSuite.Check(Read<CombatState>(game, "combat") == null, "leaving dogs cancels queue");
                    Call(game, "StartTestBattle");
                    TestSuite.Check(Read<CombatState>(game, "combat").WeakTurns == 0, "weakness never leaks between fights");
                }
        }
    }
}
