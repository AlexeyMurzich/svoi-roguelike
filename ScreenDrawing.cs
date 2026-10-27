using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        void Rect(Graphics g, float x, float y, float w, float h, Color c)
        {
            using (var b = new SolidBrush(c))
                g.FillRectangle(b, x, y, w, h);
        }

        void TextAt(Graphics g, string value, float x, float y, float size, Color color, float width, bool bold = false)
        {
            Font font = HudFont(size, bold);
            using (var brush = new SolidBrush(color))
                g.DrawString(value, font, brush, new RectangleF(x, y, width, 500));
        }

        void Stroke(Graphics g, float x, float y, float w, float h, Color c)
        {
            using (var p = new Pen(c, 1))
                g.DrawRectangle(p, x, y, w, h);
        }

        void Button(Graphics g, string label, float x, float y, float w, float h, Action action, bool primary = false)
        {
            Rect(g, x, y, w, h, primary ? colors[selected] : Color.FromArgb(40, 48, 58));
            TextAt(g, label, x + 16, y + (h - 22) / 2, 15, primary ? background : text, w - 25, true);
            actions.Add(Tuple.Create(new RectangleF(x, y, w, h), action));
        }

        void Portrait(Graphics g, int id, float x, float y, float width, float height)
        {
            if (sprites.ContainsKey(id))
            {
                var im = sprites[id];
                float r = Math.Min(width / im.Width, height / im.Height);
                var saved = g.Save();
                try
                {
                    if (!mapScreen) LivingMotion.Body(g, new RectangleF(x, y, width, height), livingSeconds, id, id == 0);
                    actorArtCache.Draw(g, im, new RectangleF(x + (width - im.Width * r) / 2, y + height - im.Height * r, im.Width * r, im.Height * r), new RectangleF(0, 0, im.Width, im.Height), 1, false);
                }
                finally { g.Restore(saved); }
                return;
            }

            float size = Math.Min(width, height);
            float xx = x + (width - size) / 2, yy = y + (height - size) / 2;
            using (var p = new Pen(colors[id], 2))
            {
                g.DrawEllipse(p, xx + size * .3f, yy + size * .1f, size * .4f, size * .4f);
                g.DrawArc(p, xx + size * .16f, yy + size * .52f, size * .68f, size * .6f, 180, 180);
                g.DrawLine(p, xx + size * .16f, yy + size * .82f, xx + size * .84f, yy + size * .82f);
            }

            TextAt(g, new[] { "П", "И", "Р", "Ш" }[id], xx + size * .40f, yy + size * .19f, size * .22f, colors[id], size * .4f, true);
        }

    }
}
