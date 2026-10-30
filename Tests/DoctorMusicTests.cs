using System;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        public static void TestDoctorMusicScenes()
        {
            using (var game = new Game())
            {
                game.CreateControl();
                Call(game, "StartTestDoctorEvent");
                TestSuite.Check(MusicChoice(game) == "doctors", "doctor theme begins on choice screen");
                var run = Read<MapRun>(game, "mapRun"); var visit = run.DoctorsAt(run.Pending.Id);
                visit.Fight(); Call(game, "EnsureCombat");
                TestSuite.Check(MusicChoice(game) == "doctors", "choice to fight keeps same track id");
                Call(game, "SetInventory", "pause");
                TestSuite.Check(MusicChoice(game) == "doctors", "pause keeps doctor track identity");
                Call(game, "SetInventory", "");
                var hero = Read<HeroState>(game, "currentHero");
                foreach (var card in hero.Deck) { card.Attack = true; card.Skill = false; card.Damage = 1000; card.GoldCost = 0; card.ManaCost = 0; }
                foreach (var card in Read<CombatState>(game, "combat").Hand) { card.Attack = true; card.Skill = false; card.Damage = 1000; card.GoldCost = 0; card.ManaCost = 0; }
                FinishTestFight(game);
                TestSuite.Check(visit.Choice == DoctorChoice.Victory && MusicChoice(game) == "doctors", "victory screen retains ordinary scene music behaviour");
                int id = run.Pending.Id; Call(game, "CompleteMapRoom");
                TestSuite.Check(MusicChoice(game) == "", "leaving completed battle stops doctor theme");
                Call(game, "EnterMapNode", id);
                TestSuite.Check(MusicChoice(game) == "", "completed room revisit does not restart doctor song");
                Call(game, "StartTestDoctorEvent");
                run = Read<MapRun>(game, "mapRun"); hero = Read<HeroState>(game, "currentHero");
                run.DoctorsAt(run.Pending.Id).Treat(hero);
                TestSuite.Check(MusicChoice(game) == "doctors", "peaceful result keeps event theme until leaving");
                Call(game, "CompleteMapRoom");
                TestSuite.Check(MusicChoice(game) == "", "peaceful exit stops theme");
                Call(game, "StartTestDoctors");
                TestSuite.Check(MusicChoice(game) == "doctors", "direct doctor battle uses same theme");
                Call(game, "StartTestDogs");
                TestSuite.Check(MusicChoice(game) == "", "ordinary encounter does not inherit doctor music");
                Call(game, "ReturnToMainMenu");
                TestSuite.Check(Read<MusicPlayer>(game, "music").Track == "", "offscreen verification opens no playback voice");
            }
        }
    }
}
