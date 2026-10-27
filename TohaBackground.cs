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

        void ClearTohaBackdrop()
        {
            if (tohaDayFrame != null) tohaDayFrame.Dispose();
            if (tohaNightFrame != null) tohaNightFrame.Dispose();
            tohaDayFrame = tohaNightFrame = null;
            tohaBackgroundSize = Size.Empty;
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
            int top = (int)Math.Round(MapView.Top * DrawingViewport.Scale);
            var size = new Size(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height - top));
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
            float dark = transform ? Motion.Ease(attackTimeline.Time / attackTimeline.Duration) : tohaVisualDark ? 1 : 0;
            var saved = graphics.Save();
            try
            {
                graphics.SetClip(MapView, CombineMode.Intersect);
                graphics.ResetTransform();
                if (dark >= 1) graphics.DrawImageUnscaled(tohaNightFrame, 0, top);
                else
                {
                    graphics.DrawImageUnscaled(tohaDayFrame, 0, top);
                    if (dark > 0)
                        SpriteDrawing.Image(graphics, tohaNightFrame, new RectangleF(0, top, size.Width, size.Height),
                            new RectangleF(0, 0, size.Width, size.Height), dark, false);
                }
            }
            finally { graphics.Restore(saved); }
        }
    }
}
