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
        struct Key : IEquatable<Key>
        {
            public Image Source;
            public RectangleF Crop;
            public Size Size;
            public bool Warm;
            public bool Equals(Key other) { return ReferenceEquals(Source, other.Source) && Crop == other.Crop && Size == other.Size && Warm == other.Warm; }
            public override bool Equals(object other) { return other is Key && Equals((Key)other); }
            public override int GetHashCode() { return Source.GetHashCode() ^ Crop.GetHashCode() ^ Size.GetHashCode() ^ Warm.GetHashCode(); }
        }
        sealed class Entry
        {
            public Bitmap Image;
            public long Stamp, Bytes;
        }
        readonly Dictionary<Key, Entry> images = new Dictionary<Key, Entry>();
        const long Limit = 24 * 1024 * 1024;
        long bytes;
        long stamp;
        float scale = 1;
        public int Count { get { return images.Count; } }
        public long Bytes { get { return bytes; } }
        public void SetScale(float value)
        {
            if (!Motion.IsFinite(value) || value <= 0) return;
            if (scale == value) return;
            Clear(); scale = value;
        }
        public void Draw(Graphics g, Image source, RectangleF target, RectangleF crop, float opacity, bool warm)
        {
            if (!Motion.IsFinite(opacity) || opacity <= 0 || !Motion.IsFinite(target.Width) || !Motion.IsFinite(target.Height) || target.Width <= 0 || target.Height <= 0) return;
            if (target.Width * scale > 8192 || target.Height * scale > 8192)
            { SpriteDrawing.Image(g, source, target, crop, opacity, warm); return; }
            var size = new Size(Math.Max(1, (int)Math.Ceiling(target.Width * scale)), Math.Max(1, (int)Math.Ceiling(target.Height * scale)));
            long required = (long)size.Width * size.Height * 4;
            if (required > Limit) { SpriteDrawing.Image(g, source, target, crop, opacity, warm); return; }
            var key = new Key { Source = source, Crop = crop, Size = size, Warm = warm };
            Entry entry;
            if (!images.TryGetValue(key, out entry))
            {
                while (bytes + required > Limit || images.Count >= 48) EvictOldest();
                var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                try
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        SpriteDrawing.Image(graphics, source, new RectangleF(0, 0, size.Width, size.Height), crop, 1, warm);
                    }
                    entry = new Entry { Image = bitmap, Bytes = required };
                    images.Add(key, entry); bytes += required;
                }
                catch { bitmap.Dispose(); throw; }
            }
            entry.Stamp = ++stamp;
            var interpolation = g.InterpolationMode;
            try
            {
                g.InterpolationMode = InterpolationMode.Bilinear;
                SpriteDrawing.Image(g, entry.Image, target, new RectangleF(0, 0, entry.Image.Width, entry.Image.Height), opacity, false);
            }
            finally { g.InterpolationMode = interpolation; }
        }
        void EvictOldest()
        {
            Key oldest = default(Key);
            long minimum = long.MaxValue;
            foreach (var pair in images)
                if (pair.Value.Stamp < minimum) { oldest = pair.Key; minimum = pair.Value.Stamp; }
            var entry = images[oldest];
            bytes -= entry.Bytes;
            entry.Image.Dispose(); images.Remove(oldest);
        }
        public void Clear()
        {
            foreach (var entry in images.Values) entry.Image.Dispose();
            images.Clear(); bytes = stamp = 0;
        }
        public void Dispose() { Clear(); }
    }
}
