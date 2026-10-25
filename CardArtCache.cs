using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ShpilSvoih
{
    // Two display-sized crops, never copies of the full card sheets. Dynamic
    // costs/damage remain live overlays; a resize or hero change releases both.
    sealed class CardArtCache : IDisposable
    {
        readonly Bitmap[] cards = new Bitmap[2];
        Image sheet;
        Size size;
        public void Draw(Graphics graphics, Image source, RectangleF crop, RectangleF area, float viewportScale, bool attack)
        {
            var pixels = new Size(Math.Max(1, (int)Math.Ceiling(area.Width * viewportScale)), Math.Max(1, (int)Math.Ceiling(area.Height * viewportScale)));
            if (sheet != source || size != pixels)
            {
                Clear(); sheet = source; size = pixels;
            }
            int index = attack ? 0 : 1;
            if (cards[index] == null)
            {
                var bitmap = new Bitmap(pixels.Width, pixels.Height, PixelFormat.Format32bppPArgb);
                try
                {
                    using (var canvas = Graphics.FromImage(bitmap))
                    {
                        canvas.CompositingMode = CompositingMode.SourceCopy;
                        canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        canvas.DrawImage(source, new RectangleF(0, 0, pixels.Width, pixels.Height), crop, GraphicsUnit.Pixel);
                    }
                    cards[index] = bitmap;
                }
                catch { bitmap.Dispose(); throw; }
            }
            graphics.DrawImage(cards[index], area);
        }
        public void Clear()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null) cards[i].Dispose();
                cards[i] = null;
            }
            sheet = null;
        }
        public void Dispose() { Clear(); }
    }

    public sealed partial class Game
    {
        readonly CardArtCache cardArtCache = new CardArtCache();
    }
}
