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
            const float gap = 12;
            float cardWidth = Math.Min(180, Math.Max(1, (width - 500 - (count - 1) * gap) / count));
            float cardHeight = 244 * cardWidth / 180;
            float totalWidth = count * cardWidth + (count - 1) * gap;
            return new RectangleF((width - totalWidth) / 2 + index * (cardWidth + gap), height - 16 - cardHeight, cardWidth, cardHeight);
        }
    }
}
