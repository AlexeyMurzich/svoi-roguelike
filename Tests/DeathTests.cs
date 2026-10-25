using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static class DeathTests
    {
        public static void Lifecycle()
        {
            using (var deaths = new DeathVisuals())
            using (var bitmap = new Bitmap(200, 200, PixelFormat.Format32bppPArgb))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                int painted = 0;
                Action<Graphics> paint = g => { painted++; g.FillEllipse(Brushes.Red, 40, 40, 80, 100); };
                TestSuite.Check(!deaths.Draw(graphics, 0, new RectangleF(40, 40, 80, 100), paint), "living creature is drawn by caller");
                deaths.Start(0); deaths.Start(0); deaths.Start(-1);
                deaths.Advance(float.NaN); deaths.Advance(float.PositiveInfinity); deaths.Advance(-1);
                deaths.Draw(graphics, 0, new RectangleF(40, 40, 80, 100), paint);
                deaths.Draw(graphics, -1, new RectangleF(40, 40, 80, 100), paint);
                TestSuite.Check(deaths.SnapshotCount == 2 && deaths.IsRunning && painted == 2, "one snapshot per creature, invalid time ignored");
                graphics.Clear(Color.Transparent);
                deaths.Draw(graphics, 0, new RectangleF(100, 40, 80, 100), paint);
                TestSuite.Check(bitmap.GetPixel(135, 80).A > 200 && bitmap.GetPixel(60, 80).A == 0, "death follows relocated creature after window resize without recapturing");
                deaths.Advance(.4f); deaths.Draw(graphics, 0, new RectangleF(40, 40, 80, 100), paint);
                TestSuite.Check(painted == 2, "dying animation reuses the captured pose");
                deaths.Advance(1);
                TestSuite.Check(!deaths.IsRunning && deaths.SnapshotCount == 0, "finished animation disposes temporary images");
                deaths.Start(0); deaths.Draw(graphics, 0, new RectangleF(40, 40, 80, 100), paint);
                TestSuite.Check(!deaths.IsRunning && painted == 2, "repainting a corpse cannot restart its death");
                deaths.Clear(); deaths.Start(0); deaths.Draw(graphics, 0, new RectangleF(40, 40, 80, 100), paint);
                deaths.Dispose(); deaths.Dispose();
                TestSuite.Check(deaths.SnapshotCount == 0 && !deaths.IsRunning, "scene reset and repeated disposal release snapshots");
            }
        }
    }

    public static partial class UiTests
    {
        static CombatState ReplaceFight(Game game, HeroState hero, EnemyKind enemy)
        {
            if (hero.Effects.Chosen == null) hero.Effects.Choose(new[] { "exchange", "armor", "feather", "ring" }[hero.Hero]);
            Read<AttackTimeline>(game, "attackTimeline").Cancel(); Read<DeathVisuals>(game, "deathVisuals").Clear();
            Write(game, "selected", hero.Hero); Write(game, "currentHero", hero);
            var fight = new CombatState(hero, new Random(4), enemy);
            Write(game, "combat", fight); Call(game, "RefreshScene"); return fight;
        }
        public static void TestDeathsAndGuard()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840); Call(game, "StartTestDogs");
                var slayer = new HeroState(3, 50, 0, new Balance()); slayer.Effects.Choose("ring");
                for (int i = 0; i < 3; i++) slayer.AddCard(new CardData { Attack = true, Damage = 30, GoldCost = 0 });
                var fight = ReplaceFight(game, slayer, EnemyKind.StrayDogs);
                Call(game, "PlayCombatCard", 0); Call(game, "AdvanceCombatAnimation", .5f);
                var deaths = Read<DeathVisuals>(game, "deathVisuals");
                TestSuite.Check(!fight.Enemies[0].IsAlive && fight.Enemies[1].IsAlive && deaths.IsRunning, "first dog dies independently");
                Key(game, Keys.Enter);
                TestSuite.Check(fight.Phase == CombatPhase.Player && fight.Turn == 1, "input waits for death animation");
                using (Render(game)) { }
                TestSuite.Check(deaths.SnapshotCount == 1, "only dying dog gets temporary sprite");
                Call(game, "AdvanceCombatAnimation", .35f);
                using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "dog-death.png"));
                Key(game, Keys.Escape); Call(game, "AdvanceCombatAnimation", 10f);
                TestSuite.Check(deaths.IsRunning && deaths.SnapshotCount == 1, "pause freezes death and preserves its snapshot");
                Key(game, Keys.Escape); Call(game, "AdvanceCombatAnimation", 1f);
                TestSuite.Check(!deaths.IsRunning && deaths.SnapshotCount == 0 && fight.Phase == CombatPhase.Player, "death completes without false victory");
                Call(game, "EndCombatTurn");
                TestSuite.Check(Read<int>(game, "actingEnemyIndex") == 1, "surviving dog still attacks");
                FinishEnemyAnimations(game);
                Call(game, "PlayCombatCard", 0); Call(game, "AdvanceCombatAnimation", .5f);
                TestSuite.Check(fight.Phase == CombatPhase.Victory && deaths.IsRunning, "last dog triggers victory with death animation");
                Key(game, Keys.Enter);
                TestSuite.Check(Read<CombatState>(game, "combat") == fight, "victory cannot skip last death");
                using (Render(game)) { }
                Call(game, "AdvanceCombatAnimation", 1f); Key(game, Keys.Enter);
                TestSuite.Check(Read<CombatState>(game, "combat") == null && deaths.SnapshotCount == 0, "map transition clears all corpses");

                Call(game, "StartTestBattle");
                var guard = new HeroState(3, 50, 0, new Balance()); guard.Effects.Choose("ring");
                guard.AddCard(new CardData { Block = 5, Skill = true, GoldCost = 10 });
                fight = ReplaceFight(game, guard, EnemyKind.Rat);
                decimal money = guard.Money;
                Call(game, "PlayCombatCard", 0); Call(game, "PlayCombatCard", 0);
                Call(game, "AdvanceCombatAnimation", .1f);
                TestSuite.Check(fight.Block == 0 && guard.Money == money, "guard payment waits for raise animation impact");
                Call(game, "AdvanceCombatAnimation", .1f);
                TestSuite.Check(fight.Block == 5 && guard.Money == money - 10, "credit-card guard pays exactly once");
                using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "shuten-credit-guard.png"));
                FinishEnemyAnimations(game); Call(game, "EndCombatTurn"); Call(game, "AdvanceCombatAnimation", .5f);
                TestSuite.Check(guard.Health == 50 && fight.Block == 0, "credit-card shield absorbs rat bite");
                FinishEnemyAnimations(game);

                for (int hero = 0; hero < 4; hero++)
                {
                    Call(game, "StartTestBattle");
                    var fragile = new HeroState(hero, 4, 0, new Balance());
                    fight = ReplaceFight(game, fragile, EnemyKind.Rat);
                    Call(game, "EndCombatTurn"); Call(game, "AdvanceCombatAnimation", .5f);
                    TestSuite.Check(fight.Phase == CombatPhase.Defeat && deaths.IsRunning, "hero " + hero + " has death animation");
                    using (Render(game)) { }
                    Call(game, "AdvanceCombatAnimation", .4f);
                    using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "hero-death-" + hero + ".png"));
                    Call(game, "AdvanceCombatAnimation", 1f);
                    TestSuite.Check(!deaths.IsRunning && deaths.SnapshotCount == 0, "hero death releases captured image");
                    Key(game, Keys.Enter);
                    TestSuite.Check(Read<CombatState>(game, "combat") == null, "hero defeat can return to menu");
                }
                Call(game, "StartTestBattle");
                var killer = new HeroState(2, 50, 0, new Balance()); killer.AddCard(new CardData { Attack = true, Damage = 30, ManaCost = 0 });
                fight = ReplaceFight(game, killer, EnemyKind.Rat);
                Call(game, "PlayCombatCard", 0); Call(game, "AdvanceCombatAnimation", .5f);
                TestSuite.Check(fight.Phase == CombatPhase.Victory && deaths.IsRunning, "rat also has death animation");
                using (Render(game)) { }
                Call(game, "AdvanceCombatAnimation", .4f);
                using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "rat-death.png"));
                Call(game, "ReturnToMainMenu"); Call(game, "AdvanceCombatAnimation", 10f);
                TestSuite.Check(deaths.SnapshotCount == 0 && !deaths.IsRunning, "leaving during death cancels safely");
            }
        }
    }
}
