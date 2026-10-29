using System;
using System.Drawing;
using System.IO;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        public static void TestBattleBackground()
        {
            foreach (var size in Sizes)
                using (var game = new Game())
                {
                    game.CreateControl(); game.ClientSize = size;
                    Call(game, "StartTestBattle");
                    using (var frame = Render(game))
                        frame.Save(Path.Combine(TestSuite.OutputDirectory, "social-battle-" + size.Width + "x" + size.Height + ".png"));
                    var ordinary = Read<Image>(game, "ordinaryCombatBackground");
                    TestSuite.Check(object.ReferenceEquals(Read<Image[]>(game, "districtArt")[0], ordinary), "ordinary battle selects social art");
                    var cache = Read<Bitmap>(game, "districtBackdrop");
                    using (Render(game)) { }
                    TestSuite.Check(object.ReferenceEquals(cache, Read<Bitmap>(game, "districtBackdrop")), "idle paint reuses background cache");
                    Call(game, "StartTestMimicrator");
                    using (var frame = Render(game)) frame.Save(Path.Combine(TestSuite.OutputDirectory, "mimicrator-courtyard-" + size.Width + "x" + size.Height + ".png"));
                    TestSuite.Check(object.ReferenceEquals(Read<Image>(game, "sceneSource"), Read<Image>(game, "eliteCombatBackground")), "elite uses approved courtyard");
                    TestSuite.Check(Read<Bitmap>(game, "districtBackdrop") == null && Read<Bitmap>(game, "sceneBackground") != null, "elite releases district cache and prepares its own scene");
                    TestSuite.Throws<ArgumentException>(() => cache.GetPixel(0, 0), "switching scene disposes the previous district bitmap");
                    Call(game, "ReturnToMainMenu");
                    Call(game, "OpenCheckout"); Call(game, "TakeBuff"); Call(game, "OpenActMap");
                    using (Render(game)) { }
                    TestSuite.Check(object.ReferenceEquals(Read<Image>(game, "sceneSource"), Read<Image>(game, "mapBackground")), "world map keeps original art");
                    TestSuite.Check(!Read<bool>(game, "sceneIsCombat"), "world map retains scrolling background mode");
                }
        }
    }
}
