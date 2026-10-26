using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ShpilSvoih
{
    // Owned by Game, shared by every actor. Source images still belong to ImageAssets.
    sealed class ActorArtCache : IDisposable
    {
        readonly Dictionary<Tuple<Image, RectangleF, Size, bool>, Bitmap> images =
            new Dictionary<Tuple<Image, RectangleF, Size, bool>, Bitmap>();
        const long Limit = 24 * 1024 * 1024;
        long bytes;
        float scale = 1;
        public int Count { get { return images.Count; } }
        public long Bytes { get { return bytes; } }
        public void SetScale(float value)
        {
            if (scale == value) return;
            Clear(); scale = value;
        }
        public void Draw(Graphics g, Image source, RectangleF target, RectangleF crop, float opacity, bool warm)
        {
            if (opacity <= 0 || target.Width <= 0 || target.Height <= 0) return;
            var size = new Size(Math.Max(1, (int)Math.Ceiling(target.Width * scale)), Math.Max(1, (int)Math.Ceiling(target.Height * scale)));
            long required = (long)size.Width * size.Height * 4;
            if (required > Limit) { SpriteDrawing.Image(g, source, target, crop, opacity, warm); return; }
            var key = Tuple.Create(source, crop, size, warm);
            Bitmap bitmap;
            if (!images.TryGetValue(key, out bitmap))
            {
                if (bytes + required > Limit || images.Count >= 48) Clear();
                bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                try
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        SpriteDrawing.Image(graphics, source, new RectangleF(0, 0, size.Width, size.Height), crop, 1, warm);
                    }
                    images.Add(key, bitmap); bytes += required;
                }
                catch { bitmap.Dispose(); throw; }
            }
            var interpolation = g.InterpolationMode;
            try
            {
                g.InterpolationMode = InterpolationMode.Bilinear;
                SpriteDrawing.Image(g, bitmap, target, new RectangleF(0, 0, bitmap.Width, bitmap.Height), opacity, false);
            }
            finally { g.InterpolationMode = interpolation; }
        }
        public void Clear()
        {
            foreach (var image in images.Values) image.Dispose();
            images.Clear(); bytes = 0;
        }
        public void Dispose() { Clear(); }
    }
}
