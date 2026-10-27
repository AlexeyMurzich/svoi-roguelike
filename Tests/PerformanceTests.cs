using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        public static void TestRenderBudget()
        {
            using (var game = new Game())
            {
                game.CreateControl(); var handle = game.Handle;
                game.ClientSize = new Size(3840, 2160); Call(game, "StartTestHedgehogs");
                var viewport = game.DrawingViewport;
                using (var image = Render(game)) image.Save(System.IO.Path.Combine(TestSuite.OutputDirectory, "render-budget-4k.png"));
                TestSuite.Check(game.ClientSize == new Size(3840, 2160), "native test window really remains 4K after first paint");
                var surface = Read<Bitmap>(game, "renderSurface");
                TestSuite.Check(surface.Width * surface.Height <= 1920 * 1080 && surface.Width == 1920, "4K rasterization has a fixed pixel budget");
                using (Render(game)) { }
                TestSuite.Check(object.ReferenceEquals(surface, Read<Bitmap>(game, "renderSurface")), "repaints reuse the same render surface");
                TestSuite.Check(game.DrawingViewport.Scale == viewport.Scale && game.DrawingViewport.Width == viewport.Width && Read<float>(game, "renderDensity") == 1, "presentation never changes native pointer coordinates");
                Click(game, game.DrawingViewport.Width * .84f, 430);
                TestSuite.Check(Read<CombatState>(game, "combat").SelectedTarget == 1, "native 4K pointer selects the right enemy through the reduced rendering surface");
                using (Render(game)) { }
                game.ClientSize = new Size(1280, 840);
                using (Render(game)) { }
                TestSuite.Check(Read<Bitmap>(game, "renderSurface") == null, "returning to normal size frees the large-screen surface");
                TestSuite.Throws<ArgumentException>(() => surface.GetPixel(0, 0), "old render surface is actually disposed");
                game.ClientSize = new Size(2560, 1080);
                using (Render(game)) { }
                surface = Read<Bitmap>(game, "renderSurface"); game.Dispose();
                TestSuite.Throws<ArgumentException>(() => surface.GetPixel(0, 0), "closing game frees the render surface");
            }
        }

        public static void TestFastCompositing()
        {
            using (var game = new Game())
            using (var target = new Bitmap(1280, 840, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
            using (var graphics = Graphics.FromImage(target))
            {
                game.CreateControl(); game.ClientSize = target.Size;
                Call(game, "StartTestIllyaToha");
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.CompositingMode = CompositingMode.SourceOver;
                Call(game, "DrawTohaBackground", graphics);
                TestSuite.Check(graphics.CompositingMode == CompositingMode.SourceOver, "opaque Toha blit restores alpha compositing for actors");
                TestSuite.Check(target.GetPixel(640, 400).A == 255, "Toha backdrop remains opaque");
                var cache = Read<object>(game, "actorArtCache");
                using (var source = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                {
                    using (var canvas = Graphics.FromImage(source)) canvas.Clear(Color.FromArgb(128, 240, 20, 40));
                    graphics.Clear(Color.Blue);
                    cache.GetType().GetMethod("Draw").Invoke(cache, new object[] { graphics, source, new RectangleF(10, 10, 64, 64), new RectangleF(0, 0, 16, 16), 1f, false });
                    var blended = target.GetPixel(40, 40);
                    TestSuite.Check(blended.A == 255 && blended.R > 100 && blended.B > 100, "prepared transparent sprites still blend with the scene");
                    TestSuite.Check(graphics.InterpolationMode == InterpolationMode.HighQualityBicubic, "fast sprite sampling restores the caller's interpolation");
                    cache.GetType().GetMethod("Clear").Invoke(cache, null);
                }
                Call(game, "StartTestDogs");
                Call(game, "DrawSceneBackground", graphics, true);
                TestSuite.Check(graphics.CompositingMode == CompositingMode.SourceOver, "ordinary backdrop also restores foreground compositing");
            }
        }

        public static void TestScaledCacheLimits()
        {
            var type = typeof(Game).Assembly.GetType("ShpilSvoih.ScaledImageCache", true);
            using (var cache = (IDisposable)Activator.CreateInstance(type, new object[] { 32768L, 3, 4 }))
            using (var source = new Bitmap(16, 16))
            using (var destination = new Bitmap(140, 140))
            using (var graphics = Graphics.FromImage(destination))
            {
                var draw = type.GetMethod("Draw");
                Func<string, long> stat = name => Convert.ToInt64(type.GetProperty(name).GetValue(cache, null));
                Action<float> paint = width => draw.Invoke(cache, new object[] { graphics, source,
                    new RectangleF(0, 0, width, 20), new RectangleF(0, 0, 16, 16), 1f, false });
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                paint(20); paint(24); paint(28); paint(20); paint(32);
                TestSuite.Check(stat("Count") == 3 && stat("Evictions") == 1, "LRU evicts one cold image, never flushes the whole cache");
                long misses = stat("Misses"); paint(20);
                TestSuite.Check(stat("Misses") == misses, "recently used image survives eviction");
                paint(24);
                TestSuite.Check(stat("Misses") == misses + 1, "least recently used image is recreated on demand");
                misses = stat("Misses"); paint(21); paint(22); paint(23);
                TestSuite.Check(stat("Misses") == misses, "nearby animation sizes reuse a resample while destination dimensions remain exact");
                TestSuite.Check(graphics.InterpolationMode == InterpolationMode.NearestNeighbor, "cached draw restores graphics interpolation");
                for (int size = 4; size < 140; size += 3)
                {
                    paint(size);
                    TestSuite.Check(stat("Bytes") <= 32768 && stat("Count") <= 3, "cache stays within byte and entry limits");
                }
                int count = (int)stat("Count");
                draw.Invoke(cache, new object[] { graphics, source, new RectangleF(0, 0, 140, 140), new RectangleF(0, 0, 16, 16), 1f, false });
                TestSuite.Check(stat("Count") == count, "oversized image bypasses cache without evicting useful entries");
                type.GetMethod("SetScale").Invoke(cache, new object[] { 2f });
                TestSuite.Check(stat("Count") == 0 && stat("Bytes") == 0, "resize frees all old display-sized copies");
                paint(20); cache.Dispose(); cache.Dispose();
                TestSuite.Check(stat("Count") == 0 && stat("Bytes") == 0 && source.Width == 16, "repeated dispose frees copies without disposing the source");
            }
        }

        public static void TestInterfaceCacheReuse()
        {
            using (var game = new Game())
            {
                game.CreateControl(); Call(game, "StartTestDogs");
                var cache = Read<object>(game, "interfaceArtCache");
                Func<string, long> stat = name => Convert.ToInt64(cache.GetType().GetProperty(name).GetValue(cache, null));
                using (Render(game)) { }
                long misses = stat("Misses"), hits = stat("Hits");
                for (int frame = 0; frame < 15; frame++)
                {
                    Call(game, "AdvanceLivingAnimation", .1f);
                    using (Render(game)) { }
                }
                TestSuite.Check(stat("Misses") == misses && stat("Hits") > hits, "animated combat reuses existing HUD and intent bitmaps");
                TestSuite.Check(stat("Bytes") <= 8 * 1024 * 1024 && stat("Count") <= 64, "HUD cache has its own fixed memory budget");
                game.Dispose();
                TestSuite.Check(stat("Bytes") == 0 && stat("Count") == 0, "closing the game releases the HUD cache");
            }
        }
    }
}
