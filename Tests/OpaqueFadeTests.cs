using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        public static void TestOpaqueFade()
        {
            var type = typeof(Game).Assembly.GetType("ShpilSvoih.OpaqueImageBlend", true);
            using (var source = new Bitmap(16, 16, PixelFormat.Format32bppPArgb))
            using (var background = new Bitmap(16, 16, PixelFormat.Format32bppPArgb))
            using (var backgroundGraphics = Graphics.FromImage(background))
            using (var destination = new Bitmap(16, 16, PixelFormat.Format32bppPArgb))
            using (var sourceGraphics = Graphics.FromImage(source))
            using (var graphics = Graphics.FromImage(destination))
            using (var process = Process.GetCurrentProcess())
            {
                sourceGraphics.Clear(Color.Red); backgroundGraphics.Clear(Color.Blue);
                using (var warm = (IDisposable)Activator.CreateInstance(type, new object[] { background, source }))
                    type.GetMethod("Draw").Invoke(warm, new object[] { graphics, 0, 0, .5f });
                uint initial = GetGuiResources(process.Handle, 0);
                for (int cycle = 0; cycle < 12; cycle++)
                    using (var blend = (IDisposable)Activator.CreateInstance(type, new object[] { background, source }))
                    {
                        graphics.ResetClip(); graphics.Clear(Color.Blue);
                        bool painted = (bool)type.GetMethod("Draw").Invoke(blend, new object[] { graphics, 0, 0, .5f });
                        var color = destination.GetPixel(8, 8);
                        TestSuite.Check(painted && Math.Abs(color.R - 128) <= 2 && Math.Abs(color.B - 127) <= 2 && color.A == 255, "constant alpha mixes opaque backgrounds with correct colors");
                        graphics.Clear(Color.Blue); graphics.SetClip(new Rectangle(4, 4, 8, 8));
                        type.GetMethod("Draw").Invoke(blend, new object[] { graphics, 0, 0, .5f });
                        TestSuite.Check(destination.GetPixel(0, 0).ToArgb() == Color.Blue.ToArgb(), "native fade respects the caller's clip");
                        blend.Dispose();
                        TestSuite.Check(!(bool)type.GetMethod("Draw").Invoke(blend, new object[] { graphics, 0, 0, .5f }), "disposed fade cannot use native handles");
                    }
                TestSuite.Note("fade GDI handles after warmup: " + initial + " -> " + GetGuiResources(process.Handle, 0));
                TestSuite.Check(GetGuiResources(process.Handle, 0) <= initial + 1, "repeated fades release bitmap and DC handles");
                TestSuite.Check(source.GetPixel(8, 8).ToArgb() == Color.Red.ToArgb(), "fade never disposes or alters the original image");
            }
        }
    }
}
