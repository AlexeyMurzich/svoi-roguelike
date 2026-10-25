using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        static void FinishTestFight(Game game)
        {
            var fight = Read<CombatState>(game, "combat");
            for (int i = 0; i < 200 && fight.Phase != CombatPhase.Victory && fight.Phase != CombatPhase.Defeat; i++)
            {
                if (Read<AttackTimeline>(game, "attackTimeline").IsRunning || Read<DeathVisuals>(game, "deathVisuals").IsRunning)
                {
                    Call(game, "AdvanceCombatAnimation", 1f);
                    continue;
                }

                int card = fight.Hand.ToList().FindIndex(c => fight.CanPlay(fight.Hand.IndexOf(c)));
                if (card >= 0)
                    Call(game, "PlayCombatCard", card);
                else
                    Call(game, "EndCombatTurn");
            }

            Call(game, "AdvanceCombatAnimation", 1f);
            TestSuite.Check(fight.Phase == CombatPhase.Victory, "fixture wins fight through real card plays");
        }

        public static void TestCombatUi()
        {
            foreach (var size in Sizes)
                using (var game = new Game())
                {
                    game.CreateControl();
                    game.ClientSize = size;
                    Call(game, "StartTestBattle");
                    var fight = Read<CombatState>(game, "combat");
                    var run = Read<MapRun>(game, "mapRun");
                    TestSuite.Check(fight.Hero.Hero == 3 && fight.Hero.Effects.Has("ring") && fight.Hand.Count == 3, "direct test launches Shuten with ring and three cards");
                    using (var frame = Render(game))
                        if (size == Sizes[2])
                            frame.Save(Path.Combine(TestSuite.OutputDirectory, "first-fight.png"));
                    Key(game, Keys.Enter);
                    TestSuite.Check(fight.Phase == CombatPhase.Enemy && run.Current.Floor == 0, "enter ends turn rather than bypassing battle");
                    Key(game, Keys.Enter);
                    Key(game, Keys.Escape);
                    Call(game, "AdvanceCombatAnimation", 1f);
                    TestSuite.Check(fight.Hero.Health == 50 && fight.Turn == 1, "pause freezes enemy attack at animation boundary");
                    using (Render(game))
                    {
                    }

                    Key(game, Keys.Escape);
                    Call(game, "AdvanceCombatAnimation", .4f);
                    TestSuite.Check(fight.Hero.Health == 50, "open jaws do not deal damage early");
                    using (var frame = Render(game))
                        if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "rat-open-jaws.png"));
                    Call(game, "AdvanceCombatAnimation", .15f);
                    TestSuite.Check(fight.Hero.Health == 45 && fight.Hand.Count == 3 && fight.Turn == 2, "rat hits for five and next turn draws three");
                    using (var frame = Render(game))
                        if (size == Sizes[2])
                            frame.Save(Path.Combine(TestSuite.OutputDirectory, "rat-attack.png"));
                    Call(game, "AdvanceCombatAnimation", 1f);
                    int attack = fight.Hand.ToList().FindIndex(c => c.Attack);
                    for (int retry = 0; attack < 0 && retry < 20; retry++)
                    {
                        Call(game, "EndCombatTurn");
                        Call(game, "AdvanceCombatAnimation", 1.1f);
                        attack = fight.Hand.ToList().FindIndex(c => c.Attack);
                    }

                    TestSuite.Check(attack >= 0, "fixture found an attack after shuffled draws");
                    int enemyHp = fight.EnemyHealth;
                    decimal money = fight.Hero.Money;
                    Call(game, "PlayCombatCard", attack);
                    Call(game, "PlayCombatCard", attack);
                    Call(game, "AdvanceCombatAnimation", .2f);
                    using (var frame = Render(game))
                        if (size == Sizes[2])
                            frame.Save(Path.Combine(TestSuite.OutputDirectory, "shuten-attack.png"));
                    Call(game, "AdvanceCombatAnimation", .31f);
                    TestSuite.Check(fight.EnemyHealth == enemyHp - 6 && fight.Hero.Money == money - 10, "animation impact applies one attack and one payment at " + size + ": target=" + enemyHp + "->" + fight.EnemyHealth + ", gold=" + money + "->" + fight.Hero.Money + ", phase=" + fight.Phase + ", card=" + attack + ", kind=" + Read<AttackTimeline>(game, "attackTimeline").Kind);
                    Call(game, "AdvanceCombatAnimation", 1f);
                    FinishTestFight(game);
                    using (var frame = Render(game))
                        if (size == Sizes[2])
                            frame.Save(Path.Combine(TestSuite.OutputDirectory, "victory.png"));
                    Key(game, Keys.Enter);
                    TestSuite.Check(run.Current.Floor == 1 && run.Pending == null && Read<CombatState>(game, "combat") == null, "victory returns to map and releases combat state");
                    using (Render(game))
                    {
                    }

                    Call(game, "OpenCurrentMapRoom");
                    TestSuite.Check(run.PendingIsRevisit && Read<CombatState>(game, "combat") == null, "revisiting cleared fight never respawns enemy");
                    Key(game, Keys.Enter);
                    TestSuite.Check(run.Current.Floor == 1, "revisit cannot grant a second victory");
                    Call(game, "StartTestBattle");
                    Call(game, "EndCombatTurn");
                    Call(game, "ReturnToMainMenu");
                    Call(game, "AdvanceCombatAnimation", 1f);
                    TestSuite.Check(Read<CombatState>(game, "combat") == null && !Read<bool>(game, "mapScreen"), "returning to menu cancels pending animation safely");
                }
        }
    }
}
