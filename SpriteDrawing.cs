using System.Drawing;
using System.Drawing.Imaging;

namespace ShpilSvoih
{
    static class SpriteDrawing
    {
        public static void Image(Graphics graphics, Image image, RectangleF target, RectangleF source, float opacity, bool warm)
        {
            if (opacity <= 0) return;
            using (var attributes = new ImageAttributes())
            {
                var color = new ColorMatrix();
                color.Matrix33 = opacity;
                if (warm) { color.Matrix11 = .77f; color.Matrix22 = .58f; }
                attributes.SetColorMatrix(color);
                attributes.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                graphics.DrawImage(image, new[] { new PointF(target.Left, target.Top), new PointF(target.Right, target.Top), new PointF(target.Left, target.Bottom) }, source, GraphicsUnit.Pixel, attributes);
            }
        }
        public static void FitBottom(Graphics graphics, Image image, RectangleF area, float opacity)
        {
            float scale = System.Math.Min(area.Width / image.Width, area.Height / image.Height);
            var target = new RectangleF(area.X + (area.Width - image.Width * scale) / 2, area.Bottom - image.Height * scale, image.Width * scale, image.Height * scale);
            Image(graphics, image, target, new RectangleF(0, 0, image.Width, image.Height), opacity, false);
        }
    }
}
