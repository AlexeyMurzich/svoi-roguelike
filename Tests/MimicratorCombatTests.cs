using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static class MimicratorCombatTests
    {
        static HeroState Hero(int who = 2, int block = 0, int damage = 0)
        {
            var hero = new HeroState(who, 1000, 0, new Balance());
            hero.Effects.Choose(who == 1 ? "polish" : who == 0 ? "shield" : "feather");
            for (int i = 0; i < 12; i++) hero.AddCard(new CardData
                { Name = "Тест", Attack = damage > 0, Skill = damage == 0, Damage = damage, Block = block, ManaCost = 0 });
            return hero;
        }

        static void End(CombatState fight)
        {
            TestSuite.Check(fight.EndPlayerTurn() && fight.ResolveEnemyTurn(), "enemy resolves");
        }

        public static void Rules()
        {
            var fight = new CombatState(Hero(), new Random(1), EnemyKind.Mimicrator);
            var enemy = fight.Enemies[0];
            TestSuite.Check(enemy.Health == 60 && enemy.MaxHealth == 60, "elite HP");
            var forms = new[] { MimicratorForm.Fairy, MimicratorForm.Beacon, MimicratorForm.Bear,
                MimicratorForm.Mussel, MimicratorForm.Beacon, MimicratorForm.Bear, MimicratorForm.Mussel };
            foreach (var form in forms)
            {
                TestSuite.Check(enemy.Form == form, "fairy once, then beacon/bear/mussel");
                int health = fight.Hero.Health, damage = enemy.AttackDamage;
                End(fight);
                TestSuite.Check(fight.Hero.Health == health - damage, "one correctly sized impact");
                TestSuite.Check(fight.IsStunned == (form == MimicratorForm.Beacon), "stun only next player turn");
                TestSuite.Check(enemy.Block == (form == MimicratorForm.Mussel ? 12 : 0), "defense expires at next own action");
                TestSuite.Check(!fight.ResolveNextEnemyAction() && fight.Hero.Health == health - damage, "no duplicate action");
            }

            fight = new CombatState(Hero(), new Random(2), EnemyKind.Mimicrator);
            End(fight); End(fight);
            TestSuite.Check(fight.IsStunned && fight.Hand.Count == 3, "stun preserves draw");
            TestSuite.Check(fight.TryPlay(0) && fight.CardsPlayedThisTurn == 1, "one zero-cost card allowed");
            int remaining = fight.Hand.Count;
            TestSuite.Check(!fight.TryPlay(0) && fight.Hand.Count == remaining && !fight.CanPlay(0),
                "zero cost and skills cannot bypass limit");
            End(fight);
            TestSuite.Check(!fight.IsStunned && fight.CardsPlayedThisTurn == 0 && fight.CanPlay(0), "one turn duration");
            var next = new CombatState(fight.Hero, new Random(3));
            TestSuite.Check(!next.IsStunned, "no carry into next fight");

            fight = new CombatState(Hero(1), new Random(4), EnemyKind.Mimicrator);
            End(fight); End(fight);
            TestSuite.Check(fight.IsStunned && fight.CardsPlayedThisTurn == 1 && fight.Hand.Count == 4,
                "Illya auto cards respect same limit");
            TestSuite.Check(!fight.CanPlay(0), "automatic card consumes sole play");

            fight = new CombatState(Hero(2, 30), new Random(5), EnemyKind.Mimicrator);
            End(fight);
            fight.TryPlay(0); End(fight);
            TestSuite.Check(fight.LastDamage == 0 && fight.IsStunned, "blocked beacon still stuns");

            fight = new CombatState(Hero(2, 0, 6), new Random(6), EnemyKind.Mimicrator);
            for (int i = 0; i < 4; i++) End(fight);
            TestSuite.Check(fight.Enemies[0].Block == 12 && fight.Hero.Health == 975, "shell guards without damage");
            fight.TryPlay(0);
            TestSuite.Check(fight.EnemyHealth == 60 && fight.Enemies[0].Block == 6, "enemy block absorbs first hit");
            fight.TryPlay(0); fight.TryPlay(0);
            TestSuite.Check(fight.EnemyHealth == 54 && fight.Enemies[0].Block == 0, "overflow reaches HP");


            fight = new CombatState(Hero(2, 0, 6), new Random(8), EnemyKind.Mimicrator);
            for (int i = 0; i < 4; i++) End(fight);
            fight.TryPlay(0);
            TestSuite.Check(fight.Enemies[0].Block == 6, "partial shell block remains");
            End(fight);
            TestSuite.Check(fight.Enemies[0].Block == 0, "unused enemy block expires on next action");

            var doomed = new HeroState(2, 6, 0, new Balance()); doomed.Effects.Choose("feather");
            fight = new CombatState(doomed, new Random(9), EnemyKind.Mimicrator);
            End(fight); End(fight);
            TestSuite.Check(fight.Phase == CombatPhase.Defeat && doomed.Health == 0 &&
                !fight.ResolveNextEnemyAction(), "lethal beacon stops queue and no new player turn");

            var protectedHero = Hero(0);
            fight = new CombatState(protectedHero, new Random(7), EnemyKind.Mimicrator);
            typeof(CombatEnemy).GetProperty("CyclePosition").SetValue(fight.Enemies[0], 1, null);
            End(fight);
            TestSuite.Check(protectedHero.Health == 1000 && !fight.IsStunned, "first turn relic cancels damage and stun");
        }
    }

    public static partial class UiTests
    {
        static void SettleMimicrator(Game game)
        {
            int frames = 0;
            while ((bool)typeof(Game).GetProperty("CombatBusy", PrivateInstance).GetValue(game, null) && frames++ < 120)
                Call(game, "AdvanceCombatAnimation", .1f);
            TestSuite.Check(frames < 120, "bounded action/transform/death queue");
        }

        public static void TestMimicratorCombat()
        {
            foreach (var size in Sizes)
                using (var game = new Game())
                {
                    game.CreateControl(); game.ClientSize = size;
                    Call(game, "StartTestMimicrator");
                    var fight = Read<CombatState>(game, "combat");
                    TestSuite.Check(fight.Enemy == EnemyKind.Mimicrator && fight.Turn == 1, "test launch starts fairy");
                    using (var frame = Render(game))
                        if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-fairy-battle.png"));
                    Call(game, "AdvanceCombatAmbient", .2f);
                    double clock = Read<double>(game, "mimicratorCombatSeconds");
                    Call(game, "EndCombatTurn"); Call(game, "AdvanceCombatAnimation", .15f);
                    Key(game, Keys.Escape);
                    Call(game, "AdvanceCombatAnimation", 1f); Call(game, "AdvanceCombatAmbient", 1f);
                    TestSuite.Check(fight.Hero.Health == 50 && Read<double>(game, "mimicratorCombatSeconds") == clock,
                        "pause freezes impact and ambient orbits");
                    Key(game, Keys.Escape);
                    Call(game, "AdvanceCombatAnimation", .75f);
                    TestSuite.Check(Read<AttackTimeline>(game, "attackTimeline").Kind == AttackAnimation.MimicratorTransform,
                        "automatic first transformation before player input");
                    using (var frame = Render(game))
                        if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-transform.png"));
                    SettleMimicrator(game);
                    TestSuite.Check(fight.Hero.Health == 45 && fight.Turn == 2 &&
                        Read<MimicratorForm>(game, "mimicratorVisualForm") == MimicratorForm.Beacon, "beacon ready in second round");

                    Call(game, "EndCombatTurn");
                    TestSuite.Check(Read<AttackTimeline>(game, "attackTimeline").Kind == AttackAnimation.MimicratorBeacon, "beacon action animation");
                    SettleMimicrator(game);
                    TestSuite.Check(fight.Turn == 3 && fight.Hero.Health == 39 && fight.IsStunned &&
                        Read<MimicratorForm>(game, "mimicratorVisualForm") == MimicratorForm.Bear, "stun and bear round");
                    using (var before = Render(game))
                    {
                        Call(game, "AdvanceCombatAmbient", .2f);
                        using (var after = Render(game))
                            TestSuite.Check(Digest(before) != Digest(after), "cartoon birds animate");
                    }
                    using (var frame = Render(game))
                        if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-stun-birds.png"));
                    decimal money = fight.Hero.Money;
                    Call(game, "PlayCombatCard", 0); SettleMimicrator(game);
                    TestSuite.Check(fight.CardsPlayedThisTurn == 1 && fight.Hero.Money == money - 10, "sole card paid once");
                    Call(game, "PlayCombatCard", 0); SettleMimicrator(game);
                    TestSuite.Check(fight.CardsPlayedThisTurn == 1 && fight.Hero.Money == money - 10, "UI rejects second play");
                    Call(game, "EndCombatTurn");
                    TestSuite.Check(Read<AttackTimeline>(game, "attackTimeline").Kind == AttackAnimation.MimicratorBear, "bear action animation");
                    SettleMimicrator(game);
                    // The sole card may have granted five block.
                    TestSuite.Check(fight.Turn == 4 && !fight.IsStunned && fight.Hero.Health >= 25 &&
                        Read<MimicratorForm>(game, "mimicratorVisualForm") == MimicratorForm.Mussel, "bear hit and shell form");
                    int health = fight.Hero.Health;
                    Call(game, "EndCombatTurn");
                    TestSuite.Check(Read<AttackTimeline>(game, "attackTimeline").Kind == AttackAnimation.MimicratorShell, "shell action animation");
                    Call(game, "AdvanceCombatAnimation", .56f);
                    using (var frame = Render(game))
                        if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-shell-guard.png"));
                    SettleMimicrator(game);
                    TestSuite.Check(fight.Turn == 5 && fight.Hero.Health == health && fight.Enemies[0].Block == 12,
                        "shield without phantom hit");
                    var killer = fight.Hand[0]; killer.Attack = true; killer.Skill = false;
                    killer.Damage = 999; killer.Block = 0; killer.GoldCost = 0; killer.ManaCost = 0;
                    Call(game, "PlayCombatCard", 0); Call(game, "AdvanceCombatAnimation", .5f);
                    TestSuite.Check(fight.Phase == CombatPhase.Victory, "kill transformed enemy");
                    using (var frame = Render(game))
                        if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-death.png"));
                    SettleMimicrator(game);
                    TestSuite.Check(Read<DeathVisuals>(game, "deathVisuals").SnapshotCount == 0, "death image released");
                    Call(game, "CompleteMapRoom");
                    TestSuite.Check(Read<MapRun>(game, "mapRun").Pending == null, "victory unlocks map");
                    TestSuite.Check(Read<ImageAssets>(game, "assetStore").Count == 36, "three source layers shared");
                }
        }


        public static void TestMimicratorEliteRoom()
        {
            using (var game = new Game())
            {
                game.CreateControl();
                Write(game, "selected", 2); Call(game, "OpenCheckout");
                Write(game, "buffCursor", 2); Call(game, "TakeBuff"); Call(game, "OpenActMap");
                var run = Read<MapRun>(game, "mapRun");
                var elite = run.Map.Nodes.First(n => n.Kind == RoomKind.Elite);
                var parents = new System.Collections.Generic.Dictionary<int, int>();
                var queue = new System.Collections.Generic.Queue<int>();
                queue.Enqueue(0); parents[0] = -1;
                while (queue.Count > 0)
                {
                    int id = queue.Dequeue();
                    foreach (int next in run.Map.Nodes[id].Exits)
                        if (!parents.ContainsKey(next)) { parents[next] = id; queue.Enqueue(next); }
                }
                var route = new System.Collections.Generic.List<int>();
                for (int id = parents[elite.Id]; id > 0; id = parents[id]) route.Add(id);
                route.Reverse();
                foreach (int id in route) { TestSuite.Check(run.Enter(id), "routing prefix"); run.CompleteRoom(); }
                Call(game, "EnterMapNode", elite.Id);
                var fight = Read<CombatState>(game, "combat");
                TestSuite.Check(fight != null && fight.Enemy == EnemyKind.Mimicrator, "elite node starts real Mimicrator");
                var intel = Read<System.Collections.Generic.Dictionary<int, IntelNode>>(game, "mapIntel");
                TestSuite.Check(intel[elite.Id].Identity == "Мимикратор", "investigation identifies elite");
                Call(game, "CompleteMapRoom");
                TestSuite.Check(run.Pending == elite, "cannot skip living elite");
                using (var frame = Render(game))
                    frame.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-elite-room.png"));
                var killer = fight.Hand[0]; killer.Attack = true; killer.Skill = false;
                killer.Damage = 999; killer.Block = 0; killer.ManaCost = 0;
                Call(game, "PlayCombatCard", 0); SettleMimicrator(game);
                Call(game, "CompleteMapRoom");
                TestSuite.Check(run.Current == elite && run.Pending == null, "elite victory advances route");
                TestSuite.Check(run.ReopenCurrent(), "revisit completed elite");
                using (var frame = Render(game)) { }
                TestSuite.Check(Read<CombatState>(game, "combat") == null, "revisit does not resurrect elite");
                Call(game, "CompleteMapRoom");
                TestSuite.Check(run.Pending == null, "revisit closes");
            }
        }

        public static void TestMimicratorAnimationSheet()
        {
            using (var game = new Game())
            using (var sheet = new Bitmap(1920, 2520))
            using (var graphics = Graphics.FromImage(sheet))
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                Call(game, "StartTestMimicrator");
                int cell = 0;
                for (int round = 0; round < 4; round++)
                {
                    Call(game, "EndCombatTurn");
                    // Advance before impact to capture the actual action pose.
                    Call(game, "AdvanceCombatAnimation", round == 0 ? .35f : round == 1 ? .43f : round == 2 ? .53f : .46f);
                    using (var frame = Render(game))
                        graphics.DrawImage(frame, cell % 2 * 960, cell / 2 * 630, 960, 630);
                    cell++;
                    SettleMimicrator(game);
                    using (var frame = Render(game))
                        graphics.DrawImage(frame, cell % 2 * 960, cell / 2 * 630, 960, 630);
                    cell++;
                }
                sheet.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-forms-review.png"));
                TestSuite.Check(true, "four actions and transformations captured");
            }
        }
    }
}
