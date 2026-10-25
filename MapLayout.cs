using System;
using System.Drawing;

namespace ShpilSvoih
{
    public static class MapLayout
    {
        public const float HudHeight = 64;
        public static readonly RectangleF View = new RectangleF(0, HudHeight, 1280, 840 - HudHeight);
        public const float WorldHeight = 1980;
        public const float FloorSpacing = 110;
        public static float MaxScroll
        {
            get
            {
                return WorldHeight - View.Height;
            }
        }

        public static float Clamp(float scroll)
        {
            return Math.Max(0, Math.Min(MaxScroll, scroll));
        }

        public static PointF WorldPosition(MapNode node)
        {
            if (node == null)
                throw new ArgumentNullException("node");
            float jitter = node.Floor <= 1 || node.Kind == RoomKind.Boss ? 0 : node.Id * 17 % 15 - 7;
            return new PointF(100 + node.Lane * 180 + node.Offset, 1900 - node.Floor * FloorSpacing + jitter);
        }

        public static PointF ScreenPosition(MapNode node, float scroll)
        {
            var world = WorldPosition(node);
            return new PointF(world.X, world.Y - Clamp(scroll) + View.Top);
        }

        public static float Focus(MapNode node)
        {
            return Clamp(WorldPosition(node).Y - View.Height + 100);
        }
    }
}
