using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        public static void TestLivingScenes()
        {
            using (var game = new Game())
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                Call(game, "StartTestDogs");
                var fight = Read<CombatState>(game, "combat");
                int hp = fight.Hero.Health, enemyHp = fight.EnemyHealth, turn = fight.Turn;
                string before;
                using (var frame = Render(game)) before = Digest(frame);
                Call(game, "AdvanceLivingAnimation", 1.1f);
                using (var frame = Render(game)) TestSuite.Check(Digest(frame) != before, "idle visibly changes without a card or enemy turn");
                TestSuite.Check(hp == fight.Hero.Health && enemyHp == fight.EnemyHealth && turn == fight.Turn, "idle cannot change combat rules");
                double time = Read<double>(game, "livingSeconds");
                Call(game, "SetInventory", "pause");
                string paused;
                using (var frame = Render(game)) paused = Digest(frame);
                Call(game, "AdvanceLivingAnimation", 10f);
                using (var frame = Render(game)) TestSuite.Check(Digest(frame) == paused, "paused idle is pixel-stable");
                TestSuite.Check(Read<double>(game, "livingSeconds") == time && !Read<Timer>(game, "combatTimer").Enabled, "pause stops clock and timer");
                Call(game, "SetInventory", "");
                TestSuite.Check(Read<Timer>(game, "combatTimer").Enabled, "idle wakes after closing overlay");
                Call(game, "OnDeactivate", EventArgs.Empty);
                TestSuite.Check(!Read<Timer>(game, "combatTimer").Enabled, "focus loss stops idle timer");
                Call(game, "OnActivated", EventArgs.Empty);
                TestSuite.Check(Read<Timer>(game, "combatTimer").Enabled, "focus regain resumes idle even without attack");
                Call(game, "AdvanceLivingAnimation", float.NaN);
                Call(game, "AdvanceLivingAnimation", -1f);
                TestSuite.Check(Read<double>(game, "livingSeconds") == time, "invalid delta ignored");
                foreach (int hz in new[] { 30, 60, 120 })
                {
                    Write(game, "livingSeconds", 0d);
                    for (int i = 0; i < hz * 3; i++) Call(game, "AdvanceLivingAnimation", 1f / hz);
                    TestSuite.Check(Math.Abs(Read<double>(game, "livingSeconds") - 3) < .00001, "idle has the same elapsed time at different refresh rates");
                }
                // Full-opacity paws must remain unchanged while the heads animate.
                Write(game, "livingSeconds", 0d);
                byte[] feet;
                using (var layer = DogLayer(game)) feet = LowerDogPixels(layer);
                Write(game, "livingSeconds", 2.1d);
                using (var layer = DogLayer(game))
                {
                    byte[] after = LowerDogPixels(layer);
                    bool equal = true;
                    for (int i = 0; i < feet.Length; i++) if (feet[i] != after[i]) { equal = false; break; }
                    TestSuite.Check(equal, "idle does not jitter or fade dog legs");
                }
                Call(game, "ReturnToMainMenu");
                TestSuite.Check(Read<double>(game, "livingSeconds") == 0, "scene reset clears elapsed time");
                var cache = Read<object>(game, "actorArtCache");
                TestSuite.Check((int)cache.GetType().GetProperty("Count").GetValue(cache, null) == 0, "scene reset frees derived actor bitmaps");
                Call(game, "StartTestDogs");
                using (Render(game)) { }
                long cacheBytes = (long)cache.GetType().GetProperty("Bytes").GetValue(cache, null);
                int cacheCount = (int)cache.GetType().GetProperty("Count").GetValue(cache, null);
                uint handles;
                using (var process = System.Diagnostics.Process.GetCurrentProcess()) handles = GetGuiResources(process.Handle, 0);
                for (int frame = 0; frame < 60; frame++)
                {
                    Call(game, "AdvanceLivingAnimation", .1f);
                    using (Render(game)) { }
                }
                TestSuite.Check((long)cache.GetType().GetProperty("Bytes").GetValue(cache, null) == cacheBytes && cacheBytes < 24 * 1024 * 1024,
                    "continuous motion reuses bounded display-size sprites");
                TestSuite.Check((int)cache.GetType().GetProperty("Count").GetValue(cache, null) == cacheCount, "animation phases do not create cache entries");
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                    TestSuite.Check(GetGuiResources(process.Handle, 0) <= handles + 2, "idle does not leak GDI resources");
                game.Dispose();
                TestSuite.Check((long)cache.GetType().GetProperty("Bytes").GetValue(cache, null) == 0, "Dispose frees the display cache");
            }
        }

        public static void TestLivingGallery()
        {
            using (var game = new Game())
            using (var sheet = new Bitmap(1280, 7 * 280))
            using (var board = Graphics.FromImage(sheet))
            {
                game.CreateControl(); game.ClientSize = new Size(1280, 840);
                Call(game, "StartTestBattle");
                board.Clear(Color.FromArgb(24, 30, 38));
                for (int row = 0; row < 7; row++)
                {
                    int hero = row < 4 ? row : 3;
                    var kind = row == 5 ? EnemyKind.StrayDogs : row == 6 ? EnemyKind.Hedgehogs : EnemyKind.Rat;
                    ReplaceFight(game, new HeroState(hero, 50, 0, new Balance()), kind);
                    for (int frame = 0; frame < 4; frame++)
                    {
                        Write(game, "livingSeconds", frame * .6d);
                        using (var image = Render(game))
                        {
                            var crop = row < 4 ? new Rectangle(100, 190, 410, 360) : new Rectangle(620, 230, 600, 340);
                            board.DrawImage(image, new Rectangle(frame * 320, row * 280 + 24, 320, 250), crop, GraphicsUnit.Pixel);
                            if (frame == 1) image.Save(Path.Combine(TestSuite.OutputDirectory, "living-scene-" + row + ".png"));
                        }
                        using (var font = new Font("Segoe UI", 11)) board.DrawString("Actor " + row + " / " + (frame * .6).ToString("F1") + "s", font, Brushes.White, frame * 320 + 8, row * 280);
                    }
                }
                sheet.Save(Path.Combine(TestSuite.OutputDirectory, "living-contact-sheet.png"));
                TestSuite.Check((Read<ImageAssets>(game, "assetStore").Count - HeroArtCount(game)) == 37, "all idle effects reuse existing source art");
            }
        }
    }
}
