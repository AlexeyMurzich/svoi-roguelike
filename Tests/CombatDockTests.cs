using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        static void DockClick(Game game, RectangleF area)
        {
            Click(game, area.X + area.Width / 2, area.Y + area.Height / 2);
        }
        public static void TestCombatDock()
        {
            foreach (var size in Sizes)
                using (var game = new Game())
                {
                    game.CreateControl(); game.ClientSize = size;
                    Call(game, "StartTestBattle");
                    var fight = Read<CombatState>(game, "combat");
                    var hand = (List<CardData>)typeof(CombatState).GetField("hand", PrivateInstance).GetValue(fight);
                    hand.Clear();
                    for (int i = 0; i < 6; i++) hand.Add(new CardData { Name = i % 2 == 0 ? "Удар" : "Защита", Attack = i % 2 == 0, Skill = i % 2 != 0, Damage = i % 2 == 0 ? 6 : 0, Block = i % 2 != 0 ? 5 : 0, ManaCost = 1, GoldCost = 10 });
                    float w = game.DrawingViewport.Width, h = game.DrawingViewport.Height;
                    using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "dock-six-" + size.Width + "x" + size.Height + ".png"));
                    w = game.DrawingViewport.Width; h = game.DrawingViewport.Height;
                    var slot = CombatLayout.CardSlot(w, h, 6, 2);
                    HoverRelic(game, slot.X + 30, slot.Y + 45);
                    TestSuite.Check(Read<int>(game, "hoveredCombatCard") == 2, "exposed fan card selected at " + size + "; actual=" + game.ClientSize + "; hover=" + Read<int>(game, "hoveredCombatCard"));
                    var raised = (RectangleF)typeof(Game).GetMethod("HandCardSlot", PrivateInstance).Invoke(game, new object[] { 2, true });
                    TestSuite.Check(raised.Top > 64 && raised.Bottom < h && raised.Width > slot.Width, "hovered card enlarges and is fully visible");
                    using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "dock-hover-" + size.Width + "x" + size.Height + ".png"));
                    decimal before = fight.Hero.Money;
                    Click(game, raised.X + raised.Width / 2, raised.Bottom - 45);
                    TestSuite.Check(Read<AttackTimeline>(game, "attackTimeline").IsRunning, "click on raised rules area starts action");
                    using (Render(game)) { }
                    DockClick(game, CombatLayout.EndTurn(w, h));
                    TestSuite.Check(fight.Phase == CombatPhase.Player && fight.Turn == 1, "disabled end turn cannot interrupt own card");
                    Call(game, "AdvanceCombatAnimation", 2f);
                    TestSuite.Check(fight.Hero.Money == before - 10 && fight.Hand.Count == 5, "raised card charges and resolves exactly once");
                    using (Render(game)) { }
                    DockClick(game, CombatLayout.DrawPile(w, h));
                    TestSuite.Check(Read<string>(game, "inventoryView") == "draw", "draw pile opens real contents");
                    int turn = fight.Turn;
                    Call(game, "AdvanceCombatAnimation", 2f);
                    TestSuite.Check(fight.Turn == turn, "pile inspection pauses gameplay");
                    using (Render(game)) { }
                    Key(game, Keys.Escape);
                    using (Render(game)) { }
                    DockClick(game, CombatLayout.DiscardPile(w, h));
                    TestSuite.Check(Read<string>(game, "inventoryView") == "discard" && fight.PileCards(true).Count == fight.DiscardCount, "discard shows actual played cards");
                    using (var frame = Render(game))
                        if (size.Width == 1280) frame.Save(Path.Combine(TestSuite.OutputDirectory, "dock-discard.png"));
                    Key(game, Keys.Escape);
                    using (Render(game)) { }
                    DockClick(game, CombatLayout.EndTurn(w, h));
                    TestSuite.Check(fight.Phase == CombatPhase.Enemy, "end turn remains clickable after pile closes");
                }
            for (int hero = 0; hero < 4; hero++)
                using (var game = new Game())
                {
                    game.CreateControl(); game.ClientSize = new Size(1280, 840);
                    typeof(Game).GetMethod("StartTestEncounter", PrivateInstance, null, new[] { typeof(EnemyKind), typeof(int) }, null).Invoke(game, new object[] { EnemyKind.Rat, hero });
                    var fight = Read<CombatState>(game, "combat");
                    using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "dock-hero-" + hero + ".png"));
                    if (fight.Hand.Count > 0)
                    {
                        var slot = CombatLayout.CardSlot(game.DrawingViewport.Width, game.DrawingViewport.Height, fight.Hand.Count, 0);
                        HoverRelic(game, slot.X + slot.Width / 2, slot.Y + 55);
                        TestSuite.Check(Read<int>(game, "hoveredCombatCard") == 0, "hero artwork and resource use same hover geometry: " + hero);
                        using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "dock-hero-hover-" + hero + ".png"));
                        int count = Read<System.Collections.IDictionary>(game, "fonts").Count;
                        for (int repeat = 0; repeat < 8; repeat++)
                        {
                            HoverRelic(game, 20, 200); using (Render(game)) { }
                            HoverRelic(game, slot.X + slot.Width / 2, slot.Y + 55); using (Render(game)) { }
                        }
                        TestSuite.Check(Read<System.Collections.IDictionary>(game, "fonts").Count == count, "hover reuses fonts");
                        object cache = Read<object>(game, "cardArtCache");
                        var images = (System.Collections.IDictionary)cache.GetType().GetField("cards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(cache);
                        TestSuite.Check(images.Count <= 4, "base and hover artwork cache stays bounded");
                        Call(game, "OnHudMouseLeave", game, EventArgs.Empty);
                        TestSuite.Check(Read<int>(game, "hoveredCombatCard") == -1, "leaving window clears lifted card");
                    }
                }
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840); Call(game, "StartTestBattle");
                var fight = Read<CombatState>(game, "combat");
                var draw = (List<CardData>)typeof(CombatState).GetField("draw", PrivateInstance).GetValue(fight);
                var original = draw.ToArray();
                var snapshot = fight.PileCards(false);
                TestSuite.Check(draw.SequenceEqual(original), "inspection never changes future draws");
                TestSuite.Check(snapshot.Select(c => c.Name).SequenceEqual(snapshot.Select(c => c.Name).OrderBy(n => n, StringComparer.Ordinal)), "inspection hides shuffle order behind sorted cards");
                TestSuite.Throws<NotSupportedException>(() => ((IList<CardData>)snapshot).Clear(), "pile view cannot mutate combat");
                var hand = (List<CardData>)typeof(CombatState).GetField("hand", PrivateInstance).GetValue(fight);
                hand.Clear(); hand.Add(new CardData { Name = "Удар", Attack = true, Damage = 6, GoldCost = 10 });
                using (Render(game)) { }
                var originalSlot = CombatLayout.CardSlot(1280, 840, 1, 0);
                HoverRelic(game, originalSlot.X + 90, originalSlot.Y + 55);
                using (Render(game)) { }
                Click(game, originalSlot.X + 90, 837);
                TestSuite.Check(!Read<AttackTimeline>(game, "attackTimeline").IsRunning && hand.Count == 1, "invisible original footprint below lifted card cannot be played");
                fight.Hero.PayMoneyCard((int)fight.Hero.Money);
                hand.Clear(); hand.Add(new CardData { Name = "Удар", Attack = true, Damage = 6, GoldCost = 10 });
                using (Render(game)) { }
                var slot = CombatLayout.CardSlot(1280, 840, 1, 0);
                HoverRelic(game, slot.X + 90, slot.Y + 55);
                using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "dock-no-money.png"));
                var raised = (RectangleF)typeof(Game).GetMethod("HandCardSlot", PrivateInstance).Invoke(game, new object[] { 0, true });
                DockClick(game, raised);
                TestSuite.Check(!Read<AttackTimeline>(game, "attackTimeline").IsRunning && hand.Count == 1 && fight.Hero.Money == 0, "hover never makes an unaffordable card playable");
                hand.Clear(); Write(game, "hoveredCombatCard", -1);
                using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "dock-empty-hand.png"));
                DockClick(game, CombatLayout.DiscardPile(1280, 840));
                TestSuite.Check(Read<string>(game, "inventoryView") == "discard" && fight.DiscardCount == 0, "empty discard stays inspectable");
                using (Render(game)) { }
            }
        }
    }
}
