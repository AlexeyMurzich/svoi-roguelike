using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    // Native vector badge stays crisp at both status and intent sizes.
    static class VigorIcon
    {
        public static void Draw(Graphics g, RectangleF area)
        {
            var saved = g.Save();
            try
            {
                g.TranslateTransform(area.X, area.Y);
                g.ScaleTransform(area.Width / 48, area.Height / 48);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var shadow = new SolidBrush(Color.FromArgb(150, 0, 0, 0))) g.FillEllipse(shadow, 3, 5, 43, 43);
                using (var fill = new LinearGradientBrush(new Rectangle(3, 2, 42, 42), Color.FromArgb(48, 107, 50), Color.FromArgb(9, 30, 23), 90))
                    g.FillEllipse(fill, 3, 2, 42, 42);
                using (var rim = new Pen(Color.FromArgb(224, 184, 93), 2)) g.DrawEllipse(rim, 3, 2, 42, 42);
                var bolt = new[] { new PointF(27, 7), new PointF(12, 26), new PointF(22, 25),
                    new PointF(19, 39), new PointF(36, 19), new PointF(26, 20) };
                using (var edge = new Pen(Color.FromArgb(20, 42, 19), 3)) g.DrawPolygon(edge, bolt);
                using (var light = new LinearGradientBrush(new Rectangle(12, 7, 24, 32), Color.FromArgb(238, 255, 177), Color.FromArgb(124, 231, 76), 90))
                    g.FillPolygon(light, bolt);
                using (var shine = new Pen(Color.FromArgb(180, 255, 236, 179), 1.3f)) g.DrawArc(shine, 6, 5, 36, 36, 205, 72);
            }
            finally { g.Restore(saved); }
        }
    }
}
