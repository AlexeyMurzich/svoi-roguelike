using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        static void HoverRelic(Game game, float x, float y)
        {
            var point = game.DrawingViewport.ToClient(new PointF(x, y));
            Call(game, "OnMapMouseMove", game, new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, 0));
        }

        public static void TestHudPortraitsAndTooltips()
        {
            using (var game = new Game())
            {
                game.CreateControl();
                // Create the HWND before sizing: Windows can clamp a hidden form
                // to the monitor on first paint, invalidating sampled coordinates.
                TestSuite.Check(game.Handle != IntPtr.Zero, "HUD fixture creates native handle before sizing");
                game.ClientSize = new Size(1280, 840);
                var sources = (RectangleF[])typeof(Game).GetField("PortraitSources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static).GetValue(null);
                var sprites = Read<Dictionary<int, Image>>(game, "sprites");
                for (int hero = 0; hero < 4; hero++)
                {
                    var source = sources[hero];
                    TestSuite.Check(source.X >= 0 && source.Y >= 0 && source.Right <= 1 && source.Bottom <= 1, "portrait crop stays within approved sprite");
                    TestSuite.Check(Math.Abs(source.Width * sprites[hero].Width - source.Height * sprites[hero].Height) < 2, "portrait crop is square without stretched faces");
                    for (int buff = 0; buff < 3; buff++)
                    {
                        Write(game, "selected", hero);
                        Call(game, "OpenCheckout");
                        Write(game, "buffCursor", buff);
                        Call(game, "TakeBuff");
                        Call(game, "OpenActMap");
                        HoverRelic(game, 500, 80);
                        var before = new List<Color>();
                        var clientOrigin = game.PointToScreen(Point.Empty);
                        int border = clientOrigin.X - game.Left;
                        int caption = clientOrigin.Y - game.Top;
                        using (var frame = Render(game))
                        {
                            for (int y = 145; y <= 215; y += 10)
                                for (int x = 30; x <= 230; x += 10)
                                    before.Add(frame.GetPixel(border + x, caption + y));
                        }
                        var actions = Read<List<Tuple<RectangleF, Action>>>(game, "actions");
                        int count = actions.Count;
                        HoverRelic(game, 46, 90);
                        TestSuite.Check(Read<bool>(game, "hoveredRelic"), "hover reveals relic " + hero + "/" + buff);
                        TestSuite.Check(actions.Count == count, "hover preserves live click targets");
                        var relicPoint = game.DrawingViewport.ToClient(new PointF(46, 90));
                        Call(game, "OnMapMouseDown", game, new MouseEventArgs(MouseButtons.Left, 1, relicPoint.X, relicPoint.Y, 0));
                        TestSuite.Check(!Read<bool>(game, "mapDragging"), "floating relic press never starts map dragging");
                        Call(game, "OnMapMouseUp", game, new MouseEventArgs(MouseButtons.Left, 1, relicPoint.X, relicPoint.Y, 0));
                        using (var frame = Render(game))
                        {
                            int sample = 0, changed = 0;
                            for (int y = 145; y <= 215; y += 10)
                                for (int x = 30; x <= 230; x += 10)
                                    if (frame.GetPixel(border + x, caption + y) != before[sample++]) changed++;
                            TestSuite.Check(changed > before.Count / 2, "tooltip visibly covers the panel below relic " + hero + "/" + buff);
                            if (buff == 0)
                                frame.Save(Path.Combine(TestSuite.OutputDirectory, "hud-hero-" + hero + ".png"));
                        }

                        TestSuite.Check(Read<HeroState>(game, "currentHero").Effects.Chosen.Id == StartingBuff.For(hero)[buff].Id, "tooltip uses currently chosen relic");
                        Click(game, 46, 90);
                        TestSuite.Check(Read<string>(game, "inventoryView") == "relics", "hover does not swallow immediate relic click");
                        HoverRelic(game, 46, 90);
                        TestSuite.Check(!Read<bool>(game, "hoveredRelic"), "open inventory suppresses hover");
                        Key(game, Keys.Escape);
                        HoverRelic(game, 46, 90);
                        Call(game, "OnHudMouseLeave", game, EventArgs.Empty);
                        TestSuite.Check(!Read<bool>(game, "hoveredRelic"), "leaving window hides tooltip");
                    }
                }

                foreach (var requested in new[]
                {
                    new Size(800, 560),
                    new Size(1280, 840),
                    new Size(1920, 1080),
                    new Size(2560, 1080),
                    new Size(1024, 768),
                    new Size(900, 1400)
                }

                )
                {
                    game.ClientSize = requested;
                    using (Render(game))
                    {
                    }

                    var mapView = (RectangleF)typeof(Game).GetProperty("MapView", PrivateInstance).GetValue(game, null);
                    TestSuite.Check(mapView.Contains(46, 90), "relic sits directly on the map at " + game.ClientSize);
                    HoverRelic(game, 46, 90);
                    TestSuite.Check(Read<bool>(game, "hoveredRelic"), "scaled hover at " + game.ClientSize);
                    using (Render(game))
                    {
                    }

                    HoverRelic(game, 90, 90);
                    TestSuite.Check(!Read<bool>(game, "hoveredRelic"), "moving beyond icon hides tooltip");
                    TestSuite.Check((Read<ImageAssets>(game, "assetStore").Count - HeroArtCount(game)) == 40, "HUD reuses sprite and icon cache across resizes");
                }

                game.ClientSize = new Size(1280, 840);
                Key(game, Keys.D1);
                var pending = Read<MapRun>(game, "mapRun").Pending;
                TestSuite.Check(pending != null, "entered room for tooltip overlay check");
                using (Render(game))
                {
                }

                HoverRelic(game, 46, 90);
                TestSuite.Check(Read<bool>(game, "hoveredRelic"), "relic hover also works while in a room");
                using (var frame = Render(game))
                    frame.Save(Path.Combine(TestSuite.OutputDirectory, "hud-in-room.png"));
                TestSuite.Check(Read<MapRun>(game, "mapRun").Pending == pending, "hover never completes or changes room");
            }
        }
    }
}
