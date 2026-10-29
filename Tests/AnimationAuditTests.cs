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
                TestSuite.Check(typeof(Game).GetMethod("DrawDogHead", PrivateInstance) == null, "no separate dog-head drawing path remains");
                var names = typeof(Game).Assembly.GetManifestResourceNames();
                TestSuite.Check(Array.IndexOf(names, "enemies.dog-body.png") < 0 && Array.IndexOf(names, "enemies.stray-dogs.png") < 0, "legacy separated body/head atlases are not shipped");
                var atlas = Read<object>(game, "dogPoses");
                TestSuite.Check((int)atlas.GetType().GetProperty("FrameCount").GetValue(atlas, null) == 6, "two dogs each have three full-body poses");
                foreach (float scale in new[] { .6f, 1f, 1.8f })
                    for (int dog = 0; dog < 2; dog++)
                    {
                        float reference = 0;
                        for (int pose = 0; pose < 3; pose++)
                        {
                            var slot = new RectangleF(30 * scale, 0, 270 * scale, 245 * scale);
                            var source = (RectangleF)atlas.GetType().GetMethod("Source").Invoke(atlas, new object[] { dog, pose });
                            var target = (RectangleF)atlas.GetType().GetMethod("Target").Invoke(atlas, new object[] { dog, pose, slot });
                            float ratio = target.Width / source.Width;
                            if (pose == 0) reference = ratio;
                            TestSuite.Check(Math.Abs(ratio - reference) < .00001f && Math.Abs(target.Height / source.Height - ratio) < .00001f, "all complete poses keep one anatomical scale");
                            TestSuite.Check(Math.Abs(target.Bottom - slot.Bottom) < .001f, "all full-body frames are grounded");
                            using (var layer = new Bitmap((int)(340 * scale), (int)(250 * scale), PixelFormat.Format32bppPArgb))
                            using (var graphics = Graphics.FromImage(layer))
                            {
                                atlas.GetType().GetMethod("Draw").Invoke(atlas, new object[] { graphics, dog, pose, slot });
                                var support = DogSilhouette(layer);
                                TestSuite.Check(support.Item1 > 1500 * scale * scale && support.Item2 > .97f, "head, neck, torso and legs belong to one connected opaque creature");
                                TestSuite.Check(Math.Abs(support.Item3 - slot.Bottom) <= 3, "actual paw alpha sits on the floor");
                            }
                        }
                    }
                var timeline = Read<AttackTimeline>(game, "attackTimeline");
                for (int dog = 0; dog < 2; dog++) for (int pose = 0; pose < 3; pose++)
                {
                    timeline.Cancel(); Write(game, "actingEnemyIndex", dog);
                    if (pose > 0)
                    {
                        timeline.Start(pose == 1 ? AttackAnimation.DogBite : AttackAnimation.DogHowl, () => { });
                        timeline.Advance(.3f / AttackTimeline.Speed);
                    }
                    var area = (RectangleF)typeof(Game).GetMethod("EnemyArea", PrivateInstance).Invoke(game, new object[] { dog });
                    var aim = (PointF)typeof(Game).GetMethod("DogTargetPoint", PrivateInstance).Invoke(game, new object[] { dog });
                    using (var layer = new Bitmap(1280, 840, PixelFormat.Format32bppPArgb))
                    using (var graphics = Graphics.FromImage(layer))
                    {
                        Call(game, "DrawDogPoseFor", graphics, area, dog);
                        TestSuite.Check(layer.GetPixel((int)aim.X, (int)aim.Y).A > 128, "projectile aim is on the actual dog, not above it");
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
                    draw.Invoke(cache, new object[] { g, image, new RectangleF(0, 0, 20 + i * 4, 40), new RectangleF(0, 0, 16, 16), 1f, false });
                TestSuite.Check((int)cache.GetType().GetProperty("Count").GetValue(cache, null) == 48, "overflow evicts only cold sprites instead of flushing the full cache");
                Call(game, "StartTestDogs");
                TestSuite.Check(Read<Timer>(game, "combatTimer").Interval == 30, "idle uses a bounded 30 Hz repaint cadence");
                Call(game, "EndCombatTurn");
                TestSuite.Check(Read<Timer>(game, "combatTimer").Interval == 15, "attacks use the responsive cadence");
                Call(game, "ReturnToMainMenu");
                Write(game, "guardVisualTime", .3f);
                Call(game, "AdvanceLivingAnimation", .2f);
                TestSuite.Check(Math.Abs(Read<float>(game, "guardVisualTime") - .1f) < .001f, "guard residue decays while no attack is running");
            }
        }
    }
}
