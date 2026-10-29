using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;

namespace ShpilSvoih
{
    // At most four display-sized crops (base/hover), never full card sheets. Dynamic
    // costs/damage remain live overlays; hero changes clear all crops, size changes stay bounded.
    sealed class CardArtCache : IDisposable
    {
        readonly Dictionary<Tuple<bool, Size>, Bitmap> cards = new Dictionary<Tuple<bool, Size>, Bitmap>();
        Image sheet;
        public void Draw(Graphics graphics, Image source, RectangleF crop, RectangleF area, float viewportScale, bool attack)
        {
            var pixels = new Size(Math.Max(1, (int)Math.Ceiling(area.Width * viewportScale)), Math.Max(1, (int)Math.Ceiling(area.Height * viewportScale)));
            if (sheet != source)
            {
                Clear(); sheet = source;
            }
            var key = Tuple.Create(attack, pixels);
            Bitmap cached;
            if (!cards.TryGetValue(key, out cached))
            {
                if (cards.Count >= 4) { Clear(); sheet = source; }
                var bitmap = new Bitmap(pixels.Width, pixels.Height, PixelFormat.Format32bppPArgb);
                try
                {
                    using (var canvas = Graphics.FromImage(bitmap))
                    {
                        canvas.CompositingMode = CompositingMode.SourceCopy;
                        canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        canvas.DrawImage(source, new RectangleF(0, 0, pixels.Width, pixels.Height), crop, GraphicsUnit.Pixel);
                    }
                    cards.Add(key, bitmap); cached = bitmap;
                }
                catch { bitmap.Dispose(); throw; }
            }
            var interpolation = graphics.InterpolationMode;
            try
            {
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                graphics.DrawImage(cached, area);
            }
            finally { graphics.InterpolationMode = interpolation; }
        }
        public void Clear()
        {
            foreach (var bitmap in cards.Values) bitmap.Dispose();
            cards.Clear();
            sheet = null;
        }
        public void Dispose() { Clear(); }
    }

    public sealed partial class Game
    {
        readonly CardArtCache cardArtCache = new CardArtCache();
    }
}
