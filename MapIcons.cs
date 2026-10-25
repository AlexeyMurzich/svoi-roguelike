using System;
using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        static string RoomName(RoomKind kind)
        {
            switch (kind)
            {
                case RoomKind.Start:
                    return "Евроопт";
                case RoomKind.Fight:
                    return "Бой";
                case RoomKind.Question:
                    return "Вопрос";
                case RoomKind.Elite:
                    return "Элитный враг";
                case RoomKind.Rest:
                    return "Костёр";
                case RoomKind.Shop:
                    return "Магазин";
                default:
                    return "Дом босса";
            }
        }

        void DrawRoomIcon(Graphics g, RoomKind kind, PointF center, float size)
        {
            if (kind == RoomKind.Start)
            {
                using (var glow = new SolidBrush(Color.FromArgb(65, 112, 255, 156)))
                    g.FillEllipse(glow, center.X - 22, center.Y - 22, 44, 44);
                using (var fill = new SolidBrush(Color.FromArgb(12, 51, 31)))
                    g.FillEllipse(fill, center.X - 15, center.Y - 15, 30, 30);
                using (var rim = new Pen(Color.FromArgb(155, 243, 168), 3))
                    g.DrawEllipse(rim, center.X - 15, center.Y - 15, 30, 30);
                return;
            }

            int index;
            switch (kind)
            {
                case RoomKind.Fight:
                    index = 0;
                    break;
                case RoomKind.Question:
                    index = 1;
                    break;
                case RoomKind.Rest:
                    index = 2;
                    break;
                case RoomKind.Shop:
                    index = 3;
                    break;
                case RoomKind.Elite:
                    index = 4;
                    break;
                case RoomKind.Boss:
                    index = 5;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("kind");
            }

            // Sample the atlas at paint time; no six extra bitmaps or image copies.
            float cellWidth = mapIcons.Width / 3f, cellHeight = mapIcons.Height / 2f;
            var source = new RectangleF(index % 3 * cellWidth, index / 3 * cellHeight, cellWidth, cellHeight);
            var target = new RectangleF(center.X - size / 2, center.Y - size / 2, size, size);
            g.DrawImage(mapIcons, target, source, GraphicsUnit.Pixel);
        }

        Color RoomColor(RoomKind kind)
        {
            if (kind == RoomKind.Elite || kind == RoomKind.Boss)
                return Color.FromArgb(228, 117, 108);
            if (kind == RoomKind.Rest)
                return Color.FromArgb(237, 166, 99);
            if (kind == RoomKind.Shop)
                return Color.FromArgb(138, 195, 151);
            if (kind == RoomKind.Question)
                return Color.FromArgb(177, 161, 220);
            return text;
        }
    }
}
