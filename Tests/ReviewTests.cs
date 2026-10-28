using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static class ReviewTests
    {
        public static void AnimationLifecycle()
        {
            foreach (AttackAnimation kind in new[] { AttackAnimation.HeroStrike, AttackAnimation.HeroGuard, AttackAnimation.RatBite, AttackAnimation.DogBite, AttackAnimation.DogHowl, AttackAnimation.HedgehogNeedles, AttackAnimation.HedgehogBite, AttackAnimation.MimicratorMagic, AttackAnimation.MimicratorBeacon, AttackAnimation.MimicratorBear, AttackAnimation.MimicratorShell, AttackAnimation.MimicratorTransform, AttackAnimation.MimicratorRest, AttackAnimation.TohaRam, AttackAnimation.TohaDarkRam, AttackAnimation.TohaTransform })
                foreach (int hz in new[] { 10, 30, 60, 120 })
                {
                    var timeline = new AttackTimeline();
                    int hits = 0;
                    timeline.Start(kind, () => hits++);
                    float expectedDuration = timeline.Duration / AttackTimeline.Speed;
                    float expectedHit = timeline.HitTime / AttackTimeline.Speed;
                    int frames = 0;
                    while (timeline.IsRunning)
                    {
                        timeline.Advance(1f / hz); frames++;
                        if (frames / (float)hz < expectedHit - .0001f)
                            TestSuite.Check(hits == 0, "animation never hits early at " + hz);
                        TestSuite.Check(frames <= hz * 2 && hits <= 1, "bounded animation and one impact");
                    }
                    TestSuite.Check(hits == 1 && Math.Abs(frames / (float)hz - expectedDuration) <= 1.01f / hz, "same real duration across frame rates");
                    timeline.Advance(10); TestSuite.Check(hits == 1, "finished animation stays finished");
                }
            var attack = new AttackTimeline();
            int cancelled = 0;
            attack.Start(AttackAnimation.RatBite, () => cancelled++);
            attack.Advance(float.NaN); attack.Advance(float.PositiveInfinity); attack.Advance(-1);
            TestSuite.Check(attack.Time == 0, "invalid time cannot corrupt clock");
            attack.Cancel(); attack.Advance(10);
            TestSuite.Check(cancelled == 0, "cancelled impact never runs");
            attack.Start(AttackAnimation.RatBite, () =>
            {
                attack.Cancel();
                attack.Start(AttackAnimation.DogHowl, () => cancelled++);
            });
            attack.Advance(10);
            TestSuite.Check(attack.Kind == AttackAnimation.DogHowl && attack.Time == 0, "old completion cannot cancel new scene animation");
            attack.Advance(10);
            TestSuite.Check(cancelled == 1 && !attack.IsRunning, "replacement impact runs once");
            TestSuite.Throws<ArgumentOutOfRangeException>(() => attack.Start(AttackAnimation.None, () => { }), "empty attack rejected");
        }

        public static void ModelBoundaries()
        {
            var extreme = new HeroState(3, 50, 0, new Balance { HealthLostPerDebtUnit = int.MaxValue });
            extreme.Effects.Choose("phone"); extreme.ChangeMoney(-decimal.MaxValue);
            TestSuite.Check(extreme.Health == 0 && extreme.Debt == decimal.MaxValue, "huge debt saturates damage without decimal overflow");
            var rich = new HeroState(3, 50, 0, new Balance()); rich.Effects.Choose("phone"); rich.ChangeMoney(decimal.MaxValue);
            TestSuite.Throws<OverflowException>(() => rich.ChangeMoney(1), "overflow rejected");
            TestSuite.Check(rich.Money == decimal.MaxValue && rich.Health == 50, "failed money change is atomic");
            for (int seed = 0; seed < 100; seed++)
            {
                var random = new Random(seed);
                int coefficient = random.Next(1, 8), health = 10000;
                decimal balance = 0;
                var hero = new HeroState(3, health, 0, new Balance { HealthLostPerDebtUnit = coefficient }); hero.Effects.Choose("phone");
                for (int i = 0; i < 80; i++)
                {
                    decimal change = random.Next(-20, 21) / 10m;
                    decimal before = Math.Max(0, -balance); balance += change;
                    decimal damage = Math.Max(0, Math.Ceiling(Math.Max(0, -balance) * coefficient) - Math.Ceiling(before * coefficient));
                    health -= (int)Math.Min(health, damage);
                    hero.ChangeMoney(change);
                    TestSuite.Check(hero.Health == health && hero.Money == balance, "fractional debt matches cumulative ceiling reference");
                }
            }
            var owner = new HeroState(1, 50, 0, new Balance());
            foreach (var card in new[] { new CardData { GoldCost = -1 }, new CardData { Damage = -1 }, new CardData { Block = -1 }, new CardData { Family = (CardFamily)99 } })
                TestSuite.Throws<ArgumentOutOfRangeException>(() => owner.AddCard(card), "invalid card rejected before receipt");
            TestSuite.Check(owner.Deck.Count == 0 && owner.CardsToDraw == 5, "invalid cards leave deck intact, Illya draws base three plus two");
            var configured = new HeroState(0, 50, 0, new Balance { BaseDraw = 2 }); StarterDeck.AddTo(configured);
            var fight = new CombatState(configured, new Random(1));
            TestSuite.Check(fight.Hand.Count == 2, "combat uses the same draw configuration as hero state");
            TestSuite.Throws<ArgumentNullException>(() => fight.Cost(null), "null card quote rejected");
            TestSuite.Throws<ArgumentNullException>(() => fight.DamageFor(null), "null card damage rejected");
        }

        public static void HandLayout()
        {
            foreach (float width in new[] { 1280f, 1991f })
                for (int count = 1; count <= 6; count++)
                {
                    RectangleF previous = RectangleF.Empty;
                    for (int index = 0; index < count; index++)
                    {
                        var card = CombatLayout.CardSlot(width, 840, count, index);
                        TestSuite.Check(card.Left >= 250 && card.Right <= width - 250 && card.Bottom <= 824.01f, "hand leaves space for draw pile and end-turn button");
                        TestSuite.Check(index == 0 || previous.Right < card.Left, "cards do not overlap");
                        TestSuite.Check(Math.Abs(card.Width / card.Height - 180f / 244) < .001, "card aspect ratio preserved");
                        previous = card;
                    }
                }
        }
    }

    public static partial class UiTests
    {
        public static void TestReviewedInteractions()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                Write(game, "selected", 3); Call(game, "OpenCheckout"); Call(game, "TakeBuff"); Call(game, "OpenActMap");
                using (Render(game)) { }
                Call(game, "ScrollMap", -110f);
                using (Render(game)) { }
                int targets = Read<System.Collections.Generic.List<Tuple<RectangleF, Action>>>(game, "actions").Count;
                Call(game, "AdvanceMapMotion", .016f);
                TestSuite.Check(targets > 0 && Read<System.Collections.Generic.List<Tuple<RectangleF, Action>>>(game, "actions").Count == targets, "camera tick preserves displayed click targets before repaint");
                Click(game, game.DrawingViewport.Width - 130, 30);
                TestSuite.Check(Read<string>(game, "inventoryView") == "deck", "deck click between animation tick and paint is never lost");
                TestSuite.Check(!Read<Timer>(game, "combatTimer").Enabled, "overlay stops idle motion timer");
                using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "review-shuten-deck.png"));
                string details = (string)typeof(Game).GetMethod("DeckCardDescription", PrivateInstance).Invoke(game, new object[] { Read<HeroState>(game, "currentHero").Deck[0] });
                TestSuite.Check(details.StartsWith("Золото: 10"), "Shuten deck reports gold instead of energy");
                Key(game, Keys.Escape);
                TestSuite.Check(Read<Timer>(game, "combatTimer").Enabled, "closing overlay resumes camera");
                var start = game.DrawingViewport.ToClient(new PointF(120, 500));
                var end = game.DrawingViewport.ToClient(new PointF(120, 620));
                Call(game, "OnMapMouseDown", game, new MouseEventArgs(MouseButtons.Left, 1, start.X, start.Y, 0));
                Call(game, "OnMapMouseMove", game, new MouseEventArgs(MouseButtons.Left, 0, end.X, end.Y, 0));
                Key(game, Keys.Escape);
                TestSuite.Check(!Read<bool>(game, "mapDragging") && !game.Capture, "opening pause cancels active drag and mouse capture");
                Key(game, Keys.Escape);
                Call(game, "OnDeactivate", EventArgs.Empty);
                TestSuite.Check(!Read<Timer>(game, "combatTimer").Enabled && !Read<bool>(game, "mapDragging"), "focus loss suspends motion and capture");
                Call(game, "OnActivated", EventArgs.Empty);
                TestSuite.Check(Read<Timer>(game, "combatTimer").Enabled, "focus return resumes pending camera movement");
                Call(game, "StartTestBattle"); Call(game, "EndCombatTurn");
                using (Render(game)) { }
                targets = Read<System.Collections.Generic.List<Tuple<RectangleF, Action>>>(game, "actions").Count;
                Call(game, "AdvanceCombatAnimation", .1f);
                TestSuite.Check(Read<System.Collections.Generic.List<Tuple<RectangleF, Action>>>(game, "actions").Count == targets, "combat tick keeps HUD targets until next paint");
                Key(game, Keys.Escape);
                float paused = Read<AttackTimeline>(game, "attackTimeline").Time;
                Call(game, "AdvanceCombatAnimation", .5f);
                TestSuite.Check(Read<AttackTimeline>(game, "attackTimeline").Time == paused && !Read<Timer>(game, "combatTimer").Enabled, "pause freezes timeline and timer");
                Key(game, Keys.Escape);
                TestSuite.Check(Read<Timer>(game, "combatTimer").Enabled, "resume wakes attack timer");
                Call(game, "AdvanceCombatAnimation", 1f);
                var phone = new HeroState(3, 5, 0, new Balance()); phone.Effects.Choose("phone");
                phone.AddCard(new CardData { Attack = true, Damage = 6, GoldCost = 10 });
                var debtFight = new CombatState(phone, new Random(1));
                Write(game, "currentHero", phone); Write(game, "combat", debtFight);
                Call(game, "PlayCombatCard", 0); Call(game, "AdvanceCombatAnimation", 1f);
                TestSuite.Check(debtFight.Phase == CombatPhase.Defeat && debtFight.EnemyHealth == CombatState.EnemyMaxHealth && Read<string>(game, "combatNotice") == "Поражение", "lethal card payment shows defeat without pretending to hit enemy");
                var fullHand = new HeroState(3, 50, 0, new Balance { BaseDraw = 6 });
                fullHand.Effects.Choose("ring"); StarterDeck.AddTo(fullHand);
                Read<DeathVisuals>(game, "deathVisuals").Clear();
                Write(game, "currentHero", fullHand);
                Write(game, "combat", new CombatState(fullHand, new Random(1)));
                Write(game, "combatNotice", "Тест расширенной руки");
                using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "review-hand-six.png"));
            }
        }

        public static void TestInvestigationPreview()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                Write(game, "selected", 2); Call(game, "OpenCheckout"); Write(game, "buffCursor", 2); Call(game, "TakeBuff"); Call(game, "OpenActMap");
                var run = Read<MapRun>(game, "mapRun");
                var intel = Read<System.Collections.Generic.Dictionary<int, IntelNode>>(game, "mapIntel");
                var node = run.Map.Nodes.First(n => n.Kind == RoomKind.Fight && intel[n.Id].Revealed);
                Call(game, "SnapMap", ExpectedFocus(game, node));
                var position = game.DrawingViewport.ToClient(MapPoint(game, node));
                Call(game, "OnMapMouseMove", game, new MouseEventArgs(MouseButtons.None, 0, position.X, position.Y, 0));
                TestSuite.Check(Read<int>(game, "hoveredMapNode") == node.Id, "revealed room can be inspected before entering");
                string identity = (string)typeof(Game).GetMethod("RevealedIdentity", PrivateInstance).Invoke(game, new object[] { node.Id });
                TestSuite.Check(identity == OrdinaryEncounters.Preview(run, node.Id), "preview names the enemy actually generated for this room");
                TestSuite.Check(run.Pending == null && run.Current.Floor == 0, "inspection does not enter or consume a room");
                using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "review-investigation-preview.png"));
                var hidden = run.Map.Nodes.First(n => !intel[n.Id].Revealed);
                string concealed = (string)typeof(Game).GetMethod("RevealedIdentity", PrivateInstance).Invoke(game, new object[] { hidden.Id });
                TestSuite.Check(concealed == "", "unrevealed identity stays hidden");
            }
        }
    }
}
