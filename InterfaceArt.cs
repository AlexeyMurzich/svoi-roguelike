using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        readonly ScaledImageCache interfaceArtCache = new ScaledImageCache(8 * 1024 * 1024, 64);

        void DrawInterfaceImage(Graphics graphics, Image image, RectangleF area, RectangleF crop)
        {
            interfaceArtCache.Draw(graphics, image, area, crop, 1, false);
        }
    }
}
