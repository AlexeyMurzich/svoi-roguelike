using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static class DebugModeTests
    {
        static HeroState Hero()
        {
            var hero = new HeroState(3, 1000, 0, new Balance());
            hero.Effects.Choose("ring");
            StarterDeck.AddTo(hero);
            return hero;
        }
        public static void Rules()
        {
            foreach (int who in new[] { 0, 1, 2 })
            {
                var other = new HeroState(who, 50, 10, new Balance());
                TestSuite.Check(!other.EnableShutGod() && !other.ShutGod && other.Money == 10, "cheat restricted to Shuten");
            }
            var hero = Hero();
            var fight = new CombatState(hero, new Random(3), EnemyKind.Rat);
            var attack = new CardData { Attack = true, Damage = 6 };
            var block = new CardData { Skill = true, Block = 5 };
            TestSuite.Check(fight.DamageFor(attack) == 6 && fight.BlockFor(block) == 5, "normal cards unaffected");
            TestSuite.Check(hero.EnableShutGod() && hero.MoneyDisplay == "∞" && hero.Debt == 0, "infinite gold is a mode, not overflowing decimal");
            TestSuite.Check(fight.DamageFor(attack) == 18 && fight.BlockFor(block) == 15, "card damage and block triple immediately");
            hero.EnableShutGod();
            TestSuite.Check(fight.DamageFor(attack) == 18 && fight.BlockFor(block) == 15, "repeated command does not stack");
            TestSuite.Check(hero.CanAffordMoneyCard(int.MaxValue, int.MaxValue) && hero.PayMoneyCard(int.MaxValue, int.MaxValue), "infinite gold bypasses payment and wealth gate");
            hero.ChangeMoney(decimal.MinValue); hero.ChangeMoney(decimal.MaxValue); hero.BeginTurn();
            TestSuite.Check(hero.Health == 1000 && hero.Debt == 0 && hero.MoneyDisplay == "∞", "payments and passive gold loss never create debt");
            TestSuite.Check(fight.DamageFor(new CardData { Attack = true, Damage = int.MaxValue }) == int.MaxValue &&
                fight.BlockFor(new CardData { Block = int.MaxValue }) == int.MaxValue, "tripled values saturate without wrapping");
            var expensive = new HeroState(3, 100, 0, new Balance());
            expensive.Effects.Choose("phone");
            for (int i = 0; i < 3; i++) expensive.AddCard(new CardData { Skill = true, Block = 5, GoldCost = decimal.MaxValue });
            expensive.EnableShutGod();
            var costly = new CombatState(expensive, new Random(1), EnemyKind.Rat);
            TestSuite.Check(costly.CanPlay(0) && costly.TryPlay(0) && costly.Block == 15 && expensive.Health == 100, "actual play uses infinite money and tripled block");
            foreach (var kind in new[] { EnemyKind.Hedgehogs, EnemyKind.StrayDogs })
            {
                var normal = new CombatState(Hero(), new Random(4), kind);
                var god = new CombatState(Hero(), new Random(4), kind); god.Hero.EnableShutGod();
                for (int turn = 0; turn < 3; turn++)
                {
                    normal.EndPlayerTurn(); normal.ResolveEnemyTurn();
                    god.EndPlayerTurn(); god.ResolveEnemyTurn();
                    TestSuite.Check(god.Hero.Health == normal.Hero.Health, "cheat does not make hero invulnerable");
                    TestSuite.Check(god.DamageFor(attack) == normal.DamageFor(attack) * 3 && god.BlockFor(block) == normal.BlockFor(block) * 3, "weakness and rabies retained before exact triple multiplier");
                }
            }
            TestSuite.Check(!Hero().ShutGod, "new run has no cheat");
        }
    }

    public static partial class UiTests
    {
        static void TypeConsole(Game game, string value)
        {
            foreach (char c in value) Call(game, "OnDebugConsoleKeyPress", game, new KeyPressEventArgs(c));
        }
        public static void TestDebugConsole()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                Key(game, Keys.Oem3);
                TestSuite.Check(Read<bool>(game, "debugConsoleOpen"), "physical yo/backtick key opens console");
                TypeConsole(game, "shut_god"); Key(game, Keys.Enter);
                TestSuite.Check(Read<string>(game, "debugReply").Contains("Начните"), "command safely rejected without a run");
                Key(game, Keys.Escape);
                Call(game, "StartTestMimicrator");
                var fight = Read<CombatState>(game, "combat");
                Call(game, "EndCombatTurn");
                Key(game, Keys.Oem3);
                int hp = fight.Hero.Health, turn = fight.Turn;
                double ambient = Read<double>(game, "mimicratorCombatSeconds"), living = Read<double>(game, "livingSeconds");
                float animation = Read<AttackTimeline>(game, "attackTimeline").Time;
                Call(game, "AdvanceCombatAnimation", 5f); Call(game, "AdvanceCombatAmbient", 5f); Call(game, "AdvanceLivingAnimation", 5f);
                TestSuite.Check(fight.Hero.Health == hp && fight.Turn == turn && Read<AttackTimeline>(game, "attackTimeline").Time == animation, "console freezes attack and impacts");
                TestSuite.Check(Read<double>(game, "mimicratorCombatSeconds") == ambient && Read<double>(game, "livingSeconds") == living, "console freezes orbit and idle");
                TypeConsole(game, "unknown"); Key(game, Keys.Enter);
                TestSuite.Check(!fight.Hero.ShutGod && Read<string>(game, "debugReply").Contains("Неизвестная"), "unknown command never executes anything");
                TypeConsole(game, "shut_god"); Key(game, Keys.Enter);
                TestSuite.Check(fight.Hero.ShutGod && fight.Turn == turn && fight.Hero.Health == hp, "enter executes command without ending turn");
                using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "debug-console-shut-god.png"));
                TestSuite.Check(Read<System.Collections.Generic.List<Tuple<RectangleF, Action>>>(game, "actions").Count == 0, "console blocks mouse targets");
                Click(game, 700, 700);
                TestSuite.Check(fight.Turn == turn, "mouse does not reach battle behind console");
                for (int i = 0; i < 100; i++) TypeConsole(game, "x");
                TestSuite.Check(Read<string>(game, "debugCommand").Length == 80, "console input bounded");
                Key(game, Keys.Back);
                TestSuite.Check(Read<string>(game, "debugCommand").Length == 79, "backspace edits input");
                Key(game, Keys.Oem3);
                Call(game, "AdvanceCombatAnimation", 5f);
                TestSuite.Check(fight.Phase == CombatPhase.Player && fight.Turn == 2, "closing console resumes from paused animation");
                using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-courtyard-god.png"));
                Call(game, "ReturnToMainMenu");
                Call(game, "StartTestBattle");
                TestSuite.Check(!Read<CombatState>(game, "combat").Hero.ShutGod && !Read<bool>(game, "debugConsoleOpen"), "new run resets test mode and console");
            }
        }
    }
}
