using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace ShpilSvoih
{
    public static class BabaiTests
    {
        static CombatState Fight(bool shield)
        {
            var hero = new HeroState(3, 1000, 0, new Balance { MoneyLostPerTurn = 0, BaseDraw = 1 });
            hero.Effects.Choose("ring");
            hero.AddCard(new CardData { Name = "Shield", Skill = true, Block = shield ? 10000 : 0, ManaCost = 0, GoldCost = 0 });
            return new CombatState(hero, new Random(1), EnemyKind.Babai);
        }
        public static void Rules()
        {
            var fight = Fight(true);
            TestSuite.Check(fight.EnemyHealth == 40 && fight.Babai.Distance == 5 && fight.Enemies[0].AttackDamage == 10, "approved HP, damage and initial distance");
            for (int turn = 1; turn <= 5; turn++)
            {
                fight.TryPlay(0); int hp = fight.Hero.Health;
                TestSuite.Check(fight.EndPlayerTurn(), "ordinary turn can end");
                TestSuite.Check(fight.Babai.Distance == 6 - turn, "ending player turn does not pull early");
                fight.PrepareNextEnemyAction();
                TestSuite.Check(fight.Babai.Distance == 6 - turn, "preparing lasso does not pull early");
                fight.ResolveNextEnemyImpact();
                TestSuite.Check(fight.Babai.Distance == 5 - turn, "one hit pulls exactly one through full block");
                if (turn < 5) TestSuite.Check(fight.Hero.Health == hp && fight.Phase == CombatPhase.Player, "block absorbs damage but not pull");
            }
            TestSuite.Check(fight.Babai.Caught && fight.Hero.Health == 0 && fight.Block == 0 && fight.Phase == CombatPhase.Defeat, "fifth pull kills regardless of HP/block");
            TestSuite.Check(!fight.ResolveNextEnemyImpact() && !fight.EndPlayerTurn() && fight.Babai.Distance == 0, "capture is terminal and cannot repeat");
            fight = Fight(false); fight.EndPlayerTurn(); fight.ResolveEnemyTurn();
            TestSuite.Check(fight.Hero.Health == 990 && fight.Babai.Distance == 4, "unblocked lasso does ten damage and one pull");
            var hero = new HeroState(3, 1000, 0, new Balance { BaseDraw = 1 }); hero.Effects.Choose("ring");
            hero.AddCard(new CardData { Name = "Lethal", Attack = true, Damage = 40, GoldCost = 0 });
            var lethal = new CombatState(hero, new Random(1), EnemyKind.Babai); lethal.TryPlay(0);
            TestSuite.Check(lethal.Phase == CombatPhase.Victory && lethal.Babai.Distance == 5 && !lethal.EndPlayerTurn(), "killing Babai cancels all future pulls");
            TestSuite.Check(new CombatState(hero, new Random(1), EnemyKind.Rat).Babai == null, "other encounters have no trap");
        }
        public static void Pools()
        {
            for (int seed = 0; seed < 200; seed++)
            {
                var history = new List<EnemyKind>();
                for (int count = 0; count < 3; count++)
                {
                    var easy = OrdinaryEncounters.Select(seed, count + 1, history);
                    TestSuite.Check(easy != EnemyKind.Babai && !history.Contains(easy), "first three fights exclude hard-only enemy and prior easy enemies");
                    history.Add(easy);
                }
                for (int count = 3; count < 20; count++)
                {
                    var hard = OrdinaryEncounters.Select(seed, count + 1, history);
                    TestSuite.Check(hard == EnemyKind.Babai, "temporary hard pool repeats only Babai after the easy pool");
                    history.Add(hard);
                }
                TestSuite.Check(EnemyCatalog.ForEliteNode(seed, 1) != EnemyKind.Babai, "Babai never replaces an elite");
            }
        }
        public static void Motion()
        {
            foreach (int hz in new[] { 10, 30, 60, 120 })
            {
                int impacts = 0; var timeline = new AttackTimeline(); timeline.Start(AttackAnimation.BabaiLasso, () => impacts++);
                int frames = 0;
                while (timeline.IsRunning) { timeline.Advance(1f / hz); frames++; TestSuite.Check(impacts <= 1, "one callback regardless of frame rate"); }
                TestSuite.Check(impacts == 1 && Math.Abs(frames / (float)hz - BabaiMotion.Duration / AttackTimeline.Speed) < 1.01f / hz, "consistent lasso duration");
            }
            TestSuite.Check(BabaiMotion.RopeExtension(0) == 0 && BabaiMotion.RopeExtension(BabaiMotion.Hit) == 1 && BabaiMotion.RopeExtension(BabaiMotion.Duration) == 0, "lasso reaches at impact and retracts completely");
            float extension = 0;
            for (int sample = 0; sample <= 100; sample++)
            {
                float next = BabaiMotion.RopeExtension(BabaiMotion.Hit * sample / 100);
                TestSuite.Check(next >= extension && next <= 1, "rope extension is continuous and monotonic before impact"); extension = next;
            }
            float previous = 1;
            for (int frame = 0; frame <= 100; frame++)
            {
                float time = BabaiMotion.Hit + BabaiMotion.PullDuration * frame / 100;
                float distance = BabaiMotion.DisplayDistance(0, 1, true, true, time);
                TestSuite.Check(distance <= previous + .00001f && distance >= 0, "capture moves smoothly toward bed without reversal"); previous = distance;
            }
            TestSuite.Check(previous == 0 && BabaiMotion.DisplayDistance(4, 4, true, true, 1) == 4, "capture ends at zero; lethal damage without pull does not move backward");
        }
    }
    public static partial class UiTests
    {
        static void SettleBabai(Game game)
        {
            int frames = 0;
            while ((bool)typeof(Game).GetProperty("CombatBusy", PrivateInstance).GetValue(game, null) && frames++ < 500)
                Call(game, "AdvanceCombatAnimation", 1f / 60);
            TestSuite.Check(frames < 500, "Babai animation queue terminates");
        }
        public static void TestBabaiBattle()
        {
            foreach (var size in Sizes)
                using (var game = new Game())
                {
                    game.CreateControl(); game.ClientSize = size; Call(game, "StartTestBabai");
                    var f = Read<CombatState>(game, "combat"); var t = Read<AttackTimeline>(game, "attackTimeline");
                    foreach (var c in f.Hero.Deck) { c.Attack = false; c.Skill = true; c.Block = 10000; c.GoldCost = 0; }
                    using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "babai-idle-" + size.Width + "x" + size.Height + ".png"));
                    int images = Read<ImageAssets>(game, "assetStore").Count;
                    for (int turn = 1; turn <= 5; turn++)
                    {
                        if (turn == 2)
                        {
                            var counter = f.Hand[0]; counter.Attack = true; counter.Skill = false; counter.Damage = 0;
                            Call(game, "PlayCombatCard", 0); Call(game, "AdvanceCombatAnimation", .32f);
                            float spriteX = (float)typeof(Game).GetProperty("HeroSpriteCenter", PrivateInstance).GetValue(game, null);
                            TestSuite.Check(spriteX > game.DrawingViewport.Width * .24f, "counterattack follows the pulled hero position");
                            using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "babai-counterattack-" + size.Width + ".png"));
                        }
                        else Call(game, "PlayCombatCard", 0);
                        SettleBabai(game); Call(game, "EndCombatTurn");
                        TestSuite.Check(t.Kind == AttackAnimation.BabaiLasso && f.Babai.Distance == 6 - turn, "lasso starts without early damage/pull");
                        Call(game, "AdvanceCombatAnimation", .2f);
                        float time = t.Time; Call(game, "SetInventory", "pause"); Call(game, "AdvanceCombatAnimation", 10f);
                        TestSuite.Check(t.Time == time, "pause freezes emerging creature and rope"); Call(game, "SetInventory", "");
                        if (turn == 1)
                        {
                            using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "babai-emerge-" + size.Width + ".png"));
                            Call(game, "AdvanceCombatAnimation", .6f);
                            using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "babai-lasso-" + size.Width + ".png"));
                        }
                        SettleBabai(game);
                        TestSuite.Check(f.Babai.Distance == 5 - turn, "animated hit pulls once");
                    }
                    TestSuite.Check(f.Phase == CombatPhase.Defeat && f.Babai.Caught && f.Hero.Health == 0, "animated fifth pull captures hero");
                    using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "babai-capture-" + size.Width + ".png"));
                    TestSuite.Check(Read<ImageAssets>(game, "assetStore").Count == images, "animation never decodes duplicate source bitmaps");
                    Call(game, "ReturnToMainMenu"); Call(game, "StartTestBabai");
                    TestSuite.Check(Read<CombatState>(game, "combat").Babai.Distance == 5, "new fight resets distance");
                    var kill = Read<CombatState>(game, "combat"); var card = kill.Hand[0]; card.Attack = true; card.Damage = 40; card.GoldCost = 0;
                    Call(game, "PlayCombatCard", 0); SettleBabai(game);
                    TestSuite.Check(kill.Phase == CombatPhase.Victory, "Babai has death animation and victory");
                    Call(game, "CompleteMapRoom"); var run = Read<MapRun>(game, "mapRun"); run.ReopenCurrent();
                    using (Render(game)) { }
                    TestSuite.Check(Read<CombatState>(game, "combat") == null, "revisit does not resurrect Babai or trap");
                }
        }
    }
}
