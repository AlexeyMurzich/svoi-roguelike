using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace ShpilSvoih
{
    public static class FomatreshkaTests
    {
        static CombatState Fight()
        {
            var hero = new HeroState(2, 300, 0, new Balance()); hero.Effects.Choose("feather");
            for (int i = 0; i < 6; i++) hero.AddCard(new CardData { Attack = true, Damage = 100, ManaCost = 1 });
            return new CombatState(hero, new Random(1), EnemyKind.Fomatreshka);
        }
        public static void Rules()
        {
            var fight = Fight(); var leader = fight.Enemies[0]; int health = fight.Hero.Health;
            TestSuite.Check(fight.Enemies.Count == 1 && leader.WillSummon, "leader starts alone with summon intent");
            fight.EndPlayerTurn(); fight.PrepareNextEnemyAction();
            TestSuite.Check(fight.Enemies.Count == 1 && !fight.PrepareNextEnemyAction(), "no early or duplicate summon at preparation");
            fight.ResolveNextEnemyImpact();
            TestSuite.Check(fight.Turn == 2 && fight.Enemies.Count == 4 && fight.Hero.Health == health, "first turn summons exactly three without attacks");
            TestSuite.Check(fight.Enemies.Skip(1).All(e => e.Name == "Фанатка" && e.Health == 16), "all minions share the exact requested name");
            for (int turn = 2; turn <= 8; turn++)
            {
                int before = fight.Hero.Health;
                fight.EndPlayerTurn();
                for (int i = 1; i <= 3; i++)
                {
                    TestSuite.Check(fight.ActingEnemy == i, "fanatics act before leader");
                    fight.ResolveNextEnemyAction();
                }
                TestSuite.Check(fight.ActingEnemy == 0, "leader acts after all three minions");
                bool drink = turn % 2 == 0;
                TestSuite.Check(leader.WillDrink == drink && leader.Vigor == (drink ? 0 : 6), "alternating drink/shot intent and stored vigor");
                fight.ResolveNextEnemyAction();
                TestSuite.Check(fight.Phase == CombatPhase.Player && fight.Turn == turn + 1 && fight.Hero.Health == before - 9 - (drink ? 0 : 24), "one full phase deals exact damage");
                TestSuite.Check(leader.Vigor == (drink ? 6 : 0), "vigor is gained by drink and consumed once by shot");
            }
            fight = Fight(); fight.TryPlay(0);
            TestSuite.Check(fight.Phase == CombatPhase.Victory && fight.Enemies.Count == 1, "killing before summon creates no minions");
            fight = Fight(); fight.EndPlayerTurn(); fight.ResolveEnemyTurn();
            fight.SelectTarget(2); fight.TryPlay(0);
            TestSuite.Check(!fight.Enemies[2].IsAlive && fight.Enemies[0].IsAlive && fight.Phase == CombatPhase.Player, "minion can die independently");
            fight.SelectTarget(0); fight.TryPlay(0);
            TestSuite.Check(fight.Phase == CombatPhase.Victory && fight.Enemies[1].Fled && fight.Enemies[3].Fled && !fight.Enemies[2].Fled, "leader death makes living minions flee without resurrecting dead one");
            TestSuite.Check(!fight.EndPlayerTurn() && !fight.SelectTarget(1), "fleeing minions cannot attack or be targeted");
            fight = Fight(); fight.EndPlayerTurn(); fight.ResolveEnemyTurn(); fight.EndPlayerTurn(); fight.ResolveEnemyTurn();
            fight.Hand[0].Attack = false; fight.Hand[0].Block = 100;
            fight.TryPlay(0); health = fight.Hero.Health;
            fight.EndPlayerTurn(); fight.ResolveEnemyTurn();
            TestSuite.Check(fight.Hero.Health == health && fight.Enemies[0].Vigor == 0, "fully blocked shot still consumes vigor");
            foreach (var kind in new[] { AttackAnimation.FomatreshkaSummon, AttackAnimation.FomatreshkaDrink, AttackAnimation.FomatreshkaShot, AttackAnimation.FanStrike })
                foreach (int hz in new[] { 10, 30, 60, 120 })
                {
                    int hits = 0; var timeline = new AttackTimeline(); timeline.Start(kind, () => hits++);
                    for (int i = 0; i < hz * 3; i++) timeline.Advance(1f / hz);
                    TestSuite.Check(hits == 1 && !timeline.IsRunning, "one action impact independent of frame rate");
                }
            int sightings = 0;
            for (int seed = 0; seed < 200; seed++) foreach (var node in ActMap.Generate(seed).Nodes)
                if (node.Kind == RoomKind.Elite)
                {
                    var kind = EnemyCatalog.ForEliteNode(seed, node.Id, node.District);
                    if (kind == EnemyKind.Fomatreshka) { sightings++; TestSuite.Check(node.District == 2, "estate-only elite"); }
                }
            TestSuite.Check(sightings > 0, "estate elite appears in generated maps");
        }
    }

    public static partial class UiTests
    {
        public static void TestFomatreshka()
        {
            foreach (var size in Sizes)
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = size; Call(game, "StartTestFomatreshka");
                var fight = Read<CombatState>(game, "combat");
                using (Render(game)) { }
                Call(game, "EndCombatTurn"); Call(game, "AdvanceCombatAnimation", 3f);
                TestSuite.Check(fight.Turn == 2 && fight.Enemies.Count == 4, "animated summon resolves once");
                Call(game, "AdvanceLivingAnimation", 1f);
                using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "fomatreshka-" + size.Width + "x" + size.Height + ".png"));
                float floor = (float)typeof(Game).GetProperty("CombatFloor", PrivateInstance).GetValue(game, null);
                for (int i = 1; i <= 3; i++)
                {
                    using (Render(game)) { }
                    Click(game, game.DrawingViewport.Width * (.48f + (i - 1) * .135f), floor - 130);
                    TestSuite.Check(fight.SelectedTarget == i, "every fan has an independent clickable target");
                }
                Call(game, "EndCombatTurn"); Call(game, "AdvanceCombatAnimation", 6f);
                TestSuite.Check(fight.Turn == 3 && fight.Enemies[0].Vigor == 6, "drink action and three attacks animate correctly");
                Call(game, "SetInventory", "pause"); double clock = Read<double>(game, "livingSeconds");
                Call(game, "AdvanceLivingAnimation", 1f); Call(game, "AdvanceCombatAnimation", 1f);
                TestSuite.Check(Read<double>(game, "livingSeconds") == clock && fight.Enemies[0].Vigor == 6, "pause freezes glow and battle");
                Call(game, "SetInventory", "");
                using (var frame = Render(game)) if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "fomatreshka-vigor.png"));
                Call(game, "EndCombatTurn"); Call(game, "AdvanceCombatAnimation", 6f);
                TestSuite.Check(fight.Turn == 4 && fight.Enemies[0].Vigor == 0, "shot consumes vigor once");
                fight.Hand[0].Attack = true; fight.Hand[0].Damage = 100;
                fight.SelectTarget(0); Call(game, "PlayCombatCard", 0); Call(game, "AdvanceCombatAnimation", .45f);
                TestSuite.Check(fight.Phase == CombatPhase.Victory && fight.Enemies.Skip(1).All(e => e.Fled), "lethal animation impact triggers immediate flight");
                TestSuite.Check(Read<float>(game, "fomatreshkaFlee") >= 0, "flight has its own clock");
                using (var frame = Render(game)) if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "fomatreshka-flee.png"));
                Call(game, "AdvanceCombatAnimation", 2f);
                TestSuite.Check(Read<float>(game, "fomatreshkaFlee") == FomatreshkaRules.FleeDuration && !Read<DeathVisuals>(game, "deathVisuals").IsRunning, "flight and death finish before victory controls unlock");
                Call(game, "ReturnToMainMenu");
                TestSuite.Check(Read<float>(game, "fomatreshkaFlee") == -1, "scene exit clears escape animation");
            }
        }
    }
}
