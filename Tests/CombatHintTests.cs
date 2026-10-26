using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        static void TestRouteButton()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                Call(game, "StartTestBattle"); FinishTestFight(game);
                using (Render(game)) { }
                var area = (RectangleF)typeof(Game).GetProperty("RouteButtonArea", PrivateInstance).GetValue(game, null);
                var run = Read<MapRun>(game, "mapRun");
                TestSuite.Check(run.Pending != null, "victory waits for explicit return");
                HoverRelic(game, area.X + area.Width / 2, area.Y + area.Height / 2);
                using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "route-button-hover.png"));
                var point = game.DrawingViewport.ToClient(new PointF(area.X + 100, area.Y + 30));
                Call(game, "OnMapMouseDown", game, new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
                TestSuite.Check(Read<bool>(game, "routePressed"), "route button enters pressed state");
                using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "route-button-pressed.png"));
                Call(game, "OnMapMouseUp", game, new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0));
                TestSuite.Check(!Read<bool>(game, "routePressed"), "release resets pressed state");
                Click(game, area.X + 100, area.Y + 30);
                TestSuite.Check(run.Pending == null && Read<CombatState>(game, "combat") == null, "new button completes room exactly once");
            }
        }

        public static void TestCombatHints()
        {
            foreach (var size in Sizes)
                using (var game = new Game())
                {
                    game.CreateControl(); game.ClientSize = size;
                    Call(game, "StartTestHedgehogs");
                    var fight = Read<CombatState>(game, "combat");
                    fight.EndPlayerTurn(); fight.ResolveEnemyTurn();
                    using (Render(game)) { }
                    var areas = Read<List<Tuple<RectangleF, CombatHint>>>(game, "combatHintAreas");
                    TestSuite.Check(areas.Any(a => a.Item2.Title == "Бешенство"), "real rabies has a hover target at " + size);
                    TestSuite.Check(areas.Count(a => a.Item2.Title == "Шипы") == 1, "thorns belongs only to spiked enemy");
                    TestSuite.Check(!areas.Any(a => a.Item2.Title.StartsWith("Блок")), "zero block never draws shield");
                    var rabies = areas.First(a => a.Item2.Title == "Бешенство");
                    foreach (var area in areas)
                        TestSuite.Check(area.Item1.Left >= 0 && area.Item1.Right <= game.DrawingViewport.Width && area.Item1.Top >= 64 && area.Item1.Bottom <= CombatLayout.CardSlot(game.DrawingViewport.Width, game.DrawingViewport.Height, 3, 0).Top, "hint stays in battle area at " + size + ": " + area.Item2.Title + " " + area.Item1);
                    int hp = fight.Hero.Health, turn = fight.Turn, hand = fight.Hand.Count;
                    var hits = Read<List<Tuple<RectangleF, Action>>>(game, "actions");
                    int clickCount = hits.Count;
                    HoverRelic(game, rabies.Item1.X + 12, rabies.Item1.Y + 12);
                    TestSuite.Check(Read<PointF?>(game, "combatPointer").HasValue, "scaled pointer maps to battle");
                    TestSuite.Check(hits.Count == clickCount, "hover preserves click actions before repaint");
                    using (var frame = Render(game))
                        frame.Save(Path.Combine(TestSuite.OutputDirectory, "combat-status-" + size.Width + "x" + size.Height + ".png"));
                    TestSuite.Check(fight.Hero.Health == hp && fight.Turn == turn && fight.Hand.Count == hand, "hover never changes rules");
                    Call(game, "OnHudMouseLeave", game, EventArgs.Empty);
                    TestSuite.Check(!Read<PointF?>(game, "combatPointer").HasValue, "leaving clears combat tooltip");
                    Call(game, "ReturnToMainMenu");
                    TestSuite.Check(Read<List<Tuple<RectangleF, CombatHint>>>(game, "combatHintAreas").Count == 0, "reset clears hover regions");
                }
            using (var game = new Game())
            {
                game.CreateControl(); Call(game, "StartTestMimicrator");
                var fight = Read<CombatState>(game, "combat");
                var enemy = fight.Enemies[0];
                TestSuite.Check(CombatHints.Intent(enemy).Single().Title == "Фея · отдых", "first fairy rests");
                fight.EndPlayerTurn(); fight.ResolveEnemyTurn();
                TestSuite.Check(CombatHints.Intent(enemy)[0].Icon == 11, "intent anticipates beacon while fairy stays visible");
                fight.EndPlayerTurn(); fight.PrepareNextEnemyAction();
                TestSuite.Check(CombatHints.Hero(fight).Any(h => h.Title == "Оглушение · следующий ход"), "pending stun is distinct at form entry");
                fight.ResolveNextEnemyAction();
                TestSuite.Check(CombatHints.Hero(fight).Any(h => h.Title == "Оглушение" && h.Number == "1"), "active stun has one card, normal hand");
                using (Render(game)) { }
                fight.EndPlayerTurn(); fight.ResolveEnemyTurn();
                var shell = CombatHints.Intent(enemy);
                TestSuite.Check(shell.Count == 2 && shell[0].Number == "5 × 2" && shell[1].Number == "12", "shell intent separates multihit and immediate block");
                fight.EndPlayerTurn(); fight.PrepareNextEnemyAction();
                using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "combat-mussel-block.png"));
                TestSuite.Check(Read<List<Tuple<RectangleF, CombatHint>>>(game, "combatHintAreas").Any(a => a.Item2.Title == "Блок · 12"), "actual shell entry block displayed");
            }
            TestRouteButton();
            var hero = new HeroState(0, 50, 0, new Balance()); hero.Effects.Choose("shield"); StarterDeck.AddTo(hero);
            var battle = new CombatState(hero, new Random(1), EnemyKind.Rat);
            TestSuite.Check(CombatHints.Hero(battle).Any(h => h.Title == "Саттен-киссюн"), "first turn protection active");
            battle.EndPlayerTurn(); battle.ResolveEnemyTurn();
            TestSuite.Check(!CombatHints.Hero(battle).Any(h => h.Title == "Саттен-киссюн"), "first turn protection expires");
            hero.ReceiveDamage(7);
            TestSuite.Check(CombatHints.Hero(battle).Any(h => h.Title == "Слёзы" && h.Number == "7"), "tears display actual accumulated value");
        }
    }
}
