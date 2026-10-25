using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        public static void TestReturnToMainMenu()
        {
            foreach (var size in Sizes)
                using (var game = new Game())
                {
                    game.CreateControl();
                    game.ClientSize = size;
                    Write(game, "selected", 3);
                    Call(game, "OpenCheckout");
                    Call(game, "TakeBuff");
                    Key(game, Keys.Escape);
                    Key(game, Keys.D1);
                    var oldRun = Read<MapRun>(game, "mapRun");
                    TestSuite.Check(oldRun.Pending != null, "return starts from a pending room");
                    Key(game, Keys.Escape);
                    using (Render(game))
                    {
                    }

                    bool fullscreen = Read<bool>(game, "isFullscreen");
                    Click(game, 640, 507);
                    TestSuite.Check(!Read<bool>(game, "mapScreen") && !Read<bool>(game, "checkout") && !Read<bool>(game, "preview") && Read<string>(game, "inventoryView") == "", "main menu returns to hero selection at " + size);
                    TestSuite.Check(Read<MapRun>(game, "mapRun") == null && Read<HeroState>(game, "currentHero") == null, "old run and pending room released");
                    TestSuite.Check(!Read<bool>(game, "mapDragging") && !Read<bool>(game, "hoveredRelic") && !Read<bool>(game, "legendOpen"), "transient map state cleared");
                    TestSuite.Check(!game.IsDisposed && Read<bool>(game, "isFullscreen") == fullscreen, "return keeps app and display mode");
                    Click(game, 640, 507);
                    TestSuite.Check(!Read<bool>(game, "checkout"), "old pause hit targets are cleared");
                    using (var frame = Render(game))
                        if (size == Sizes[2])
                            frame.Save(Path.Combine(TestSuite.OutputDirectory, "main-menu.png"));
                    Key(game, Keys.Enter);
                    var fresh = Read<HeroState>(game, "currentHero");
                    TestSuite.Check(fresh.Money == 0 && fresh.Health == fresh.MaxHealth && fresh.Deck.Count == 0 && fresh.Effects.Chosen == null, "new run starts without previous gold, deck or relic");
                    Write(game, "buffCursor", 2);
                    Call(game, "TakeBuff");
                    Key(game, Keys.Escape);
                    var nextRun = Read<MapRun>(game, "mapRun");
                    TestSuite.Check(nextRun != oldRun && nextRun.Current.Floor == 0 && nextRun.Pending == null && fresh.Money == 0, "new route starts at Euroopt without old pending room or ring gold");
                }
        }

        public static void TestFullscreenAndMinimalHud()
        {
            using (var game = new Game())
            {
                game.CreateControl();
                var window = game.Bounds;
                Call(game, "StartFullscreen");
                TestSuite.Check(game.FormBorderStyle == FormBorderStyle.None, "fullscreen removes caption and resize frame");
                TestSuite.Check(game.Bounds == Screen.FromControl(game).Bounds, "fullscreen uses full monitor rather than working area");
                TestSuite.Check(game.TopMost && Read<bool>(game, "isFullscreen"), "fullscreen covers taskbar when active");
                Call(game, "OnShown", EventArgs.Empty);
                TestSuite.Check(game.Bounds == Screen.FromControl(game).Bounds, "shown handler never shrinks fullscreen to taskbar work area");
                Key(game, Keys.F11);
                TestSuite.Check(!Read<bool>(game, "isFullscreen") && game.FormBorderStyle == FormBorderStyle.Sizable && game.Bounds == window, "F11 restores prior window");
                Key(game, Keys.F11);
                Call(game, "OnDeactivate", EventArgs.Empty);
                TestSuite.Check(!game.TopMost, "alt-tab does not trap other windows behind game");
                Call(game, "OnActivated", EventArgs.Empty);
                TestSuite.Check(game.TopMost, "returning to fullscreen restores taskbar coverage");
                Write(game, "selected", 3);
                Call(game, "OpenCheckout");
                Call(game, "TakeBuff");
                Key(game, Keys.Escape);
                TestSuite.Check(Read<bool>(game, "mapScreen") && !Read<bool>(game, "checkout"), "chosen relic leads out of checkout");
                using (var frame = Render(game))
                    frame.Save(Path.Combine(TestSuite.OutputDirectory, "fullscreen-map.png"));
                TestSuite.Check(!Read<bool>(game, "legendOpen"), "legend is collapsed on a new run");
                var legendPoint = game.DrawingViewport.ToClient(new PointF(game.DrawingViewport.Width - 114, 210));
                Call(game, "OnMapMouseMove", game, new MouseEventArgs(MouseButtons.None, 0, legendPoint.X, legendPoint.Y, 0));
                TestSuite.Check(!Read<bool>(game, "legendOpen"), "hover never opens legend");
                Click(game, game.DrawingViewport.Width - 114, 210);
                TestSuite.Check(Read<bool>(game, "legendOpen"), "explicit click opens legend");
                using (var frame = Render(game))
                    frame.Save(Path.Combine(TestSuite.OutputDirectory, "legend-open.png"));
                Click(game, game.DrawingViewport.Width - 114, 210);
                TestSuite.Check(!Read<bool>(game, "legendOpen"), "second click closes legend");
                using (Render(game))
                {
                }

                float scrollBefore = Read<float>(game, "mapScroll");
                Click(game, game.DrawingViewport.Width - 58, 157);
                TestSuite.Check(Read<float>(game, "mapScroll") == scrollBefore, "removed upper scroll arrow has no live target");
                var dragFrom = game.DrawingViewport.ToClient(new PointF(game.DrawingViewport.Width - 50, game.DrawingViewport.Height - 150));
                var dragTo = new Point(dragFrom.X, dragFrom.Y + 80);
                Call(game, "OnMapMouseDown", game, new MouseEventArgs(MouseButtons.Left, 1, dragFrom.X, dragFrom.Y, 0));
                Call(game, "OnMapMouseMove", game, new MouseEventArgs(MouseButtons.Left, 0, dragTo.X, dragTo.Y, 0));
                Call(game, "AdvanceMapMotion", .016f);
                TestSuite.Check(Read<bool>(game, "mapDragging") && Read<float>(game, "mapScroll") < scrollBefore, "free right edge allows map dragging after scrollbar removal");
                Call(game, "OnMapMouseUp", game, new MouseEventArgs(MouseButtons.Left, 1, dragTo.X, dragTo.Y, 0));
                Click(game, game.DrawingViewport.Width - 50, game.DrawingViewport.Height - 70);
                Key(game, Keys.End);
                SettleMap(game);
                using (Render(game))
                {
                }

                decimal goldBefore = Read<HeroState>(game, "currentHero").Money;
                Click(game, 46, 90);
                TestSuite.Check(Read<string>(game, "inventoryView") == "relics", "relic icon opens description without permanent caption");
                Key(game, Keys.Escape);
                using (Render(game))
                {
                }

                Click(game, game.DrawingViewport.Width - 130, 31);
                TestSuite.Check(Read<string>(game, "inventoryView") == "deck", "deck icon opens deck");
                Key(game, Keys.Escape);
                using (Render(game))
                {
                }

                Click(game, game.DrawingViewport.Width - 49, 31);
                TestSuite.Check(Read<string>(game, "inventoryView") == "pause", "gear opens pause rather than checkout");
                using (var frame = Render(game))
                    frame.Save(Path.Combine(TestSuite.OutputDirectory, "pause-menu.png"));
                Key(game, Keys.Enter);
                TestSuite.Check(Read<MapRun>(game, "mapRun").Pending == null, "pause blocks map progression");
                Key(game, Keys.Escape);
                TestSuite.Check(Read<bool>(game, "mapScreen") && !Read<bool>(game, "checkout"), "escape resumes map");
                Key(game, Keys.Escape);
                Key(game, Keys.Escape);
                TestSuite.Check(Read<HeroState>(game, "currentHero").Money == goldBefore && goldBefore == 300, "pause cannot reselect or duplicate starting relic");
                using (Render(game))
                {
                }

                var run = Read<MapRun>(game, "mapRun");
                var start = MapPoint(game, run.Current);
                Click(game, start.X, start.Y);
                TestSuite.Check(Read<bool>(game, "mapScreen") && !Read<bool>(game, "checkout"), "start node cannot return to checkout");
            }
        }
    }
}
