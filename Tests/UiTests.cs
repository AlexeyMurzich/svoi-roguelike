using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        public static readonly Size[] Sizes =
        {
            new Size(800, 560),
            new Size(1000, 720),
            new Size(1280, 840),
            new Size(1920, 1080),
            new Size(2560, 1080),
            new Size(900, 1400)
        };
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private static FieldInfo Field(string name)
        {
            return typeof(Game).GetField(name, PrivateInstance);
        }

        private static T Read<T>(Game game, string name)
        {
            return (T)Field(name).GetValue(game);
        }

        private static void Write(Game game, string name, object value)
        {
            Field(name).SetValue(game, value);
        }

        private static void Call(Game game, string method, params object[] arguments)
        {
            typeof(Game).GetMethod(method, PrivateInstance).Invoke(game, arguments);
        }

        private static void Click(Game game, float x, float y, MouseButtons button = MouseButtons.Left)
        {
            if (Read<bool>(game, "mapScreen") && y >= 174 && y <= 744 && (Read<string>(game, "inventoryView") != "" || Read<MapRun>(game, "mapRun").Pending != null && !((bool)typeof(Game).GetProperty("HasLiveFight", PrivateInstance).GetValue(game, null))))
            {
                x += (game.DrawingViewport.Width - 1280) / 2;
                y += (game.DrawingViewport.Height - 840) / 2;
            }

            Point point = game.DrawingViewport.ToClient(new PointF(x, y));
            Call(game, "OnClick", game, new MouseEventArgs(button, 1, point.X, point.Y, 0));
        }

        private static PointF MapPoint(Game game, MapNode node)
        {
            return (PointF)typeof(Game).GetMethod("MapNodePoint", PrivateInstance).Invoke(game, new object[] { node });
        }

        private static float ExpectedFocus(Game game, MapNode node)
        {
            float height = game.DrawingViewport.Height - MapLayout.HudHeight;
            return Math.Max(0, Math.Min(Math.Max(0, MapLayout.WorldHeight - height), MapLayout.WorldPosition(node).Y - height + 100));
        }

        private static void Key(Game game, Keys key)
        {
            var args = new KeyEventArgs(key);
            Call(game, "HandleGameKey", game, args);
            TestSuite.Check(args.Handled && args.SuppressKeyPress, "handled game key");
        }

        private static Bitmap Render(Game game)
        {
            var bitmap = new Bitmap(game.Width, game.Height);
            game.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            return bitmap;
        }

        private static string Digest(Bitmap bitmap)
        {
            using (var stream = new MemoryStream())
            using (var sha = SHA256.Create())
            {
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return Convert.ToBase64String(sha.ComputeHash(stream.ToArray()));
            }
        }

        public static void TestAssets()
        {
            string directory = Path.Combine(TestSuite.OutputDirectory, "asset-fixtures");
            Directory.CreateDirectory(directory);
            var assembly = typeof(Game).Assembly;
            string name = "illya.png";
            string localFile = Path.Combine(directory, name);
            using (var pixel = new Bitmap(8, 8))
            {
                pixel.SetPixel(0, 0, Color.FromArgb(0, 0, 0, 0));
                pixel.SetPixel(3, 3, Color.Red);
                pixel.Save(localFile, System.Drawing.Imaging.ImageFormat.Png);
            }

            var assets = new ImageAssets(assembly, directory);
            var first = assets.Get(name, name);
            TestSuite.Check(first.Width == 8 && first.GetPixel(0, 0).A == 0, "custom image preserves alpha");
            TestSuite.Check(object.ReferenceEquals(first, assets.Get(name, name)) && assets.Count == 1, "one decoded image per key");
            using (File.Open(localFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
            }

            File.Delete(localFile);
            TestSuite.Check(!File.Exists(localFile), "no file lock after decoding");
            assets.Reload(name);
            var fallback = assets.Get(name, name);
            TestSuite.Check(fallback.Width > 8 && assets.Count == 1, "embedded fallback and bounded replacement");
            TestSuite.Check(!object.ReferenceEquals(first, fallback), "reload replaces only requested bitmap");
            assets.Dispose();
            assets.Dispose();
            TestSuite.Check(assets.Count == 0, "cache emptied on dispose");
            TestSuite.Throws<ObjectDisposedException>(() => assets.Get(name, name), "closed cache rejects use");
            File.WriteAllText(localFile, "not a png");
            using (var corrupt = new ImageAssets(assembly, directory))
            {
                TestSuite.Check(corrupt.Get(name, name).Width > 8, "corrupt override falls back");
                TestSuite.Check(corrupt.Warnings.Length == 1, "corrupt asset is reported");
                TestSuite.Throws<InvalidDataException>(() => corrupt.Get("missing.png", "missing.png"), "missing required asset fails clearly");
            }

            TestSuite.Check(typeof(Game).Assembly.GetManifestResourceNames().Count() == 78, "61 images, six music tracks, two Toha, eight card sounds and one cashier glitch");
            using (var embedded = new ImageAssets(assembly, Path.Combine(directory, "no-overrides")))
            {
                var atlas = embedded.Get("map-icons.png", "map-icons.png");
                TestSuite.Check(atlas.Width * 2 == atlas.Height * 3, "six square atlas cells preserve proportions");
                TestSuite.Check(atlas.GetPixel(0, 0).A == 0 && atlas.GetPixel(atlas.Width - 1, atlas.Height - 1).A == 0, "icon sheet has genuine transparent corners");
                var heart = embedded.Get("health-icon.png", "health-icon.png");
                var rat = embedded.Get("enemies/giant-rat.png", "enemies.giant-rat.png");
                TestSuite.Check(rat.GetPixel(0, 0).A == 0 && rat.GetPixel(rat.Width - 1, rat.Height - 1).A == 0, "enemy sprite keeps true transparent corners");
                TestSuite.Check(rat.GetPixel(rat.Width / 2, rat.Height / 2).A > 200, "enemy sprite contains visible body pixels");
                TestSuite.Check(heart.GetPixel(0, 0).A == 0 && heart.GetPixel(heart.Width / 2, heart.Height / 2).A > 200, "health icon has real transparency and solid center");
                var hud = embedded.Get("menu-icons.png", "menu-icons.png");
                TestSuite.Check(hud.Width == hud.Height * 2, "deck and settings occupy two square atlas cells");
                TestSuite.Check(hud.GetPixel(0, 0).A == 0 && hud.GetPixel(hud.Width - 1, hud.Height - 1).A == 0, "HUD icons retain transparency");
                for (int index = 0; index < 6; index++)
                {
                    int x = (int)((index % 3 + .5) * atlas.Width / 3.0);
                    int y = (int)((index / 3 + .5) * atlas.Height / 2.0);
                    TestSuite.Check(atlas.GetPixel(x, y).A > 200, "icon cell is populated: " + index);
                }
            }
        }

        public static void TestInteraction()
        {
            Application.EnableVisualStyles();
            using (var game = new Game())
            {
                game.CreateControl();
                var store = Read<ImageAssets>(game, "assetStore");
                TestSuite.Check((store.Count - HeroArtCount(game)) == 40, "all art loaded once");
                TestSuite.Check(Read<Dictionary<int, Image>>(game, "sprites").Count == 4, "four portraits");
                TestSuite.Check(Read<Dictionary<string, Image>>(game, "relicImages").Count == 13, "thirteen unique icons");
                foreach (var size in Sizes)
                {
                    game.ClientSize = size;
                    for (int hero = 0; hero < 4; hero++)
                        for (int buff = 0; buff < 3; buff++)
                        {
                            Write(game, "checkout", false);
                            Write(game, "preview", false);
                            using (Render(game))
                            {
                            }

                            int before = Read<int>(game, "selected");
                            Click(game, 100 + hero * 188, 350, MouseButtons.Right);
                            TestSuite.Check(Read<int>(game, "selected") == before, "right button ignored");
                            Click(game, 100 + hero * 188, 350);
                            TestSuite.Check(Read<int>(game, "selected") == hero, "hero hit target at " + size);
                            using (Render(game))
                            {
                            }

                            Click(game, 150, 680);
                            TestSuite.Check(Read<bool>(game, "checkout"), "checkout opened");
                            Click(game, 1100, 740); // Before a new frame: old buttons must be inactive.
                            TestSuite.Check(Read<HeroState>(game, "currentHero").Effects.Chosen == null, "stale click cannot buy");
                            using (Render(game))
                            {
                            }

                            Click(game, 420 + buff * 302, 320);
                            using (var frame = Render(game))
                            {
                                if (size == Sizes[2] && buff == 0)
                                    frame.Save(Path.Combine(TestSuite.OutputDirectory, "cashier-hero-" + hero + ".png"));
                            }

                            Key(game, Keys.Enter);
                            var state = Read<HeroState>(game, "currentHero");
                            string chosen = StartingBuff.For(hero)[buff].Id;
                            TestSuite.Check(state.Effects.Chosen.Id == chosen, "correct relic selected");
                            decimal money = state.Money;
                            Key(game, Keys.Enter);
                            Key(game, Keys.Right);
                            TestSuite.Check(state.Effects.Chosen.Id == chosen && state.Money == money, "locked after selection");
                            using (Render(game))
                            {
                            }

                            Key(game, Keys.Escape);
                            TestSuite.Check(Read<bool>(game, "mapScreen"), "selected relic leaves checkout on escape");
                            Write(game, "mapScreen", false);
                        }

                    Write(game, "preview", true);
                    using (var frame = Render(game))
                        frame.Save(Path.Combine(TestSuite.OutputDirectory, "viewport-" + size.Width + "x" + size.Height + ".png"));
                    Key(game, Keys.Right);
                    using (Render(game))
                    {
                    }

                    Key(game, Keys.Escape);
                    TestSuite.Check(!Read<bool>(game, "preview"), "preview exit");
                    using (Render(game))
                    {
                    }

                    var viewport = Viewport.Fit(size);
                    if (viewport.OffsetX > 1 || viewport.OffsetY > 1)
                    {
                        beforeMarginClick(game);
                    }
                }

                TestSuite.Check((store.Count - HeroArtCount(game)) == 40, "resizes never reload or duplicate art");
                Call(game, "DisposeCheckoutArt");
                Call(game, "LoadCheckoutArt");
                TestSuite.Check((store.Count - HeroArtCount(game)) == 40, "view reopening reuses owned art");
                game.Dispose();
                TestSuite.Check(store.Count == 0, "form disposes all images");
            }
        }

        private static void beforeMarginClick(Game game)
        {
            int hero = Read<int>(game, "selected");
            Call(game, "OnClick", game, new MouseEventArgs(MouseButtons.Left, 1, 0, 0, 0));
            TestSuite.Check(Read<int>(game, "selected") == hero, "letterbox clicks ignored");
        }

        [DllImport("user32.dll")]
        private static extern uint GetGuiResources(IntPtr process, uint flags);
        public static void TestRepaint()
        {
            using (var game = new Game())
            {
                game.CreateControl();
                game.ClientSize = Sizes[2];
                Write(game, "selected", 3);
                Call(game, "OpenCheckout");
                string baseline;
                using (var frame = Render(game))
                    baseline = Digest(frame);
                var process = Process.GetCurrentProcess();
                uint initialHandles = GetGuiResources(process.Handle, 0);
                int initialFonts = Read<Dictionary<Tuple<float, bool>, Font>>(game, "fonts").Count;
                for (int i = 0; i < 72; i++)
                {
                    game.ClientSize = Sizes[i % Sizes.Length];
                    using (Render(game))
                    {
                    }
                }

                game.ClientSize = Sizes[2];
                using (var frame = Render(game))
                    TestSuite.Check(Digest(frame) == baseline, "resize roundtrip produces same pixels, no trails/duplicates");
                using (var frame = Render(game))
                    TestSuite.Check(Digest(frame) == baseline, "repeated paint is deterministic");
                TestSuite.Check((Read<ImageAssets>(game, "assetStore").Count - HeroArtCount(game)) == 40, "constant bitmap cache");
                TestSuite.Check(Read<Dictionary<Tuple<float, bool>, Font>>(game, "fonts").Count == initialFonts, "fonts reused across repaint/resize");
                uint finalHandles = GetGuiResources(process.Handle, 0);
                TestSuite.Check(finalHandles <= initialHandles + 6, "bounded GDI handles: " + initialHandles + " -> " + finalHandles);
                TestSuite.Note("After 72 resizes: GDI handles " + initialHandles + " -> " + finalHandles + "; decoded images 40; cached fonts " + initialFonts + ".");
                process.Dispose();
            }
        }
    }
}
