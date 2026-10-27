using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ShpilSvoih
{
    // Owns only display-sized copies. Original bitmaps belong to ImageAssets.
    // Separate instances prevent HUD icons from evicting combat animation poses.
    sealed class ScaledImageCache : IDisposable
    {
        struct Key : IEquatable<Key>
        {
            public Image Source;
            public RectangleF Crop;
            public Size Size;
            public bool Warm;

            public bool Equals(Key other)
            {
                return ReferenceEquals(Source, other.Source) && Crop == other.Crop && Size == other.Size && Warm == other.Warm;
            }
            public override bool Equals(object other) { return other is Key && Equals((Key)other); }
            public override int GetHashCode()
            {
                return Source.GetHashCode() ^ Crop.GetHashCode() ^ Size.GetHashCode() ^ Warm.GetHashCode();
            }
        }

        sealed class Entry
        {
            public Bitmap Image;
            public long Stamp, Bytes;
        }

        readonly Dictionary<Key, Entry> images = new Dictionary<Key, Entry>();
        readonly long byteLimit;
        readonly int entryLimit, pixelStep;
        long bytes, stamp;
        float scale = 1;

        public ScaledImageCache(long byteLimit = 24 * 1024 * 1024, int entryLimit = 48, int pixelStep = 1)
        {
            if (byteLimit <= 0 || entryLimit <= 0 || pixelStep < 1 || pixelStep > 16)
                throw new ArgumentOutOfRangeException("byteLimit");
            this.byteLimit = byteLimit;
            this.entryLimit = entryLimit;
            this.pixelStep = pixelStep;
        }

        public int Count { get { return images.Count; } }
        public long Bytes { get { return bytes; } }
        public long Hits { get; private set; }
        public long Misses { get; private set; }
        public long Evictions { get; private set; }

        public void SetScale(float value)
        {
            if (!Motion.IsFinite(value) || value <= 0 || scale == value) return;
            Clear();
            scale = value;
        }

        int PixelSize(float logicalSize)
        {
            // Small changes in an animated silhouette reuse the same resample.
            // Destination geometry remains exact: only bitmap resolution is bucketed.
            int pixels = Math.Max(1, (int)Math.Ceiling(logicalSize * scale));
            return (pixels + pixelStep - 1) / pixelStep * pixelStep;
        }

        public void Draw(Graphics graphics, Image source, RectangleF target, RectangleF crop, float opacity, bool warm)
        {
            if (!Motion.IsFinite(opacity) || opacity <= 0 || !Motion.IsFinite(target.Width) ||
                !Motion.IsFinite(target.Height) || target.Width <= 0 || target.Height <= 0) return;
            if (target.Width * scale > 8192 || target.Height * scale > 8192)
            {
                SpriteDrawing.Image(graphics, source, target, crop, opacity, warm);
                return;
            }
            var size = new Size(PixelSize(target.Width), PixelSize(target.Height));
            long required = (long)size.Width * size.Height * 4;
            if (required > byteLimit)
            {
                SpriteDrawing.Image(graphics, source, target, crop, opacity, warm);
                return;
            }

            var key = new Key { Source = source, Crop = crop, Size = size, Warm = warm };
            Entry entry;
            if (images.TryGetValue(key, out entry)) Hits++;
            else
            {
                Misses++;
                MakeRoom(required);
                entry = CreateEntry(source, crop, size, warm, required);
                images.Add(key, entry);
                bytes += required;
            }
            entry.Stamp = ++stamp;
            DrawEntry(graphics, entry.Image, target, opacity);
        }

        static Entry CreateEntry(Image source, RectangleF crop, Size size, bool warm, long required)
        {
            var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            try
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    SpriteDrawing.Image(graphics, source, new RectangleF(PointF.Empty, size), crop, 1, warm);
                }
                return new Entry { Image = bitmap, Bytes = required };
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }

        static void DrawEntry(Graphics graphics, Bitmap bitmap, RectangleF target, float opacity)
        {
            var interpolation = graphics.InterpolationMode;
            try
            {
                graphics.InterpolationMode = InterpolationMode.Bilinear;
                SpriteDrawing.Image(graphics, bitmap, target, new RectangleF(0, 0, bitmap.Width, bitmap.Height), opacity, false);
            }
            finally { graphics.InterpolationMode = interpolation; }
        }

        void MakeRoom(long required)
        {
            while (bytes + required > byteLimit || images.Count >= entryLimit)
            {
                Key oldest = default(Key);
                long minimum = long.MaxValue;
                foreach (var pair in images)
                    if (pair.Value.Stamp < minimum)
                    {
                        oldest = pair.Key;
                        minimum = pair.Value.Stamp;
                    }
                var entry = images[oldest];
                bytes -= entry.Bytes;
                images.Remove(oldest);
                entry.Image.Dispose();
                Evictions++;
            }
        }

        public void Clear()
        {
            foreach (var entry in images.Values) entry.Image.Dispose();
            images.Clear();
            bytes = stamp = 0;
        }

        public void Dispose() { Clear(); }
    }
}
