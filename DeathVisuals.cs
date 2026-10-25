using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ShpilSvoih
{
    // One temporary snapshot per dying creature; released when its animation ends.
    public sealed class DeathVisuals : IDisposable
    {
        public const float Duration = .95f;
        sealed class Entry
        {
            public float Time;
            public Bitmap Sprite;
        }
        readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        public bool IsRunning
        {
            get
            {
                foreach (var entry in entries.Values) if (entry.Time < Duration) return true;
                return false;
            }
        }
        public int SnapshotCount
        {
            get
            {
                int count = 0;
                foreach (var entry in entries.Values) if (entry.Sprite != null) count++;
                return count;
            }
        }
        public void Start(int id)
        {
            if (!entries.ContainsKey(id)) entries.Add(id, new Entry());
        }
        public void Advance(float seconds)
        {
            if (!Motion.IsFinite(seconds) || seconds <= 0) return;
            foreach (var entry in entries.Values)
            {
                entry.Time = Math.Min(Duration, entry.Time + seconds);
                if (entry.Time >= Duration && entry.Sprite != null)
                {
                    entry.Sprite.Dispose();
                    entry.Sprite = null;
                }
            }
        }
        public bool Draw(Graphics graphics, int id, RectangleF area, Action<Graphics> paint)
        {
            Entry entry;
            if (!entries.TryGetValue(id, out entry)) return false;
            if (entry.Time >= Duration) return true;
            const int padding = 40;
            if (entry.Sprite == null)
            {
                entry.Sprite = new Bitmap((int)Math.Ceiling(area.Width) + padding * 2, (int)Math.Ceiling(area.Height) + padding * 2, PixelFormat.Format32bppPArgb);
                using (var canvas = Graphics.FromImage(entry.Sprite))
                {
                    canvas.SmoothingMode = SmoothingMode.AntiAlias;
                    canvas.InterpolationMode = InterpolationMode.Bilinear;
                    canvas.TranslateTransform(padding - area.X, padding - area.Y);
                    paint(canvas);
                }
            }
            float progress = Motion.Ease(entry.Time / Duration);
            var saved = graphics.Save();
            try
            {
                float pivotX = area.X + area.Width / 2, pivotY = area.Bottom;
                graphics.TranslateTransform(pivotX + (id < 0 ? -18 : 18) * progress, pivotY + 24 * progress);
                graphics.RotateTransform((id < 0 ? -25 : 25) * progress);
                graphics.ScaleTransform(1 - .12f * progress, 1 - .65f * progress);
                graphics.TranslateTransform(-pivotX, -pivotY);
                using (var attributes = new ImageAttributes())
                {
                    var color = new ColorMatrix(); color.Matrix33 = 1 - progress;
                    attributes.SetColorMatrix(color);
                    graphics.DrawImage(entry.Sprite, Rectangle.Round(new RectangleF(area.X - padding, area.Y - padding, entry.Sprite.Width, entry.Sprite.Height)), 0, 0, entry.Sprite.Width, entry.Sprite.Height, GraphicsUnit.Pixel, attributes);
                }
            }
            finally { graphics.Restore(saved); }
            return true;
        }
        public void Clear()
        {
            foreach (var entry in entries.Values) if (entry.Sprite != null) entry.Sprite.Dispose();
            entries.Clear();
        }
        public void Dispose() { Clear(); }
    }
}
