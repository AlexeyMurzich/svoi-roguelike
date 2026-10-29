using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static class MimicratorTests
    {
        public static void Orbits()
        {
            foreach (int item in new[] { 0, 1, 2 })
            {
                bool front = false, back = false;
                for (int step = 0; step <= 240; step++)
                {
                    var pose = MimicratorOrbit.Sample(item, step / 20.0);
                    front |= pose.Depth >= 0; back |= pose.Depth < 0;
                    TestSuite.Check(pose.Position.X >= 350 && pose.Position.X <= 930 &&
                        pose.Position.Y > 290 && pose.Position.Y < 600, "orbit remains inside scene");
                    TestSuite.Check(pose.Scale > .65 && pose.Scale < 1.05, "bounded perspective");
                }
                TestSuite.Check(front && back, "each prop crosses behind and in front of the fairy");
                var start = MimicratorOrbit.Sample(item, 0);
                var end = MimicratorOrbit.Sample(item, MimicratorOrbit.Period);
                TestSuite.Check(Math.Abs(start.Depth - end.Depth) < .00001 &&
                    Math.Abs(start.Position.X - end.Position.X) < .001, "closed orbit");
                var reference = MimicratorOrbit.Sample(item, 7);
                foreach (int hz in new[] { 30, 60, 120 })
                {
                    double time = 0;
                    for (int frame = 0; frame < hz * 7; frame++) time += 1.0 / hz;
                    var sampled = MimicratorOrbit.Sample(item, time);
                    TestSuite.Check(Math.Abs(sampled.Position.X - reference.Position.X) < .001 &&
                        Math.Abs(sampled.Position.Y - reference.Position.Y) < .001, "frame-rate independent position");
                }
            }
            TestSuite.Throws<ArgumentOutOfRangeException>(() => MimicratorOrbit.Sample(-1, 0), "invalid item");
            TestSuite.Throws<ArgumentOutOfRangeException>(() => MimicratorOrbit.Sample(3, 0), "invalid item");
            TestSuite.Throws<ArgumentOutOfRangeException>(() => MimicratorOrbit.Sample(0, double.NaN), "invalid time");
        }
    }

    public static partial class UiTests
    {
        public static void TestMimicratorPreview()
        {
            using (var game = new Game())
            {
                game.CreateControl();
                TestSuite.Check((Read<ImageAssets>(game, "assetStore").Count - HeroArtCount(game)) == 36, "preview art is lazy");
                game.StartMimicratorPreview();
                TestSuite.Check((Read<ImageAssets>(game, "assetStore").Count - HeroArtCount(game)) == 38, "two shared new images");
                var crops = Read<RectangleF[]>(game, "mimicratorSources");
                var props = Read<Bitmap>(game, "mimicratorProps");
                var body = Read<Bitmap>(game, "mimicratorBody");
                TestSuite.Check(body.GetPixel(0, 0).A == 0 && props.GetPixel(0, 0).A == 0, "real alpha, no baked rectangle");
                for (int i = 0; i < 3; i++)
                    TestSuite.Check(crops[i].Width > 50 && crops[i].Height > 50 &&
                        crops[i].Left >= i * props.Width / 3 && crops[i].Right <= (i + 1) * props.Width / 3,
                        "separate complete sprite inside cell");

                foreach (var size in Sizes)
                {
                    game.ClientSize = size;
                    using (var first = Render(game))
                    {
                        Call(game, "AdvanceMimicrator", .2f);
                        using (var second = Render(game))
                            TestSuite.Check(Digest(first) != Digest(second), "animation changes at " + size);
                    }
                    Key(game, Keys.Space);
                    double time = Read<double>(game, "mimicratorSeconds");
                    using (var before = Render(game))
                    {
                        Call(game, "AdvanceMimicrator", .2f);
                        using (var after = Render(game))
                            TestSuite.Check(Digest(before) == Digest(after), "paused frame stable at " + size);
                    }
                    TestSuite.Check(Read<double>(game, "mimicratorSeconds") == time, "pause freezes clock");
                    Key(game, Keys.Space);
                    Call(game, "AdvanceMimicrator", .1f);
                    TestSuite.Check(Read<double>(game, "mimicratorSeconds") > time, "resume moves");
                    using (var image = Render(game))
                        image.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-" + size.Width + "x" + size.Height + ".png"));
                }

                game.ClientSize = new Size(1280, 840);
                var cache = Read<Bitmap>(game, "mimicratorBackdrop");
                using (var image = Render(game)) { }
                TestSuite.Check(!ReferenceEquals(cache, Read<Bitmap>(game, "mimicratorBackdrop")), "resize replaces backdrop");
                using (var image = Render(game)) { }
                cache = Read<Bitmap>(game, "mimicratorBackdrop");
                using (var image = Render(game)) { }
                TestSuite.Check(ReferenceEquals(cache, Read<Bitmap>(game, "mimicratorBackdrop")), "repaint reuses backdrop");

                // Deactivation halts the shared timer; activation resets the real-time clock.
                Call(game, "OnDeactivate", EventArgs.Empty);
                TestSuite.Check(!Read<Timer>(game, "combatTimer").Enabled, "focus loss stops animation timer");
                double stopped = Read<double>(game, "mimicratorSeconds");
                using (var image = Render(game)) { }
                TestSuite.Check(Read<double>(game, "mimicratorSeconds") == stopped, "paint never advances time");
                Call(game, "OnActivated", EventArgs.Empty);
                TestSuite.Check(Read<Timer>(game, "combatTimer").Enabled, "focus restore resumes animation");

                var watch = Stopwatch.StartNew();
                for (int i = 0; i < 10; i++)
                    using (var image = Render(game)) { }
                watch.Stop();
                TestSuite.Note("Mimicrator offscreen 1280x840 mean repaint: " + (watch.Elapsed.TotalMilliseconds / 10).ToString("F2") + " ms (not an FPS guarantee).");

                Key(game, Keys.Escape);
                TestSuite.Check(!Read<bool>(game, "mimicratorPreview") &&
                    Read<Bitmap>(game, "mimicratorBackdrop") == null, "exit releases scene cache");
                using (var menu = Render(game)) { }
                Click(game, 200, 743);
                TestSuite.Check(Read<bool>(game, "mimicratorPreview"), "main menu opens demo");
                using (var image = Render(game)) { }
                Click(game, 100, 779);
                TestSuite.Check(Read<bool>(game, "mimicratorPaused"), "pause button");
                using (var image = Render(game)) { }
                Click(game, 1100, 51);
                TestSuite.Check(!Read<bool>(game, "mimicratorPreview"), "back button");
                for (int i = 0; i < 12; i++)
                {
                    game.StartMimicratorPreview();
                    using (var image = Render(game)) { }
                    Call(game, "CloseMimicratorPreview");
                }
                TestSuite.Check((Read<ImageAssets>(game, "assetStore").Count - HeroArtCount(game)) == 38, "reopening never duplicates art");
                game.Dispose();
                TestSuite.Check(Read<ImageAssets>(game, "assetStore").Count == 0, "all source layers released");
            }
        }

        public static void TestMimicratorContactSheet()
        {
            using (var game = new Game())
            using (var sheet = new Bitmap(1920, 1260))
            using (var graphics = Graphics.FromImage(sheet))
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                game.StartMimicratorPreview();
                for (int frame = 0; frame < 4; frame++)
                {
                    Write(game, "mimicratorSeconds", frame * 3.0);
                    using (var image = Render(game))
                        graphics.DrawImage(image, frame % 2 * 960, frame / 2 * 630, 960, 630);
                }
                sheet.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-orbit-review.png"));
                TestSuite.Check(true, "four phases rendered for visual review");
            }
        }
    }
}
