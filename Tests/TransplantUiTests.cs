using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        public static void TestTransplantRewards()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                Call(game, "StartTestDoctorEvent");
                var hero = Read<HeroState>(game, "currentHero"); var run = Read<MapRun>(game, "mapRun");
                var visit = run.DoctorsAt(run.Pending.Id);
                foreach (var size in new[] { new Size(1280, 840), new Size(1920, 1080), new Size(900, 1400) })
                {
                    game.ClientSize = size;
                    using (var shot = Render(game)) shot.Save(Path.Combine(TestSuite.OutputDirectory, "transplant-choice-" + size.Width + ".png"));
                }
                game.ClientSize = new Size(1280, 840);
                using (Render(game)) { }
                Click(game, 500, 580);
                var fight = Read<CombatState>(game, "combat");
                TestSuite.Check(visit.Choice == DoctorChoice.Battle && fight != null, "real choice button starts transplant battle");
                TestSuite.Check(EnemyCatalog.Name(EnemyKind.FemaleDoctor) == "Чёрный трансплантолог" && EnemyCatalog.Name(EnemyKind.MaleDoctor) == "Чёрный трансплантолог", "both enemy labels renamed");
                TestSuite.Check(!visit.ClaimReward(fight), "claim rejected before victory");
                foreach (var card in hero.Deck) { card.Attack = true; card.Skill = false; card.Damage = 1000; card.GoldCost = 0; card.ManaCost = 0; }
                foreach (var card in fight.Hand) { card.Attack = true; card.Skill = false; card.Damage = 1000; card.GoldCost = 0; card.ManaCost = 0; }
                FinishTestFight(game);
                TestSuite.Check(visit.Choice == DoctorChoice.Victory && hero.Health == hero.MaxHealth && hero.Doctors.Items.Count == 0, "victory heals and exposes unclaimed item");
                Call(game, "CompleteMapRoom"); TestSuite.Check(run.Pending != null, "cannot bypass unclaimed reward with route action");
                foreach (var size in new[] { new Size(1280, 840), new Size(1920, 1080), new Size(900, 1400) })
                {
                    game.ClientSize = size;
                    using (var shot = Render(game)) shot.Save(Path.Combine(TestSuite.OutputDirectory, "transplant-rewards-" + size.Width + ".png"));
                }
                game.ClientSize = new Size(1280, 840);
                using (Render(game)) { }
                Click(game, 500, 480);
                TestSuite.Check(visit.RewardClaimed && hero.Doctors.Items.Count == 1, "clicking reward row grants exact rolled item");
                Click(game, 500, 480);
                TestSuite.Check(hero.Doctors.Items.Count == 1 && !visit.ClaimReward(fight), "double click does not duplicate relic");
                using (var shot = Render(game)) shot.Save(Path.Combine(TestSuite.OutputDirectory, "transplant-claimed.png"));
                Key(game, Keys.Enter);
                TestSuite.Check(run.Pending == null && run.DoctorsCompleted, "continue exits only after claim");
                Call(game, "StartTestDoctorEvent");
                using (Render(game)) { }
                Click(game, 500, 480);
                hero = Read<HeroState>(game, "currentHero");
                TestSuite.Check(hero.Doctors.LostOrgans.Count == 1 && hero.Health == hero.MaxHealth, "simplified treatment button keeps approved penalty and full healing");
                using (var shot = Render(game)) shot.Save(Path.Combine(TestSuite.OutputDirectory, "transplant-treatment.png"));
                Call(game, "CompleteMapRoom"); Call(game, "ReturnToMainMenu");
            }
        }
    }
}
