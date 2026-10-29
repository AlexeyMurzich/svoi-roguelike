using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Image hudTexture, healthIcon, coinIcon;
        bool hoveredRelic;
        static readonly RectangleF RelicHitArea = new RectangleF(16, 65, 62, 58);
        // Source regions refer to the approved full-size sprites. Rendering a crop
        // reuses the owned image; no extra face bitmaps are allocated on resize.
        static readonly RectangleF[] PortraitSources =
        {
            new RectangleF(.425f, .012f, .235f, .157f),
            new RectangleF(.463f, .005f, .220f, .147f),
            new RectangleF(.418f, .006f, .230f, .153f),
            new RectangleF(.395f, .025f, .205f, .137f)
        };
        void DrawHudBackground(Graphics g)
        {
            var area = new RectangleF(0, 0, CanvasWidth, MapLayout.HudHeight);
            DrawInterfaceImage(g, hudTexture, area, Viewport.CoverSource(hudTexture.Size, area.Size));
            using (var shade = new LinearGradientBrush(area, Color.FromArgb(70, 7, 20, 21), Color.FromArgb(175, 6, 19, 20), 90f))
                g.FillRectangle(shade, area);
            using (var line = new Pen(Color.FromArgb(115, gold)))
                g.DrawLine(line, 0, MapLayout.HudHeight - 1, CanvasWidth, MapLayout.HudHeight - 1);
        }

        void DrawHeroPortrait(Graphics g)
        {
            var area = new RectangleF(23, 5, 54, 54);
            var sprite = sprites[selected];
            var region = PortraitSources[selected];
            var source = new RectangleF(region.X * sprite.Width, region.Y * sprite.Height, region.Width * sprite.Width, region.Height * sprite.Height);
            var saved = g.Save();
            try
            {
                using (var clip = new GraphicsPath())
                {
                    clip.AddEllipse(area);
                    g.SetClip(clip, CombineMode.Intersect);
                    Rect(g, area.X, area.Y, area.Width, area.Height, surface);
                    DrawInterfaceImage(g, sprite, area, source);
                }
            }
            finally
            {
                g.Restore(saved);
            }

            using (var frame = new Pen(gold, 2))
                g.DrawEllipse(frame, area);
            using (var frame = new Pen(Color.FromArgb(90, gold), 1))
                g.DrawEllipse(frame, area.X - 3, area.Y - 3, area.Width + 6, area.Height + 6);
        }

        void SetRelicHover(bool value)
        {
            if (hoveredRelic == value)
                return;
            hoveredRelic = value;
            // Hover must preserve the current frame's click targets.
            Invalidate();
        }

        void OnHudMouseLeave(object sender, EventArgs e)
        {
            combatPointer = null;
            hoveredCombatCard = -1;
            routePressed = false;
            SetRelicHover(false);
            hoveredMapNode = -1;
            Invalidate();
        }

        Font HudFont(float size, bool bold)
        {
            var key = Tuple.Create(size, bold);
            Font font;
            if (!fonts.TryGetValue(key, out font))
            {
                font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
                fonts.Add(key, font);
            }

            return font;
        }

        void DrawRelicTooltip(Graphics g)
        {
            var relic = currentHero.Effects.Chosen;
            const float x = 16, y = 134, padding = 18;
            float width = Math.Min(380, CanvasWidth - 32), inner = width - padding * 2;
            var titleFont = HudFont(20, true);
            var bodyFont = HudFont(16, false);
            float titleHeight = (float)Math.Ceiling(g.MeasureString(relic.Name, titleFont, (int)inner).Height);
            float bodyHeight = (float)Math.Ceiling(g.MeasureString(relic.Description, bodyFont, (int)inner).Height);
            float height = padding * 2 + titleHeight + 10 + bodyHeight;
            Rect(g, x + 4, y + 5, width, height, Color.FromArgb(180, 0, 0, 0));
            Rect(g, x, y, width, height, Color.FromArgb(255, 12, 27, 25));
            RelicFrame(g, x, y, width, height, true);
            using (var brush = new SolidBrush(gold))
                g.DrawString(relic.Name, titleFont, brush, new RectangleF(x + padding, y + padding, inner, titleHeight + 2));
            using (var brush = new SolidBrush(text))
                g.DrawString(relic.Description, bodyFont, brush, new RectangleF(x + padding, y + padding + titleHeight + 10, inner, bodyHeight + 2));
        }
    }
}
