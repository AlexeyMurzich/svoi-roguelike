using System;
using System.Linq;
using System.Drawing;
using System.Reflection;

namespace ShpilSvoih
{
    public static class DoctorTests
    {
        static void Check(bool value, string message) { TestSuite.Check(value, message); }
        static HeroState Hero(int hp)
        {
            var h = new HeroState(0, hp, 0, new Balance()); h.Effects.Choose("zalt"); return h;
        }
        public static void Rules()
        {
            var h = Hero(100); var f = new CombatState(h, new Random(3), EnemyKind.Doctors);
            Check(f.Enemies.Count == 2 && f.Enemies[0].MaxHealth == 40 && f.Enemies[1].MaxHealth == 50, "separate doctor targets and initial balance");
            Check(f.EndPlayerTurn(), "start enemy round"); f.ResolveNextEnemyAction();
            Check(h.Health == 95 && f.Poison == 2 && f.Bleeding == 0, "female impact once: damage and poison");
            Check(!f.ResolveNextEnemyImpact() || f.Phase == CombatPhase.Player, "male resolved");
            Check(h.Health == 91 && f.Poison == 1 && f.Bleeding == 1, "two independent status ticks on player turn");
            f.EndPlayerTurn(); f.ResolveEnemyTurn();
            Check(h.Health == 80 && f.Poison == 2 && f.Bleeding == 2, "stack then decay independently");

            h = Hero(100); h.Doctors.Give(DoctorDrop.Liver); f = new CombatState(h, new Random(3), EnemyKind.Doctors);
            f.EndPlayerTurn(); f.ResolveEnemyTurn();
            Check(h.Health == 93 && f.Poison == 0 && f.Bleeding == 1, "liver stops poison but not bleeding");

            h = Hero(100); f = new CombatState(h, new Random(4)); f.ApplyPoison(5);
            int before = h.Health;
            for (int i = 5; i > 0; i--) { f.EndPlayerTurn(); f.ResolveEnemyTurn(); Check(f.Poison == i - 1, "poison decay"); }
            Check(h.Health == before - 25 - 15, "poison triangular total plus rat attacks");
            f.ApplyBleeding(3); f.ApplyPoison(4);
            Check(f.Bleeding == 3 && f.Poison == 4, "independent counters");

            h = Hero(100); f = new CombatState(h, new Random(2));
            f.TryPlay(0); f.ApplyPoison(5); f.ApplyBleeding(3);
            f.EndPlayerTurn(); f.ResolveEnemyTurn();
            Check(h.Health == 92 && f.Block == 0, "statuses bypass remaining block independently");
            h.Doctors.Give(DoctorDrop.Liver); f.ApplyPoison(10);
            f.EndPlayerTurn(); f.ResolveEnemyTurn();
            Check(f.Poison == 0 && f.Bleeding == 1, "new liver removes existing poison at tick, preserves bleeding");

            h = new HeroState(0, 100, 0, new Balance()); h.Effects.Choose("shield");
            f = new CombatState(h, new Random(2), EnemyKind.Doctors); f.EndPlayerTurn(); f.ResolveEnemyTurn();
            Check(h.Health == 100 && f.Poison == 0 && f.Bleeding == 0, "first-turn shield blocks both debuffs and damage");

            h = Hero(100); h.Doctors.Lose(Organ.Eye, h); h.Doctors.Lose(Organ.Liver, h);
            for (int i = 0; i < 4; i++)
            {
                f = new CombatState(h, new Random(i));
                Check(f.HiddenCosts == (i < 3), "three blind combats exactly");
                Check(f.Poison == (i < 3 ? 4 : 0), "start poison five ticks at first turn");
            }
            h = Hero(101); h.Doctors.Lose(Organ.Heart, h); h.HealFully();
            Check(h.MaxHealth == 75 && h.Health == 75, "maximum reduced before full heal, floor retained HP");
        }
        public static void Relics()
        {
            var h = Hero(101); h.Doctors.Give(DoctorDrop.Heart); h.ReceiveDamage(101);
            Check(h.Health == 26 && h.Doctors.HeartUsed, "heart restores ceil quarter once");
            h.ReceiveDamage(100); Check(h.Health == 0, "heart does not trigger twice");
            h = new HeroState(3, 40, 0, new Balance()); h.Effects.Choose("phone"); h.Doctors.Give(DoctorDrop.Heart);
            h.ChangeMoney(-40); Check(h.Health == 10 && h.Doctors.HeartUsed, "heart on lethal debt");
            h = Hero(20); h.Doctors.Give(DoctorDrop.Heart); var f = new CombatState(h, new Random(1));
            f.ApplyPoison(20); f.EndPlayerTurn(); f.ResolveEnemyTurn();
            Check(h.Health == 5 && h.Doctors.HeartUsed && f.Phase == CombatPhase.Player, "heart on lethal poison before defeat");

            int chocolate = 0; var relics = new int[3];
            for (int seed = 0; seed < 10000; seed++)
            {
                var e = new DoctorEvent(seed); if (e.Drop == DoctorDrop.Chocolate) chocolate++; else relics[(int)e.Drop]++;
                var other = new DoctorEvent(seed); Check(e.Drop == other.Drop && e.LostOrgan == other.LostOrgan, "deterministic event contents");
            }
            Check(chocolate > 1900 && chocolate < 2100 && relics.All(n => n > 2500 && n < 2850), "20 percent replacement and even organ pool");
            h = Hero(100); h.ReceiveDamage(70); var visit = new DoctorEvent(10);
            Check(visit.Treat(h) && h.Health == h.MaxHealth, "treatment heals fully");
            Check(!visit.Treat(h) && !visit.Fight(), "one choice only");
            visit = new DoctorEvent(12); Check(visit.Fight(), "choose battle");
            h = Hero(100); h.AddCard(new CardData { Attack = true, Damage = 1000 });
            f = new CombatState(h, new Random(1), EnemyKind.Doctors);
            Check(!visit.Reward(f), "no premature reward");
            f.TryPlay(0); f.EndPlayerTurn(); f.ResolveEnemyTurn(); f.TryPlay(0);
            Check(f.Phase == CombatPhase.Victory && visit.Reward(f), "victory reward and full heal");
            Check(h.Health == h.MaxHealth && h.Doctors.Items.Count == 1 && !visit.Reward(f), "exactly one item even chocolate and no duplication");
        }
        public static void NegativeRelics()
        {
            foreach (Organ organ in Enum.GetValues(typeof(Organ)))
            {
                var h = Hero(101);
                int seed = Enumerable.Range(0, 100).First(n => new DoctorEvent(n).LostOrgan == organ);
                var visit = new DoctorEvent(seed);
                h.ReceiveDamage(70);
                Check(visit.Treat(h) && h.Health == h.MaxHealth && h.Doctors.LostOrgans.Count == 1 && h.Doctors.LostOrgans[0] == organ, "treatment grants exact negative organ relic");
                int max = h.MaxHealth;
                h.Doctors.Lose(organ, h);
                Check(h.MaxHealth == max && h.Doctors.LostOrgans.Count == 1 && !visit.Treat(h), "negative relic cannot duplicate or reapply on reopen");
                if (organ == Organ.Heart) Check(h.MaxHealth == 75 && h.Doctors.HeartHealthLost == 26, "permanent heart loss stores exact max HP reduction");
                else
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var fight = new CombatState(h, new Random(i));
                        Check(fight.HiddenCosts == (organ == Organ.Eye && i < 3), "eye penalty survives exactly three starts");
                        Check(fight.LiverPenaltyActive == (organ == Organ.Liver && i < 3), "liver penalty active identity tracked separately from immunity");
                        Check(h.Doctors.RemainingFights(organ) == Math.Max(0, 2 - i), "future fights counter decrements once");
                    }
                    Check(h.Doctors.LostOrgans.Count == 1, "expired relic retained as operation history");
                }
            }
            var healthy = Hero(100); var battle = new DoctorEvent(4); battle.Fight();
            Check(healthy.Doctors.LostOrgans.Count == 0, "choosing battle never grants a negative relic");
            healthy.Doctors.Give(DoctorDrop.Liver); healthy.Doctors.Lose(Organ.Liver, healthy);
            var immune = new CombatState(healthy, new Random(2));
            Check(immune.LiverPenaltyActive && immune.Poison == 0 && healthy.Doctors.LiverFights == 2, "immunity suppresses poison without rerolling or losing curse record");
        }
        public static void Questions()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var run = new MapRun(ActMap.Generate(seed)); var h = Hero(100);
                var easy = new System.Collections.Generic.HashSet<EnemyKind>(); int fights = 0, doctorVisits = 0;
                while (!run.Finished)
                {
                    int id = run.Current.Exits[0]; run.Enter(id); var node = run.Pending;
                    EnemyKind? enemy = node.Kind == RoomKind.Fight ? (EnemyKind?)run.OrdinaryEnemyAt(id) : null;
                    if (node.Kind == RoomKind.Question)
                    {
                        var kind = run.QuestionAt(id, h);
                        if (kind == QuestionKind.Doctors) { run.DoctorsAt(id).Treat(h); doctorVisits++; }
                        if (kind == QuestionKind.Fight) enemy = run.StartQuestionFight(id);
                    }
                    if (enemy.HasValue) { Check(fights < 3 ? enemy != EnemyKind.Babai && easy.Add(enemy.Value) : enemy == EnemyKind.Babai, "question and normal nodes share unique easy then hard pool"); fights++; }
                    Check(run.CompleteRoom(), "route closes resolved question");
                    Check(run.CompletedOrdinaryFights == fights && doctorVisits <= 1, "no duplicate doctors or ordinary counters");
                }
            }
            for (int seed = 0; seed < 100; seed++)
            {
                var run = new MapRun(ActMap.Generate(seed)); var h = Hero(100);
                foreach (var n in run.Map.Nodes.Where(n => n.Kind == RoomKind.Question))
                {
                    var before = run.QuestionAt(n.Id, h); h.Doctors.Give(DoctorDrop.Eye);
                    Check(run.QuestionAt(n.Id, h) != QuestionKind.Fight, "eye excludes ordinary question combats");
                    if (before == QuestionKind.Doctors) Check(run.QuestionAt(n.Id, h) == QuestionKind.Doctors, "eye retains voluntary event");
                    Check(n.Kind == RoomKind.Question, "does not transform room");
                }
            }
        }
    }

    public static partial class UiTests
    {
        public static void TestDoctors()
        {
            using (var game = new Game())
            {
                game.CreateControl(); Call(game, "StartTestDoctors");
                var fight = Read<CombatState>(game, "combat");
                foreach (var size in new[] { new Size(960, 720), new Size(1280, 720), new Size(1920, 1080), new Size(3840, 2160) })
                {
                    game.ClientSize = size;
                    using (var shot = new Bitmap(size.Width, size.Height))
                    {
                        game.DrawToBitmap(shot, new Rectangle(Point.Empty, size));
                        if (size.Width == 1280) shot.Save(System.IO.Path.Combine(TestSuite.OutputDirectory, "doctors.png"));
                    }
                }
                Call(game, "EndCombatTurn");
                Write(game, "inventoryView", "pause"); int hp = fight.Hero.Health;
                Call(game, "AdvanceCombatAnimation", 1f); TestSuite.Check(fight.Hero.Health == hp, "doctors pause freezes impact");
                Write(game, "inventoryView", ""); Call(game, "AdvanceCombatAnimation", 3f);
                TestSuite.Check(fight.Hero.Health == hp - 9 && fight.Poison == 1 && fight.Bleeding == 1, "animated impacts apply once");
                var art = Read<ImageAssets>(game, "assetStore"); int count = art.Count;
                Call(game, "LoadDoctorArt"); TestSuite.Check(art.Count == count, "shared doctor sources");
                Call(game, "StartTestDoctorEvent");
                using (var shot = new Bitmap(game.Width, game.Height)) game.DrawToBitmap(shot, new Rectangle(Point.Empty, shot.Size));
                var run = Read<MapRun>(game, "mapRun"); var visit = run.DoctorsAt(run.Pending.Id);
                Call(game, "CompleteMapRoom"); TestSuite.Check(run.Pending != null, "enter cannot skip operation choice");
                visit.Treat(Read<HeroState>(game, "currentHero")); Call(game, "CompleteMapRoom");
                TestSuite.Check(run.Pending == null && run.DoctorsCompleted, "treatment completes unique event");
                Call(game, "ReturnToMainMenu"); TestSuite.Check(Read<CombatState>(game, "combat") == null, "cleanup");
            }
        }
    }
}
