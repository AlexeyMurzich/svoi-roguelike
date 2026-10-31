using System;
using System.Drawing;
using System.IO;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        static int RemainingNegative(Game game, Organ organ)
        {
            return (int)typeof(Game).GetMethod("NegativeFights", PrivateInstance).Invoke(game, new object[] { organ });
        }
        public static void TestNegativeRelics()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                Call(game, "StartTestDoctorEvent");
                var run = Read<MapRun>(game, "mapRun"); var hero = Read<HeroState>(game, "currentHero");
                var visit = run.DoctorsAt(run.Pending.Id);
                TestSuite.Check(visit.Treat(hero) && hero.Doctors.LostOrgans.Count == 1, "real treatment stores one curse");
                using (var image = Render(game)) image.Save(Path.Combine(TestSuite.OutputDirectory, "negative-treatment.png"));
                Call(game, "CompleteMapRoom");
                foreach (Organ organ in Enum.GetValues(typeof(Organ))) hero.Doctors.Lose(organ, hero);
                hero.HealFully();
                Call(game, "SetInventory", "relics");
                using (var image = Render(game)) image.Save(Path.Combine(TestSuite.OutputDirectory, "negative-relics.png"));
                TestSuite.Check(hero.Doctors.LostOrgans.Count == 3 && RemainingNegative(game, Organ.Eye) == 3, "three distinct negative icons with full counters before combat");
                Call(game, "SetInventory", "");
                foreach (var card in hero.Deck) { card.Attack = true; card.Skill = false; card.Damage = 1000; card.GoldCost = 0; card.ManaCost = 0; }
                for (int step = 0; step < 3; step++)
                {
                    // Reuse a deterministic ordinary room; preserve the same hero/penalties.
                    var route = new MapRun(ActMap.Generate(42)); Write(game, "mapRun", route);
                    Call(game, "EnterMapNode", route.Current.Exits[0]);
                    var fight = Read<CombatState>(game, "combat");
                    TestSuite.Check(fight.HiddenCosts && fight.LiverPenaltyActive && RemainingNegative(game, Organ.Eye) == 3 - step && RemainingNegative(game, Organ.Liver) == 3 - step, "HUD includes current affected fight exactly once");
                    Call(game, "SetInventory", "relics");
                    using (var image = Render(game)) image.Save(Path.Combine(TestSuite.OutputDirectory, "negative-fight-" + (step + 1) + ".png"));
                    Call(game, "SetInventory", ""); FinishTestFight(game); Call(game, "CompleteMapRoom");
                    TestSuite.Check(RemainingNegative(game, Organ.Eye) == 2 - step && RemainingNegative(game, Organ.Liver) == 2 - step, "post-victory counter counts only future fights");
                }
                Call(game, "SetInventory", "relics");
                using (var image = Render(game)) image.Save(Path.Combine(TestSuite.OutputDirectory, "negative-expired.png"));
                TestSuite.Check(hero.Doctors.LostOrgans.Count == 3 && hero.MaxHealth == 37, "expired temporary relics remain in inventory; heart remains permanent");
                // Future combinations fit on pages, rather than overflowing the inventory.
                foreach (DoctorDrop item in Enum.GetValues(typeof(DoctorDrop))) hero.Doctors.Give(item);
                Write(game, "doctorRelicPage", 1);
                using (Render(game)) { }
                TestSuite.Check(Read<int>(game, "doctorRelicPage") == 1, "mixed positive and negative inventory has a second page");
                Call(game, "ReturnToMainMenu");
            }
        }
    }
}
