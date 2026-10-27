using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        public static void TestNeckAttachment()
        {
            using (var game = new Game())
            {
                game.CreateControl(); Call(game, "StartTestDogs");
                foreach (float scale in new[] { .6f, 1f, 1.8f })
                {
                    var area = new RectangleF(0, 0, 270 * scale, 245 * scale);
                    var body = (RectangleF)typeof(Game).GetMethod("DogBodyArea", PrivateInstance).Invoke(game, new object[] { area });
                    using (var baseImage = new Bitmap((int)area.Width + 1, (int)area.Height + 1))
                    using (var headImage = new Bitmap(baseImage.Width, baseImage.Height))
                    using (var bg = Graphics.FromImage(baseImage))
                    using (var hg = Graphics.FromImage(headImage))
                    {
                        bg.DrawImage(Read<Image>(game, "dogBody"), body);
                        foreach (bool warm in new[] { false, true })
                            for (int frame = 0; frame < 3; frame++)
                                for (int phase = 0; phase < 6; phase++)
                                {
                                    Write(game, "livingSeconds", phase * .55d);
                                    hg.Clear(Color.Transparent);
                                    Call(game, "DrawDogHead", hg, body, frame, 1f, warm);
                                    int overlap = 0;
                                    for (int y = 0; y < headImage.Height; y += 2)
                                        for (int x = 0; x < headImage.Width; x += 2)
                                            if (baseImage.GetPixel(x, y).A > 220 && headImage.GetPixel(x, y).A > 220) overlap++;
                                    if (overlap <= 20 * scale * scale)
                                    {
                                        using (var diagnostic = new Bitmap(baseImage))
                                        using (var dg = Graphics.FromImage(diagnostic))
                                        { dg.DrawImageUnscaled(headImage, 0, 0); diagnostic.Save(Path.Combine(TestSuite.OutputDirectory, "neck-gap.png")); }
                                    }
                                    TestSuite.Check(overlap > 20 * scale * scale, "opaque neck overlap at pose " + frame + ", scale " + scale + ", phase " + phase + ": " + overlap);
                                }
                        hg.Clear(Color.Transparent);
                        Call(game, "DrawDogHeadBlend", hg, body, 0, 2, .5f, false);
                        string transition = Digest(headImage);
                        hg.Clear(Color.Transparent); Call(game, "DrawDogHead", hg, body, 2, 1f, false);
                        TestSuite.Check(Digest(headImage) == transition, "transition has one opaque face, never two fading heads");
                    }
                }
            }
        }

        public static void TestAnimationCachePolicy()
        {
            using (var game = new Game())
            using (var image = new Bitmap(16, 16))
            using (var target = new Bitmap(180, 100))
            using (var g = Graphics.FromImage(target))
            {
                var cache = Read<object>(game, "actorArtCache");
                var draw = cache.GetType().GetMethod("Draw");
                for (int i = 0; i < 60; i++)
                    draw.Invoke(cache, new object[] { g, image, new RectangleF(0, 0, 20 + i, 40), new RectangleF(0, 0, 16, 16), 1f, false });
                TestSuite.Check((int)cache.GetType().GetProperty("Count").GetValue(cache, null) == 48, "overflow evicts only cold sprites instead of flushing the full cache");
                Call(game, "StartTestDogs");
                TestSuite.Check(Read<Timer>(game, "combatTimer").Interval == 33, "idle uses a bounded 30 Hz repaint cadence");
                Call(game, "EndCombatTurn");
                TestSuite.Check(Read<Timer>(game, "combatTimer").Interval == 16, "attacks use the responsive cadence");
                Call(game, "ReturnToMainMenu");
                Write(game, "guardVisualTime", .3f);
                Call(game, "AdvanceLivingAnimation", .2f);
                TestSuite.Check(Math.Abs(Read<float>(game, "guardVisualTime") - .1f) < .001f, "guard residue decays while no attack is running");
            }
        }
    }
}
