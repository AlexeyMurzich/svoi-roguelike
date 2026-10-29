using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        static void SettleMap(Game game)
        {
            for (int i = 0; i < 240; i++) Call(game, "AdvanceMapMotion", .016f);
        }

        public static void TestScrollableMapAndMenus()
        {
            foreach (var size in Sizes)
                using (var game = new Game())
                {
                    game.CreateControl();
                    game.ClientSize = size;
                    Write(game, "selected", 2);
                    Call(game, "OpenCheckout");
                    Write(game, "buffCursor", 2);
                    Call(game, "TakeBuff");
                    Call(game, "OpenActMap");
                    var hero = Read<HeroState>(game, "currentHero");
                    var run = Read<MapRun>(game, "mapRun");
                    float initialScroll = Read<float>(game, "mapScroll");
                    TestSuite.Check(initialScroll == Math.Max(0, MapLayout.WorldHeight - game.DrawingViewport.Height + MapLayout.HudHeight), "start is shown at bottom");
                    var viewport = game.DrawingViewport;
                    TestSuite.Check(viewport.OffsetX == 0 && viewport.OffsetY == 0, "map has no letterbox margins");
                    var corner = viewport.ToClient(new PointF(viewport.Width, viewport.Height));
                    TestSuite.Check(corner.X == game.ClientSize.Width && corner.Y == game.ClientSize.Height, "map fills complete client area");
                    using (Render(game))
                    {
                    }

                    initialScroll = Read<float>(game, "mapScroll");
                    Click(game, game.DrawingViewport.Width - 130, 30);
                    TestSuite.Check(Read<string>(game, "inventoryView") == "deck" && hero.Deck.Count == 6, "top deck button opens six-card starter deck");
                    using (var frame = Render(game))
                        if (size == Sizes[2])
                            frame.Save(Path.Combine(TestSuite.OutputDirectory, "deck-empty.png"));
                    Call(game, "ScrollMap", -110f);
                    TestSuite.Check(Read<float>(game, "mapScroll") == initialScroll, "menu blocks map scrolling");
                    Click(game, 640, 602);
                    Key(game, Keys.D1);
                    TestSuite.Check(run.Current.Floor == 0 && run.Pending == null, "menu blocks map clicks and movement keys");
                    Key(game, Keys.Escape);
                    TestSuite.Check(Read<bool>(game, "mapScreen") && !Read<bool>(game, "checkout"), "escape closes menu first");
                    using (Render(game))
                    {
                    }

                    var hover = game.DrawingViewport.ToClient(MapPoint(game, run.Map.Nodes[1]));
                    Call(game, "OnMapMouseMove", game, new MouseEventArgs(MouseButtons.None, 0, hover.X, hover.Y, 0));
                    Click(game, 46, 90);
                    TestSuite.Check(Read<string>(game, "inventoryView") == "relics", "hover does not discard valid click targets before paint");
                    Key(game, Keys.Escape);
                    Key(game, Keys.Home);
                    SettleMap(game);
                    TestSuite.Check(Read<float>(game, "mapScroll") == 0, "home scrolls to boss");
                    using (var frame = Render(game))
                        if (size == Sizes[2])
                            frame.Save(Path.Combine(TestSuite.OutputDirectory, "map-boss.png"));
                    Click(game, 640, 602);
                    TestSuite.Check(run.Pending == null, "offscreen first fight has no hit target");
                    Key(game, Keys.End);
                    SettleMap(game);
                    var wheelPoint = game.DrawingViewport.ToClient(new PointF(640, 400));
                    Call(game, "OnMapWheel", game, new MouseEventArgs(MouseButtons.None, 0, wheelPoint.X, wheelPoint.Y, 120));
                    SettleMap(game);
                    TestSuite.Check(Math.Abs(Read<float>(game, "mapScroll") - Math.Max(0, initialScroll - 110)) < 1.5f, "wheel scroll: " + size + " initial=" + initialScroll + " actual=" + Read<float>(game, "mapScroll") + " client=" + game.ClientSize + " canvas=" + game.DrawingViewport.Width + "x" + game.DrawingViewport.Height + " menu=" + Read<string>(game, "inventoryView"));
                    Key(game, Keys.End);
                    SettleMap(game);
                    var from = game.DrawingViewport.ToClient(new PointF(120, 500));
                    var to = game.DrawingViewport.ToClient(new PointF(120, 620));
                    Call(game, "OnMapMouseDown", game, new MouseEventArgs(MouseButtons.Left, 1, from.X, from.Y, 0));
                    Call(game, "OnMapMouseMove", game, new MouseEventArgs(MouseButtons.Left, 0, to.X, to.Y, 0));
                    TestSuite.Check(Read<bool>(game, "mapDragging") && Read<bool>(game, "mapDragMoved"), "drag active before paint: " + size + " dragging=" + Read<bool>(game, "mapDragging") + " moved=" + Read<bool>(game, "mapDragMoved") + " scroll=" + Read<float>(game, "mapScroll") + " visible=" + game.Visible);
                    using (Render(game))
                    {
                    }

                    TestSuite.Check(Read<bool>(game, "mapDragging") && Read<bool>(game, "mapDragMoved"), "drag active after paint: " + size + " dragging=" + Read<bool>(game, "mapDragging") + " moved=" + Read<bool>(game, "mapDragMoved"));
                    var duringDrag = MapPoint(game, run.Map.Nodes[1]);
                    Click(game, duringDrag.X, duringDrag.Y);
                    TestSuite.Check(run.Pending == null, "click-before-mouse-up ordering also cannot enter a room during drag");
                    Call(game, "OnMapMouseUp", game, new MouseEventArgs(MouseButtons.Left, 1, to.X, to.Y, 0));
                    SettleMap(game);
                    TestSuite.Check(Math.Abs(Read<float>(game, "mapScroll") - Math.Max(0, initialScroll - 120)) < 2.5f, "drag moves map at " + size + ": " + initialScroll + " -> " + Read<float>(game, "mapScroll") + ", start=" + Read<float>(game, "dragStartY"));
                    using (Render(game))
                    {
                    }

                    var first = MapPoint(game, run.Map.Nodes[1]);
                    Click(game, first.X, first.Y);
                    TestSuite.Check(run.Pending == null, "drag release cannot select a room");
                    using (Render(game))
                    {
                    }

                    Click(game, first.X, first.Y);
                    TestSuite.Check(run.Pending != null, "ordinary click works after drag suppression");
                    using (Render(game))
                    {
                    }

                    Click(game, 46, 90);
                    TestSuite.Check(Read<string>(game, "inventoryView") == "relics", "relic menu remains available inside room");
                    using (var frame = Render(game))
                        if (size == Sizes[2])
                            frame.Save(Path.Combine(TestSuite.OutputDirectory, "relic-menu.png"));
                    Key(game, Keys.Enter);
                    TestSuite.Check(run.Pending != null && run.Current.Floor == 0, "enter in relic menu cannot complete pending room");
                    Key(game, Keys.Escape);
                    FinishTestFight(game);
                    Key(game, Keys.Enter);
                    SettleMap(game);
                    TestSuite.Check(run.Current.Floor == 1 && Read<float>(game, "mapScroll") == ExpectedFocus(game, run.Current), "completed room focuses camera");
                    using (Render(game))
                    {
                    }

                    var current = MapPoint(game, run.Current);
                    int currentId = run.Current.Id;
                    Click(game, current.X, current.Y);
                    TestSuite.Check(run.Pending == run.Current && run.PendingIsRevisit, "current room reopens on click");
                    Key(game, Keys.Enter);
                    TestSuite.Check(run.Pending == null && run.Current.Id == currentId, "revisit returns without advancing floor");
                    Key(game, Keys.D);
                    for (int i = 0; i < 9; i++)
                        hero.AddCard(new CardData { Name = "Тест " + i, ManaCost = i % 3 });
                    using (Render(game))
                    {
                    }

                    Click(game, 960, 703);
                    TestSuite.Check(Read<int>(game, "deckPage") == 1, "deck has functional second page");
                    Key(game, Keys.Right);
                    TestSuite.Check(Read<int>(game, "deckPage") == 1, "last page bounded");
                    Key(game, Keys.Left);
                    TestSuite.Check(Read<int>(game, "deckPage") == 0, "deck page keyboard navigation");
                    using (Render(game))
                    {
                    }

                    Click(game, 970, 213);
                    TestSuite.Check(Read<string>(game, "inventoryView") == "", "mouse closes inventory");
                    using (Render(game))
                    {
                    }

                    TestSuite.Check(!Read<bool>(game, "legendOpen"), "legend remains closed until clicked");
                    using (Render(game))
                    {
                    }

                    Key(game, Keys.Up);
                    Key(game, Keys.Down);
                    Key(game, Keys.PageUp);
                    Key(game, Keys.Space);
                    SettleMap(game);
                    TestSuite.Check(Read<float>(game, "mapScroll") == ExpectedFocus(game, run.Current), "focus shortcut restores current position");
                    TestSuite.Check((Read<ImageAssets>(game, "assetStore").Count - HeroArtCount(game)) == 38, "menus and scrolling reuse thirty-eight assets");
                }
        }
    }

    public static class MapMenuTests
    {
        public static void LayoutAndDeck()
        {
            TestSuite.Check(MapLayout.Clamp(-100) == 0 && MapLayout.Clamp(float.MaxValue) == MapLayout.MaxScroll, "scroll bounds");
            foreach (var node in ActMap.Generate(23).Nodes)
            {
                var position = MapLayout.ScreenPosition(node, MapLayout.Focus(node));
                TestSuite.Check(position.Y >= MapLayout.View.Top + 40 && position.Y <= MapLayout.View.Bottom - 60, "focus keeps room away from chrome");
                TestSuite.Check(position.X > 220 && position.X < 1060, "rooms fit wide map area");
            }

            TestSuite.Throws<ArgumentNullException>(() => MapLayout.WorldPosition(null), "null node rejected");
            var hero = new HeroState(1, 50, 0, new Balance());
            TestSuite.Check(hero.MaxHealth == 50 && hero.Deck.Count == 0, "real initial health and empty deck");
            hero.Effects.Choose("armor");
            var card = new CardData
            {
                Name = "Тест защиты",
                Skill = true,
                Block = 4,
                ManaCost = 1
            };
            hero.AddCard(card);
            TestSuite.Check(hero.Deck.Count == 1 && hero.Deck[0] == card && card.Block == 6, "deck receipt applies relic effect");
            TestSuite.Throws<ArgumentNullException>(() => hero.AddCard(null), "null card rejected");
            TestSuite.Throws<NotSupportedException>(() => ((IList<CardData>)hero.Deck).Clear(), "deck view immutable");
            hero.ReceiveDamage(7);
            TestSuite.Check(hero.Health == 43 && hero.MaxHealth == 50, "health maximum remains stable");
            foreach (var size in new[]
            {
                new Size(1920, 1080),
                new Size(2560, 1080),
                new Size(900, 1400)
            }

            )
            {
                var viewport = Viewport.FillMap(size);
                var edge = viewport.ToClient(new PointF(viewport.Width, viewport.Height));
                TestSuite.Check(edge.X == size.Width && edge.Y == size.Height && viewport.OffsetX == 0, "wide and portrait map have no bars");
                PointF mapped;
                TestSuite.Check(viewport.TryMap(new Point(size.Width - 1, size.Height - 1), out mapped), "outermost pixels accept input");
            }

            var run = new MapRun(ActMap.Generate(42));
            while (!run.Finished)
            {
                run.Enter(run.Current.Exits[0]);
                run.CompleteRoom();
            }

            TestSuite.Check(run.ReopenCurrent() && run.PendingIsRevisit, "boss room can be viewed after finish");
            TestSuite.Check(!run.CompleteRoom() && run.Pending == null && run.Finished, "boss revisit closes without completing twice");
        }
    }
}
