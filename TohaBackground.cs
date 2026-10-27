using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Bitmap tohaDayFrame, tohaNightFrame;
        Size tohaBackgroundSize;
        OpaqueImageBlend tohaBlend;
        bool tohaBlendFailed;

        void ClearTohaBackdrop()
        {
            ClearTohaBlend();
            tohaBlendFailed = false;
            if (tohaDayFrame != null) tohaDayFrame.Dispose();
            if (tohaNightFrame != null) tohaNightFrame.Dispose();
            tohaDayFrame = tohaNightFrame = null;
            tohaBackgroundSize = Size.Empty;
        }

        void ClearTohaBlend()
        {
            if (tohaBlend != null) tohaBlend.Dispose();
            tohaBlend = null;
        }

        void BlendTohaBackdrop(Graphics graphics, int top, float opacity)
        {
            if (tohaBlend == null && !tohaBlendFailed)
            {
                try { tohaBlend = new OpaqueImageBlend(tohaDayFrame, tohaNightFrame); }
                catch (InvalidOperationException) { tohaBlendFailed = true; }
                catch (System.Runtime.InteropServices.ExternalException) { tohaBlendFailed = true; }
                catch (OutOfMemoryException) { tohaBlendFailed = true; }
            }
            if (tohaBlend != null && tohaBlend.Draw(graphics, 0, top, opacity)) return;
            // A rectangular GDI+ fallback is still much faster than an affine color-matrix draw.
            using (var attributes = new ImageAttributes())
            {
                var color = new ColorMatrix(); color.Matrix33 = opacity; attributes.SetColorMatrix(color);
                graphics.DrawImage(tohaNightFrame, new Rectangle(0, top, tohaNightFrame.Width, tohaNightFrame.Height),
                    0, 0, tohaNightFrame.Width, tohaNightFrame.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        static Bitmap PrepareTohaBackdrop(Image source, Size size)
        {
            var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            try
            {
                using (var canvas = Graphics.FromImage(bitmap))
                {
                    canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    canvas.DrawImage(source, new Rectangle(Point.Empty, size), Viewport.CoverSource(source.Size, size), GraphicsUnit.Pixel);
                    using (var shade = new SolidBrush(Color.FromArgb(26, 7, 16, 21))) canvas.FillRectangle(shade, 0, 0, size.Width, size.Height);
                }
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }

        void DrawTohaBackground(Graphics graphics)
        {
            LoadTohaArt();
            int top = (int)Math.Round(MapView.Top * RenderViewport.Scale);
            var size = new Size(Math.Max(1, RenderPixels.Width), Math.Max(1, RenderPixels.Height - top));
            if (tohaDayFrame == null || tohaBackgroundSize != size)
            {
                ClearTohaBackdrop();
                try
                {
                    tohaDayFrame = PrepareTohaBackdrop(tohaDay, size);
                    tohaNightFrame = PrepareTohaBackdrop(tohaNight, size);
                    tohaBackgroundSize = size;
                }
                catch { ClearTohaBackdrop(); throw; }
            }
            bool transform = attackTimeline.Kind == AttackAnimation.TohaTransform;
            if (!transform) ClearTohaBlend();
            float dark = transform ? Motion.Ease(attackTimeline.Time / attackTimeline.Duration) : tohaVisualDark ? 1 : 0;
            var saved = graphics.Save();
            try
            {
                graphics.SetClip(MapView, CombineMode.Intersect);
                graphics.ResetTransform();
                graphics.CompositingMode = CompositingMode.SourceCopy;
                if (dark >= 1) graphics.DrawImageUnscaled(tohaNightFrame, 0, top);
                else
                {
                    graphics.DrawImageUnscaled(tohaDayFrame, 0, top);
                    graphics.CompositingMode = CompositingMode.SourceOver;
                    if (dark > 0)
                        BlendTohaBackdrop(graphics, top, dark);
                }
            }
            finally { graphics.Restore(saved); }
        }
    }
}
