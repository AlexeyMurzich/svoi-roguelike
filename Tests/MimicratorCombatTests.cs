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
            TestSuite.Check(enemy.Health == 60, "elite HP");
            for (int round = 0; round < 12; round++)
            {
                var expected = (MimicratorForm)(round % 4);
                int health = fight.Hero.Health;
                TestSuite.Check(enemy.NextForm == expected, "full four-form cycle");
                TestSuite.Check(fight.EndPlayerTurn(), "end player turn");
                TestSuite.Check(fight.PrepareNextEnemyAction() && enemy.Form == expected, "form entered during enemy turn");
                TestSuite.Check(!fight.PrepareNextEnemyAction(), "entry is idempotent");
                TestSuite.Check(fight.Hero.Health == health, "entry itself never applies attack damage");
                TestSuite.Check(fight.StunPending == (expected == MimicratorForm.Beacon), "stun scheduled at entry for next turn");
                TestSuite.Check(enemy.Block == (expected == MimicratorForm.Mussel ? 12 : 0), "shell block immediate at entry");
                int hits = expected == MimicratorForm.Mussel ? 2 : 1;
                int hit = expected == MimicratorForm.Fairy ? 0 : expected == MimicratorForm.Beacon ? 6 :
                    expected == MimicratorForm.Bear ? 14 : 5;
                for (int i = 0; i < hits; i++)
                {
                    TestSuite.Check(fight.ResolveNextEnemyImpact(), "resolve one impact");
                    TestSuite.Check(fight.Hero.Health == health - hit * (i + 1), "individual impact damage");
                    if (i + 1 < hits) TestSuite.Check(fight.Phase == CombatPhase.Enemy &&
                        fight.EnemyImpactsResolved == 1, "second shell impact remains queued");
                }
                TestSuite.Check(fight.Phase == CombatPhase.Player && enemy.Form == expected, "no transformation between player turns");
                TestSuite.Check(fight.IsStunned == (expected == MimicratorForm.Beacon), "one-card next player turn");
                TestSuite.Check(!fight.ResolveNextEnemyAction(), "completed action cannot replay");
            }

            fight = new CombatState(Hero(), new Random(2), EnemyKind.Mimicrator);
            End(fight); End(fight);
            TestSuite.Check(fight.IsStunned && fight.Hand.Count == 3, "stun preserves draw");
            TestSuite.Check(fight.TryPlay(0) && fight.CardsPlayedThisTurn == 1, "one zero-cost skill allowed");
            int hand = fight.Hand.Count;
            TestSuite.Check(!fight.TryPlay(0) && fight.Hand.Count == hand, "cannot bypass play limit");
            End(fight);
            TestSuite.Check(!fight.IsStunned && fight.CanPlay(0), "stun expires after one player turn");
            TestSuite.Check(!new CombatState(fight.Hero, new Random(2)).IsStunned, "new battle clears stun");

            fight = new CombatState(Hero(1), new Random(4), EnemyKind.Mimicrator);
            End(fight); End(fight);
            TestSuite.Check(fight.IsStunned && fight.CardsPlayedThisTurn == 1 && fight.Hand.Count == 4 &&
                !fight.CanPlay(0), "Illya automatic card consumes sole play");

            fight = new CombatState(Hero(2, 30), new Random(5), EnemyKind.Mimicrator);
            End(fight); fight.TryPlay(0); End(fight);
            TestSuite.Check(fight.LastDamage == 0 && fight.IsStunned, "blocked beacon still stuns next turn");

            fight = new CombatState(Hero(2, 7), new Random(6), EnemyKind.Mimicrator);
            End(fight); End(fight); End(fight);
            fight.TryPlay(0);
            int before = fight.Hero.Health;
            fight.EndPlayerTurn(); fight.PrepareNextEnemyAction();
            TestSuite.Check(fight.Enemies[0].Block == 12 && fight.Block == 7, "shell gets own block before first hit");
            fight.ResolveNextEnemyImpact();
            TestSuite.Check(fight.Hero.Health == before && fight.Block == 2 && fight.Phase == CombatPhase.Enemy,
                "first five-damage hit spends block separately");
            fight.ResolveNextEnemyImpact();
            TestSuite.Check(fight.Hero.Health == before - 3 && fight.Turn == 5, "second hit uses remaining shared block");

            fight = new CombatState(Hero(2, 0, 6), new Random(7), EnemyKind.Mimicrator);
            for (int i = 0; i < 4; i++) End(fight);
            TestSuite.Check(fight.Enemies[0].Block == 12 && fight.Hero.Health == 970, "two shell hits, no fairy damage");
            fight.TryPlay(0);
            TestSuite.Check(fight.EnemyHealth == 60 && fight.Enemies[0].Block == 6, "shell block absorbs card");
            fight.EndPlayerTurn(); fight.PrepareNextEnemyAction();
            TestSuite.Check(fight.Enemies[0].Form == MimicratorForm.Fairy && fight.Enemies[0].Block == 0,
                "returning fairy clears old block at entry");
            before = fight.Hero.Health; fight.ResolveNextEnemyAction();
            TestSuite.Check(fight.Hero.Health == before, "returning fairy rests without damage or healing");

            var doomed = new HeroState(2, 21, 0, new Balance()); doomed.Effects.Choose("feather");
            fight = new CombatState(doomed, new Random(8), EnemyKind.Mimicrator);
            End(fight); End(fight); End(fight);
            fight.EndPlayerTurn(); fight.PrepareNextEnemyAction(); fight.ResolveNextEnemyImpact();
            TestSuite.Check(fight.Phase == CombatPhase.Defeat && fight.EnemyImpactsResolved == 1 &&
                !fight.ResolveNextEnemyImpact(), "death after first shell hit cancels second");

            var protectedHero = Hero(0);
            fight = new CombatState(protectedHero, new Random(9), EnemyKind.Mimicrator);
            typeof(CombatEnemy).GetProperty("ActionsTaken").SetValue(fight.Enemies[0], 1, null);
            fight.EndPlayerTurn(); fight.PrepareNextEnemyAction();
            TestSuite.Check(!fight.StunPending, "first-turn relic rejects entry debuff");
            fight.ResolveNextEnemyAction();
            TestSuite.Check(protectedHero.Health == 1000 && !fight.IsStunned, "relic also rejects beacon damage");
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
                    using (var frame = Render(game))
                        if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-fairy-battle.png"));
                    Call(game, "EndCombatTurn");
                    TestSuite.Check(Read<AttackTimeline>(game, "attackTimeline").Kind == AttackAnimation.MimicratorRest, "first fairy rests");
                    Call(game, "AdvanceCombatAnimation", .15f);
                    Key(game, Keys.Escape); Call(game, "AdvanceCombatAnimation", 1f);
                    TestSuite.Check(fight.Hero.Health == 50 && fight.Turn == 1, "paused rest does not advance");
                    Key(game, Keys.Escape); SettleMimicrator(game);
                    TestSuite.Check(fight.Hero.Health == 50 && fight.Turn == 2 &&
                        Read<MimicratorForm>(game, "mimicratorVisualForm") == MimicratorForm.Fairy,
                        "no transform at end of fairy turn");

                    Call(game, "EndCombatTurn");
                    TestSuite.Check(Read<AttackTimeline>(game, "attackTimeline").Kind == AttackAnimation.MimicratorTransform &&
                        !fight.EnemyActionPrepared && !fight.StunPending, "next turn starts with transformation");
                    Call(game, "AdvanceCombatAnimation", .36f);
                    TestSuite.Check(fight.EnemyActionPrepared && fight.StunPending && fight.Hero.Health == 50 &&
                        fight.Phase == CombatPhase.Enemy && Read<MimicratorForm>(game, "mimicratorVisualForm") == MimicratorForm.Beacon,
                        "beacon entry schedules stun before attack");
                    using (var frame = Render(game))
                        if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-entry-stun.png"));
                    SettleMimicrator(game);
                    TestSuite.Check(fight.Hero.Health == 44 && fight.Turn == 3 && fight.IsStunned &&
                        Read<MimicratorForm>(game, "mimicratorVisualForm") == MimicratorForm.Beacon, "same-turn beacon hit; remains beacon");
                    using (var before = Render(game))
                    {
                        Call(game, "AdvanceCombatAmbient", .2f);
                        using (var after = Render(game))
                            TestSuite.Check(Digest(before) != Digest(after), "birds animate");
                    }
                    decimal money = fight.Hero.Money;
                    Call(game, "PlayCombatCard", 0); SettleMimicrator(game);
                    Call(game, "PlayCombatCard", 0); SettleMimicrator(game);
                    TestSuite.Check(fight.CardsPlayedThisTurn == 1 && fight.Hero.Money == money - 10, "exactly one paid play");
                    int health = fight.Hero.Health;
                    Call(game, "EndCombatTurn");
                    TestSuite.Check(Read<AttackTimeline>(game, "attackTimeline").Kind == AttackAnimation.MimicratorTransform, "bear transforms at start of own turn");
                    SettleMimicrator(game);
                    TestSuite.Check(fight.Turn == 4 && fight.Hero.Health >= health - 14 &&
                        fight.Hero.Health <= health - 9 && !fight.IsStunned &&
                        Read<MimicratorForm>(game, "mimicratorVisualForm") == MimicratorForm.Bear, "bear hits in same enemy turn");
                    health = fight.Hero.Health;
                    Call(game, "EndCombatTurn"); Call(game, "AdvanceCombatAnimation", .36f);
                    TestSuite.Check(fight.Enemies[0].Block == 12 && fight.Hero.Health == health &&
                        fight.EnemyImpactsResolved == 0, "shell immediately gets block at entry");
                    using (var frame = Render(game))
                        if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-shell-entry-block.png"));
                    Call(game, "AdvanceCombatAnimation", .4f);
                    TestSuite.Check(Read<AttackTimeline>(game, "attackTimeline").Kind == AttackAnimation.MimicratorShell, "first shell clack queued");
                    Call(game, "AdvanceCombatAnimation", .5f);
                    TestSuite.Check(fight.Hero.Health == health - 5 && fight.EnemyImpactsResolved == 1 &&
                        fight.Phase == CombatPhase.Enemy, "first shell hit applies once");
                    using (var frame = Render(game))
                        if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-shell-first-hit.png"));
                    Key(game, Keys.Escape); Call(game, "AdvanceCombatAnimation", 3f);
                    TestSuite.Check(fight.Hero.Health == health - 5, "pause between two impacts");
                    Key(game, Keys.Escape); SettleMimicrator(game);
                    TestSuite.Check(fight.Hero.Health == health - 10 && fight.Turn == 5 &&
                        Read<MimicratorForm>(game, "mimicratorVisualForm") == MimicratorForm.Mussel,
                        "second five-damage hit, no premature transform");
                    health = fight.Hero.Health;
                    Call(game, "EndCombatTurn"); SettleMimicrator(game);
                    TestSuite.Check(fight.Hero.Health == health && fight.Turn == 6 &&
                        Read<MimicratorForm>(game, "mimicratorVisualForm") == MimicratorForm.Fairy &&
                        fight.Enemies[0].Block == 0, "returns to original fairy and rests");
                    using (var frame = Render(game))
                        if (size == Sizes[2]) frame.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-return-rest.png"));
                    var killer = fight.Hand[0]; killer.Attack = true; killer.Skill = false;
                    killer.Damage = 999; killer.Block = 0; killer.GoldCost = 0; killer.ManaCost = 0;
                    Call(game, "PlayCombatCard", 0); SettleMimicrator(game);
                    TestSuite.Check(fight.Phase == CombatPhase.Victory &&
                        Read<DeathVisuals>(game, "deathVisuals").SnapshotCount == 0, "death ends loop and releases snapshot");
                    Call(game, "CompleteMapRoom");
                    TestSuite.Check(Read<MapRun>(game, "mapRun").Pending == null, "victory opens map");
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
            using (var sheet = new Bitmap(1920, 3150))
            using (var graphics = Graphics.FromImage(sheet))
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                Call(game, "StartTestMimicrator");
                int cell = 0;
                for (int round = 0; round < 5; round++)
                {
                    Call(game, "EndCombatTurn");
                    if (round > 0) Call(game, "AdvanceCombatAnimation", .72f);
                    Call(game, "AdvanceCombatAnimation", round == 2 ? .53f : .4f);
                    using (var frame = Render(game))
                        graphics.DrawImage(frame, cell % 2 * 960, cell / 2 * 630, 960, 630);
                    cell++;
                    SettleMimicrator(game);
                    using (var frame = Render(game))
                        graphics.DrawImage(frame, cell % 2 * 960, cell / 2 * 630, 960, 630);
                    cell++;
                }
                sheet.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-forms-review.png"));
                TestSuite.Check(true, "five turns including return to fairy captured");
            }
        }
    }
}
