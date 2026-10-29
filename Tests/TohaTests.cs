using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace ShpilSvoih
{
    public static class TohaTests
    {
        static CombatState Fight(int damage)
        {
            var hero = new HeroState(3, 1000, 0, new Balance { MoneyLostPerTurn = 0 });
            hero.Effects.Choose("ring");
            for (int i = 0; i < 8; i++) hero.AddCard(new CardData { Name = "Test", Attack = true, Damage = damage });
            return new CombatState(hero, new Random(7), EnemyKind.Toha);
        }

        public static void Rules()
        {
            var f = Fight(69);
            var enemy = f.Enemies[0];
            TestSuite.Check(enemy.Health == 100 && !enemy.IsDarkToha && enemy.AttackDamage == 10, "Toha starts light at full HP");
            f.TryPlay(0);
            TestSuite.Check(enemy.Health == 31 && !enemy.IsDarkToha, "31 percent does not transform");
            f.Hand[0].Damage = 1; f.TryPlay(0);
            TestSuite.Check(enemy.Health == 30 && enemy.IsDarkToha && enemy.AttackDamage == 16, "exactly 30 percent transforms immediately without healing");
            TestSuite.Check(CombatHints.Intent(enemy)[0].Number == "16", "dark intent uses the actual stronger attack");
            int health = f.Hero.Health;
            f.EndPlayerTurn(); f.ResolveEnemyTurn();
            TestSuite.Check(f.Hero.Health == health - 16 && enemy.IsDarkToha && enemy.Health == 30, "dark ram and permanent form persist across turns");
            f = Fight(71); f.TryPlay(0);
            TestSuite.Check(f.Enemies[0].Health == 29 && f.Enemies[0].IsDarkToha, "skipping past threshold also transforms");
            f = Fight(100); f.TryPlay(0);
            TestSuite.Check(f.Phase == CombatPhase.Victory && !f.Enemies[0].IsDarkToha && !f.EndPlayerTurn(), "lethal hit does not resurrect or transform the dead enemy");
            f = Fight(1);
            f.Hand[0].Attack = false; f.Hand[0].Skill = true; f.Hand[0].Block = 100; f.TryPlay(0);
            health = f.Hero.Health; f.EndPlayerTurn(); f.ResolveEnemyTurn();
            TestSuite.Check(f.Hero.Health == health && f.Block == 0, "ram respects and consumes block normally");
            var kinds = new HashSet<EnemyKind>();
            for (int seed = 0; seed < 200; seed++)
                foreach (var node in ActMap.Generate(seed).Nodes.Where(n => n.Kind == RoomKind.Elite))
                {
                    var kind = EnemyCatalog.ForEliteNode(seed, node.Id); kinds.Add(kind);
                    TestSuite.Check(kind == EnemyCatalog.ForEliteNode(seed, node.Id), "elite selection is deterministic");
                }
            TestSuite.Check(kinds.SetEquals(new[] { EnemyKind.Toha, EnemyKind.Mimicrator }), "both elite encounters are generated");
        }

        public static void Motion()
        {
            foreach (var kind in new[] { AttackAnimation.TohaRam, AttackAnimation.TohaDarkRam, AttackAnimation.TohaTransform })
                foreach (int hz in new[] { 10, 30, 60, 120 })
                {
                    int hits = 0; var timeline = new AttackTimeline(); timeline.Start(kind, () => hits++);
                    float duration = timeline.Duration;
                    TestSuite.Check(TohaMotion.Drive(0, timeline.HitTime, duration) == 0 &&
                        Math.Abs(TohaMotion.Drive(timeline.HitTime, timeline.HitTime, duration) - 1) < .00001f &&
                        TohaMotion.Drive(duration, timeline.HitTime, duration) == 0, "ram begins at home, reaches target at impact and returns home");
                    int frames = 0;
                    while (timeline.IsRunning)
                    {
                        timeline.Advance(1f / hz); frames++;
                        TestSuite.Check(hits <= 1 && frames < hz * 3, "bounded animation with one impact");
                    }
                    TestSuite.Check(hits == 1 && Math.Abs(frames / (float)hz - duration / AttackTimeline.Speed) <= 1.01f / hz,
                        "consistent elapsed duration across frame rates");
                    timeline.Advance(10); TestSuite.Check(hits == 1, "finished impact does not repeat");
                }
        }
    }

    public static partial class UiTests
    {
        static void SettleToha(Game game)
        {
            int steps = 0;
            while ((bool)typeof(Game).GetProperty("CombatBusy", PrivateInstance).GetValue(game, null) && steps++ < 600)
                Call(game, "AdvanceCombatAnimation", 1f / 60);
            TestSuite.Check(steps < 600, "Toha animation queue completes");
        }

        public static void TestIllyaToha()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                Call(game, "StartTestIllyaToha");
                SettleToha(game);
                var fight = Read<CombatState>(game, "combat");
                TestSuite.Check(fight.Enemies.Count == 1 && fight.Enemies[0].Kind == EnemyKind.Toha, "Illya test opens Toha encounter");
                TestSuite.Check(fight.CardsPlayedThisTurn == 2 && fight.Energy == 3 && fight.Hand.Count == 3, "Illya draws five and automatically plays two free cards");
                TestSuite.Check(fight.Hand.All(c => fight.Cost(c) >= 0 && fight.Cost(c) <= 4), "Illya hand uses confusion prices");
                using (var image = Render(game)) image.Save(Path.Combine(TestSuite.OutputDirectory, "illya-vs-toha.png"));
                Call(game, "ReturnToMainMenu");
                TestSuite.Check(Read<Bitmap>(game, "tohaDayFrame") == null, "Illya test cleans up Toha scene on exit");
            }
        }

        public static void TestTohaCombat()
        {
            foreach (var size in Sizes)
                using (var game = new Game())
                {
                    game.CreateControl(); game.ClientSize = size; Call(game, "StartTestToha");
                    var f = Read<CombatState>(game, "combat");
                    using (var image = Render(game)) image.Save(Path.Combine(TestSuite.OutputDirectory, "toha-light-" + size.Width + "x" + size.Height + ".png"));
                    TestSuite.Check(Read<ImageAssets>(game, "assetStore").Count == 42, "Toha lazily loads four shared assets");
                    foreach (string source in new[] { "tohaLight", "tohaDark" })
                    {
                        var sprite = (Bitmap)Read<Image>(game, source);
                        TestSuite.Check(sprite.GetPixel(100, 100).A == 0, "sprite has no translucent background haze");
                    }
                    var dayFrame = Read<Bitmap>(game, "tohaDayFrame");
                    using (Render(game)) { }
                    TestSuite.Check(ReferenceEquals(dayFrame, Read<Bitmap>(game, "tohaDayFrame")), "idle reuses prepared backdrops");
                    string before;
                    using (var image = Render(game)) before = Digest(image);
                    Call(game, "AdvanceLivingAnimation", .5f);
                    using (var image = Render(game)) TestSuite.Check(before != Digest(image), "idle suspension and exhaust are animated");
                    Call(game, "EndCombatTurn");
                    var timeline = Read<AttackTimeline>(game, "attackTimeline");
                    TestSuite.Check(timeline.Kind == AttackAnimation.TohaRam, "light form uses its ram");
                    Call(game, "AdvanceCombatAnimation", (timeline.HitTime - .01f) / AttackTimeline.Speed);
                    TestSuite.Check(f.Hero.Health == 50, "light ram does not damage before contact");
                    if (size == Sizes[2]) using (var image = Render(game)) image.Save(Path.Combine(TestSuite.OutputDirectory, "toha-light-ram.png"));
                    Call(game, "SetInventory", "pause"); float time = timeline.Time;
                    Call(game, "AdvanceCombatAnimation", 4f);
                    TestSuite.Check(timeline.Time == time && f.Hero.Health == 50, "pause freezes ram and damage");
                    Call(game, "SetInventory", ""); SettleToha(game);
                    TestSuite.Check(f.Hero.Health == 40 && f.Turn == 2, "light ram damages exactly once");
                    var card = f.Hand[0]; card.Attack = true; card.Skill = false; card.Block = 0; card.Damage = 70; card.GoldCost = 0;
                    Call(game, "PlayCombatCard", 0);
                    Call(game, "AdvanceCombatAnimation", .51f / AttackTimeline.Speed);
                    TestSuite.Check(f.Enemies[0].IsDarkToha && f.Enemies[0].Health == 30 && !Read<bool>(game, "tohaVisualDark"), "rules switch at hit while visual transformation waits for current animation");
                    Call(game, "AdvanceCombatAnimation", .46f / AttackTimeline.Speed);
                    TestSuite.Check(timeline.Kind == AttackAnimation.TohaTransform, "transformation queues once after the hero attack");
                    Call(game, "SetInventory", "pause"); time = timeline.Time;
                    Call(game, "AdvanceCombatAnimation", 4f);
                    TestSuite.Check(timeline.Time == time, "pause freezes transformation");
                    Call(game, "SetInventory", "");
                    Call(game, "AdvanceCombatAnimation", .3f);
                    using (var image = Render(game)) if (size == Sizes[2]) image.Save(Path.Combine(TestSuite.OutputDirectory, "toha-transform.png"));
                    var blend = Read<object>(game, "tohaBlend");
                    TestSuite.Check(blend != null, "transition owns its native backdrop copies");
                    using (Render(game)) { }
                    TestSuite.Check(ReferenceEquals(blend, Read<object>(game, "tohaBlend")), "fade reuses its copies across frames");
                    SettleToha(game);
                    TestSuite.Check(Read<bool>(game, "tohaVisualDark") && f.Phase == CombatPhase.Player && f.Enemies[0].Health == 30, "transformation preserves HP and player turn");
                    using (var image = Render(game)) image.Save(Path.Combine(TestSuite.OutputDirectory, "toha-dark-" + size.Width + "x" + size.Height + ".png"));
                    TestSuite.Check(Read<object>(game, "tohaBlend") == null, "native backdrop copies are released after transformation");
                    int hp = f.Hero.Health; Call(game, "EndCombatTurn");
                    TestSuite.Check(timeline.Kind == AttackAnimation.TohaDarkRam, "dark form uses stronger ram animation");
                    Call(game, "AdvanceCombatAnimation", timeline.HitTime / AttackTimeline.Speed);
                    TestSuite.Check(f.Hero.Health == hp - 16, "dark impact deals sixteen once");
                    if (size == Sizes[2]) using (var image = Render(game)) image.Save(Path.Combine(TestSuite.OutputDirectory, "toha-dark-ram.png"));
                    SettleToha(game); TestSuite.Check(f.Hero.Health == hp - 16, "no repeated dark impact");
                    card = f.Hand[0]; card.Attack = true; card.Skill = false; card.Block = 0; card.Damage = 999; card.GoldCost = 0;
                    Call(game, "PlayCombatCard", 0); Call(game, "AdvanceCombatAnimation", .51f / AttackTimeline.Speed);
                    TestSuite.Check(f.Phase == CombatPhase.Victory && Read<DeathVisuals>(game, "deathVisuals").SnapshotCount == 1, "Toha has a death snapshot");
                    SettleToha(game);
                    Call(game, "ReturnToMainMenu");
                    TestSuite.Check(Read<Bitmap>(game, "tohaDayFrame") == null && Read<Bitmap>(game, "tohaNightFrame") == null, "leaving fight frees both viewport-sized backdrops");
                    TestSuite.Throws<ArgumentException>(() => dayFrame.GetPixel(0, 0), "released backdrop is disposed");
                }
        }
        public static void TestTohaEliteRoom()
        {
            using (var game = new Game())
            {
                game.CreateControl(); MapRun run = null; MapNode elite = null;
                for (int attempt = 0; attempt < 30 && elite == null; attempt++)
                {
                    Call(game, "ReturnToMainMenu"); Write(game, "selected", 3);
                    Call(game, "OpenCheckout"); Call(game, "TakeBuff"); Call(game, "OpenActMap");
                    run = Read<MapRun>(game, "mapRun");
                    elite = run.Map.Nodes.FirstOrDefault(n => n.Kind == RoomKind.Elite && EnemyCatalog.ForEliteNode(run.Map.Seed, n.Id) == EnemyKind.Toha);
                }
                TestSuite.Check(elite != null, "procedural map contains a Toha elite encounter");
                var parents = new Dictionary<int, int>(); var queue = new Queue<int>(); queue.Enqueue(0); parents[0] = -1;
                while (queue.Count > 0)
                {
                    int id = queue.Dequeue();
                    foreach (int next in run.Map.Nodes[id].Exits)
                        if (!parents.ContainsKey(next)) { parents[next] = id; queue.Enqueue(next); }
                }
                var route = new List<int>();
                for (int id = parents[elite.Id]; id > 0; id = parents[id]) route.Add(id);
                route.Reverse();
                foreach (int id in route) { TestSuite.Check(run.Enter(id), "legal prefix to elite"); run.CompleteRoom(); }
                var intel = Read<Dictionary<int, IntelNode>>(game, "mapIntel");
                TestSuite.Check(intel[elite.Id].Identity == "Тоха", "investigation matches generated Toha before entering");
                Call(game, "EnterMapNode", elite.Id);
                var fight = Read<CombatState>(game, "combat");
                TestSuite.Check(fight.Enemy == EnemyKind.Toha, "actual elite room spawns Toha without an override");
                using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "toha-elite-room.png"));
                var oldBackdrop = Read<Bitmap>(game, "tohaDayFrame");
                game.ClientSize = new Size(1920, 1080);
                using (Render(game)) { }
                TestSuite.Throws<ArgumentException>(() => oldBackdrop.GetPixel(0, 0), "resizing releases the previous viewport background");
                TestSuite.Check(Read<ImageAssets>(game, "assetStore").Count == 42, "resizing reuses the four decoded source images");
                var card = fight.Hand[0]; card.Attack = true; card.Skill = false; card.Damage = 999; card.Block = 0; card.GoldCost = 0;
                Call(game, "PlayCombatCard", 0); SettleToha(game); Call(game, "CompleteMapRoom");
                TestSuite.Check(run.Current == elite && run.Pending == null, "defeating Toha completes the elite room");
                TestSuite.Check(run.ReopenCurrent(), "completed elite can be revisited");
                using (Render(game)) { }
                TestSuite.Check(Read<CombatState>(game, "combat") == null, "revisit never resurrects Toha");
            }
        }
    }
}
