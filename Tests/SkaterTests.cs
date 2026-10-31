using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ShpilSvoih
{
    public static class SkaterTests
    {
        static HeroState Hero()
        {
            var hero = new HeroState(0, 100, 0, new Balance()); hero.Effects.Choose("exchange"); StarterDeck.AddTo(hero); return hero;
        }
        public static void Combat()
        {
            var h = Hero(); var fight = new CombatState(h, new Random(1), EnemyKind.SkaterMaster);
            TestSuite.Check(fight.Enemies.Count == 3 && fight.Enemies[0].Health == 80 && fight.Enemies[1].Health == 20 && fight.Enemies[2].Health == 20, "one master and exactly two independent padawans");
            TestSuite.Check(fight.Enemies[0].Name == "Магистр скейтер" && fight.Enemies[1].Name == "Падаван скейтер", "approved names");
            fight.EndPlayerTurn(); fight.ResolveNextEnemyAction();
            TestSuite.Check(h.Health == 88 && fight.ActingEnemy == 1, "one saber impact removes twelve HP");
            fight.ResolveNextEnemyAction(); TestSuite.Check(h.Health == 84 && fight.ActingEnemy == 2, "first independent blaster impact");
            fight.ResolveNextEnemyAction(); TestSuite.Check(h.Health == 80 && fight.Turn == 2 && fight.Phase == CombatPhase.Player, "second blaster and next turn");
            TestSuite.Check(!fight.ResolveNextEnemyImpact() && h.Health == 80, "no extra impact outside enemy turn");
            h = Hero(); foreach (var card in h.Deck) { card.Attack = false; card.Skill = true; card.Block = 15; card.ManaCost = 0; }
            fight = new CombatState(h, new Random(1), EnemyKind.SkaterMaster); fight.TryPlay(0); fight.EndPlayerTurn(); fight.ResolveEnemyTurn();
            TestSuite.Check(h.Health == 95, "three hits consume shared block once in order");
            h = Hero(); foreach (var card in h.Deck) { card.Attack = true; card.Skill = false; card.Damage = 1000; card.ManaCost = 0; }
            fight = new CombatState(h, new Random(1), EnemyKind.SkaterMaster); fight.TryPlay(0);
            TestSuite.Check(!fight.Enemies[0].IsAlive && fight.Enemies[1].IsAlive && fight.Enemies[2].IsAlive && fight.Phase == CombatPhase.Player, "padawans stay after master death");
            fight.EndPlayerTurn(); fight.ResolveEnemyTurn(); TestSuite.Check(h.Health == 92, "dead master cannot attack, living padawans still attack");
            fight.SelectTarget(2); fight.TryPlay(0); TestSuite.Check(fight.SelectedTarget == 1, "target switches to surviving padawan");
            fight.TryPlay(0); TestSuite.Check(fight.Phase == CombatPhase.Victory, "victory requires all three targets");
        }
        public static void HardPool()
        {
            var first = new HashSet<EnemyKind>();
            for (int seed = 0; seed < 300; seed++)
            {
                var history = new List<EnemyKind>();
                for (int i = 0; i < 15; i++)
                {
                    var enemy = OrdinaryEncounters.Select(seed, i + 1, history);
                    TestSuite.Check(i < 3 ? !SkaterRules.IsHard(enemy) && !history.Contains(enemy) : SkaterRules.IsHard(enemy), "three unique easy encounters, then hard only");
                    if (i == 3) first.Add(enemy);
                    if (i > 3) TestSuite.Check(enemy != history[i - 1], "hard pool never repeats consecutive encounter");
                    history.Add(enemy);
                }
            }
            TestSuite.Check(first.Count == 2, "both hard encounters can start the hard sequence");
        }
    }
    public static partial class UiTests
    {
        public static void TestSkaters()
        {
            foreach (int fps in new[] { 10, 30, 60, 120 }) using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840); Call(game, "StartTestSkaters");
                var fight = Read<CombatState>(game, "combat");
                if (fps == 30)
                {
                    using (var shot = Render(game)) shot.Save(Path.Combine(TestSuite.OutputDirectory, "skaters-ready.png"));
                    Call(game, "SelectCombatTarget", 1); TestSuite.Check(fight.SelectedTarget == 1, "first padawan selectable");
                    Call(game, "SelectCombatTarget", 2); TestSuite.Check(fight.SelectedTarget == 2, "second padawan selectable");
                    foreach (var size in new[] { new Size(1920, 1080), new Size(900, 1400) })
                    { game.ClientSize = size; using (var shot = Render(game)) shot.Save(Path.Combine(TestSuite.OutputDirectory, "skaters-" + size.Width + ".png")); }
                    game.ClientSize = new Size(1280, 840);
                }
                Call(game, "EndCombatTurn"); Call(game, "SetInventory", "pause"); Call(game, "AdvanceCombatAnimation", 1f);
                TestSuite.Check(fight.Hero.Health == 50, "pause before impact keeps HP"); Call(game, "SetInventory", "");
                for (int n = 0; n < fps * 6 && fight.Turn == 1; n++) Call(game, "AdvanceCombatAnimation", 1f / fps);
                TestSuite.Check(fight.Hero.Health == 30 && fight.Turn == 2, "animation resolves exactly twelve plus four plus four independent of FPS");
                using (Render(game)) { }
                var store = Read<ImageAssets>(game, "assetStore"); int count = store.Count;
                using (Render(game)) { } using (Render(game)) { }
                TestSuite.Check(store.Count == count, "shared approved art source is not duplicated on repaint");
                Call(game, "ReturnToMainMenu"); TestSuite.Check(Read<CombatState>(game, "combat") == null, "scene cleanup");
            }
        }
    }
}
