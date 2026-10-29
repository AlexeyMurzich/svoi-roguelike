using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        static Bitmap DogLayer(Game game)
        {
            var bitmap = new Bitmap(270, 245, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.Bilinear;
                Call(game, "DrawDogPose", graphics, new RectangleF(0, 0, 270, 245));
            }
            return bitmap;
        }

        public static void TestDogLegStability()
        {
            using (var game = new Game())
            using (var board = new Bitmap(1080, 542))
            using (var boardGraphics = Graphics.FromImage(board))
            {
                game.CreateControl(); Call(game, "StartTestDogs"); Write(game, "actingEnemyIndex", 0);
                var timeline = Read<AttackTimeline>(game, "attackTimeline");
                boardGraphics.Clear(Color.FromArgb(28, 36, 40));
                int sample = 0, baseline;
                using (var layer = DogLayer(game)) baseline = DogSilhouette(layer).Item1;
                foreach (var kind in new[] { AttackAnimation.DogBite, AttackAnimation.DogHowl })
                {
                    timeline.Cancel(); timeline.Start(kind, () => { }); int frame = 0;
                    while (timeline.IsRunning)
                    {
                        using (var layer = DogLayer(game))
                        {
                            var support = DogSilhouette(layer);
                            TestSuite.Check(support.Item2 > .97f && support.Item1 > baseline * .65f, "whole dog stays connected and opaque during " + kind);
                            TestSuite.Check(Math.Abs(support.Item3 - 245) <= 3, "paws stay grounded during " + kind);
                            if (frame % 15 == 0 && sample < 8)
                            {
                                int x = sample % 4 * 270, y = sample / 4 * 271;
                                boardGraphics.DrawImageUnscaled(layer, x, y + 26);
                                using (var font = new Font("Segoe UI", 13)) boardGraphics.DrawString(kind + " " + timeline.Time.ToString("F2"), font, Brushes.White, x + 10, y + 3);
                                sample++;
                            }
                        }
                        timeline.Advance(1f / 60); frame++;
                    }
                    timeline.Cancel();
                }
                board.Save(Path.Combine(TestSuite.OutputDirectory, "dog-animation-contact-sheet.png"));
            }
        }

        public static void TestSceneResourceStress()
        {
            using (var game = new Game())
            using (var process = Process.GetCurrentProcess())
            {
                game.CreateControl();
                uint initialHandles = 0;
                int initialFonts = 0;
                for (int cycle = 0; cycle < 18; cycle++)
                {
                    game.ClientSize = Sizes[cycle % Sizes.Length];
                    Bitmap previous = Read<Bitmap>(game, "sceneBackground");
                    Call(game, "StartTestDogs");
                    using (Render(game)) { }
                    if (previous != null)
                        TestSuite.Throws<ArgumentException>(() => previous.GetPixel(0, 0), "old scene background is disposed on replacement");
                    Key(game, Keys.Escape);
                    using (Render(game)) { }
                    Key(game, Keys.Escape);
                    Call(game, "ReturnToMainMenu"); Call(game, "OpenCheckout"); Call(game, "TakeBuff"); Call(game, "OpenActMap");
                    Call(game, "ScrollMap", -110f); Call(game, "AdvanceMapMotion", .1f);
                    using (Render(game)) { }
                    TestSuite.Check((Read<ImageAssets>(game, "assetStore").Count - HeroArtCount(game)) == 36, "scene transitions never duplicate source images");
                    if (cycle == 5)
                    {
                        initialHandles = GetGuiResources(process.Handle, 0);
                        initialFonts = Read<System.Collections.Generic.Dictionary<Tuple<float, bool>, Font>>(game, "fonts").Count;
                    }
                }
                uint finalHandles = GetGuiResources(process.Handle, 0);
                TestSuite.Check(finalHandles <= initialHandles + 3, "battle/map/resize stress keeps GDI handles bounded");
                TestSuite.Check(Read<System.Collections.Generic.Dictionary<Tuple<float, bool>, Font>>(game, "fonts").Count == initialFonts, "scene stress reuses fonts after warmup");
                TestSuite.Note("18 battle/map/pause/resize cycles: GDI handles " + initialHandles + " -> " + finalHandles + "; source images 36; font cache " + initialFonts + ".");
                var assets = Read<ImageAssets>(game, "assetStore");
                game.Dispose();
                TestSuite.Check(assets.Count == 0 && Read<Bitmap>(game, "sceneBackground") == null, "closing game releases original images and derived background");
            }
        }
    }
}
