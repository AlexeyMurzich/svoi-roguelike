using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Bitmap checkoutBackdrop;
        float checkoutBackdropScale;
        void ClearCheckoutBackdrop()
        {
            if (checkoutBackdrop != null) checkoutBackdrop.Dispose();
            checkoutBackdrop = null;
        }
        void DrawCheckoutBackdrop(Graphics graphics)
        {
            var viewport = RenderViewport;
            var size = new Size(Math.Max(1, (int)Math.Ceiling(1280 * viewport.Scale)), Math.Max(1, (int)Math.Ceiling(840 * viewport.Scale)));
            if (checkoutBackdrop == null || checkoutBackdrop.Size != size || checkoutBackdropScale != viewport.Scale)
            {
                ClearCheckoutBackdrop();
                checkoutBackdropScale = viewport.Scale;
                checkoutBackdrop = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                using (var canvas = Graphics.FromImage(checkoutBackdrop))
                {
                    canvas.CompositingMode = CompositingMode.SourceCopy;
                    canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    canvas.DrawImage(checkoutBackground, new Rectangle(Point.Empty, size), Viewport.CoverSource(checkoutBackground.Size, new SizeF(1280, 840)), GraphicsUnit.Pixel);
                }
            }
            var saved = graphics.Save();
            try
            {
                graphics.ResetTransform();
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.DrawImageUnscaled(checkoutBackdrop, (int)Math.Round(viewport.OffsetX), (int)Math.Round(viewport.OffsetY));
            }
            finally { graphics.Restore(saved); }
        }
    }
}
