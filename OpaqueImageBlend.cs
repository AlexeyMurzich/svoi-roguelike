using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ShpilSvoih
{
    // Owns two viewport-sized native copies only during the transition.
    // Copy the base inside the acquired DC too: a bitmap Graphics.GetHdc()
    // can expose a temporary surface whose previous pixels are undefined.
    sealed class OpaqueImageBlend : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct Blend { public byte Operation, Flags, Alpha, Format; }
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] static extern int SaveDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool RestoreDC(IntPtr dc, int saved);
        [DllImport("gdi32.dll")] static extern int IntersectClipRect(IntPtr dc, int left, int top, int right, int bottom);
        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr destination, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
        [DllImport("msimg32.dll")] static extern bool AlphaBlend(IntPtr destination, int x, int y, int width, int height,
            IntPtr source, int sourceX, int sourceY, int sourceWidth, int sourceHeight, Blend blend);
        IntPtr baseDc, baseBitmap, basePrevious, dc, bitmap, previous;
        readonly Size size;
        public OpaqueImageBlend(Bitmap background, Bitmap foreground)
        {
            if (background == null || foreground == null) throw new ArgumentNullException("background");
            if (background.Size != foreground.Size) throw new ArgumentException("Backdrop sizes differ.");
            size = foreground.Size;
            try
            {
                Create(background, out baseDc, out baseBitmap, out basePrevious);
                Create(foreground, out dc, out bitmap, out previous);
            }
            catch { Dispose(); throw; }
        }
        static void Create(Bitmap source, out IntPtr context, out IntPtr image, out IntPtr old)
        {
            context = CreateCompatibleDC(IntPtr.Zero); image = old = IntPtr.Zero;
            if (context == IntPtr.Zero) throw new InvalidOperationException("Cannot create backdrop DC.");
            image = source.GetHbitmap(); old = SelectObject(context, image);
            if (old == IntPtr.Zero || old == new IntPtr(-1)) throw new InvalidOperationException("Cannot select backdrop bitmap.");
        }
        public bool Draw(Graphics graphics, int x, int y, float opacity)
        {
            if (dc == IntPtr.Zero) return false;
            var clip = Rectangle.Ceiling(graphics.VisibleClipBounds);
            IntPtr destination = graphics.GetHdc();
            int saved = SaveDC(destination);
            try
            {
                if (saved == 0) return false;
                IntersectClipRect(destination, clip.Left, clip.Top, clip.Right, clip.Bottom);
                byte alpha = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(opacity * 255)));
                return BitBlt(destination, x, y, size.Width, size.Height, baseDc, 0, 0, 0x00CC0020) &&
                    AlphaBlend(destination, x, y, size.Width, size.Height, dc, 0, 0, size.Width, size.Height, new Blend { Alpha = alpha });
            }
            finally
            {
                if (saved != 0) RestoreDC(destination, saved);
                graphics.ReleaseHdc(destination);
            }
        }
        static void Release(ref IntPtr context, ref IntPtr image, ref IntPtr old)
        {
            if (context != IntPtr.Zero)
            {
                if (old != IntPtr.Zero && old != new IntPtr(-1)) SelectObject(context, old);
                DeleteDC(context); context = IntPtr.Zero;
            }
            if (image != IntPtr.Zero) { DeleteObject(image); image = IntPtr.Zero; }
            old = IntPtr.Zero;
        }
        public void Dispose()
        {
            Release(ref dc, ref bitmap, ref previous); Release(ref baseDc, ref baseBitmap, ref basePrevious);
        }
    }
}
