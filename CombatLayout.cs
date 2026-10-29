using System;
using System.Drawing;

namespace ShpilSvoih
{
    public static class CombatLayout
    {
        public static RectangleF CardSlot(float width, float height, int count, int index)
        {
            if (count <= 0 || index < 0 || index >= count)
                throw new ArgumentOutOfRangeException("index");
            float cardWidth = Math.Min(180, Math.Max(1, width - 500));
            float cardHeight = 244 * cardWidth / 180;
            float step = count == 1 ? 0 : Math.Min(192, Math.Max(1, (width - 500 - cardWidth) / (count - 1)));
            float offset = index - (count - 1) * .5f;
            float arch = count == 1 ? 0 : 10 * offset * offset / Math.Max(1, (count - 1) * (count - 1) * .25f);
            float totalWidth = cardWidth + (count - 1) * step;
            return new RectangleF((width - totalWidth) / 2 + index * step, height - 200 + arch, cardWidth, cardHeight);
        }

        public static float CardAngle(int count, int index) { return (index - (count - 1) * .5f) * Math.Min(3, 10f / Math.Max(1, count - 1)); }
        public static RectangleF DrawPile(float width, float height) { return new RectangleF(25, height - 109, 100, 90); }
        public static RectangleF DiscardPile(float width, float height) { return new RectangleF(width - 125, height - 109, 100, 90); }
        public static RectangleF Resource(float width, float height) { return new RectangleF(64, height - 210, 100, 100); }
        public static RectangleF EndTurn(float width, float height) { return new RectangleF(width - 240, height - 209, 215, 68); }
    }
}
