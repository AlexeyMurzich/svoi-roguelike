using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ShpilSvoih
{
    public static class Motion
    {
        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static float FrameDelta(float value)
        {
            return IsFinite(value) ? Math.Max(0, Math.Min(.25f, value)) : 0;
        }

        public static float Ease(float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return t * t * t * (t * (t * 6 - 15) + 10);
        }

        public static float Damp(float current, float target, ref float velocity, float seconds, float smoothTime)
        {
            if (!IsFinite(current) || !IsFinite(target) || !IsFinite(velocity) || !IsFinite(seconds) || !IsFinite(smoothTime) || smoothTime <= 0)
                throw new ArgumentOutOfRangeException("motion");
            if (seconds <= 0) return current;
            float omega = 2 / smoothTime, x = omega * seconds;
            float decay = 1 / (1 + x + .48f * x * x + .235f * x * x * x);
            float change = current - target;
            float step = (velocity + omega * change) * seconds;
            velocity = (velocity - omega * step) * decay;
            float next = target + (change + step) * decay;
            if ((target - current > 0) == (next > target))
            {
                next = target;
                velocity = 0;
            }
            if (Math.Abs(next - target) < .03f && Math.Abs(velocity) < .1f)
            {
                next = target;
                velocity = 0;
            }
            return next;
        }
    }

    // A view into one owned bitmap; common bounds keep the three poses registered.
    sealed class PoseAtlas
    {
        public readonly Bitmap Image;
        public readonly RectangleF Bounds;
        public readonly int CellWidth;
        readonly int columns;
        readonly ActorArtCache cache;
        public readonly int FrameCount;

        public PoseAtlas(Bitmap image, int columns = 3, int rows = 1, ActorArtCache cache = null)
        {
            if (image == null) throw new ArgumentNullException("image");
            if (columns <= 0 || rows <= 0 || image.Width % columns != 0 || image.Height % rows != 0 || image.Width / columns != image.Height / rows)
                throw new ArgumentException("Animation atlas must contain equal square cells.");
            this.columns = columns;
            this.cache = cache;
            FrameCount = columns * rows;
            Image = image;
            CellWidth = image.Width / columns;
            int left = CellWidth, top = CellWidth, right = -1, bottom = -1;
            var data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[image.Width * 4];
                for (int y = 0; y < image.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                    for (int x = 0; x < image.Width; x++)
                        if (row[x * 4 + 3] > 32)
                        {
                            int local = x % CellWidth;
                            left = Math.Min(left, local); right = Math.Max(right, local);
                            top = Math.Min(top, y % CellWidth); bottom = Math.Max(bottom, y % CellWidth);
                        }
                }
            }
            finally { image.UnlockBits(data); }
            if (right < left) throw new ArgumentException("Empty animation atlas.");
            Bounds = RectangleF.FromLTRB(Math.Max(0, left - 2), Math.Max(0, top - 2), Math.Min(CellWidth, right + 3), Math.Min(CellWidth, bottom + 3));
        }

        public RectangleF Target(RectangleF slot)
        {
            float scale = Math.Min(slot.Width / Bounds.Width, slot.Height / Bounds.Height);
            float width = Bounds.Width * scale, height = Bounds.Height * scale;
            return new RectangleF(slot.X + (slot.Width - width) / 2, slot.Bottom - height, width, height);
        }

        public void Draw(Graphics g, int frame, RectangleF slot, float opacity)
        {
            if (opacity <= 0) return;
            DrawPart(g, frame, slot, opacity, 1, PointF.Empty);
        }

        public void DrawPart(Graphics g, int frame, RectangleF slot, float opacity, float partScale, PointF offset)
        {
            if (frame < 0 || frame >= FrameCount) throw new ArgumentOutOfRangeException("frame");
            if (opacity <= 0) return;
            var target = Target(slot);
            float scale = target.Width / Bounds.Width;
            target = new RectangleF(
                target.Left + (Bounds.X * (partScale - 1) + offset.X) * scale,
                target.Top + (Bounds.Y * (partScale - 1) + offset.Y) * scale,
                target.Width * partScale, target.Height * partScale);
            var source = Bounds;
            source.X += frame % columns * CellWidth;
            source.Y += frame / columns * CellWidth;
            if (cache == null) SpriteDrawing.Image(g, Image, target, source, opacity, false);
            else cache.Draw(g, Image, target, source, opacity, false);
        }

        public void Blend(Graphics g, int from, int to, float amount, RectangleF slot)
        {
            amount = Motion.Ease(amount);
            if (from == to) { Draw(g, from, slot, 1); return; }
            Draw(g, from, slot, 1 - amount);
            Draw(g, to, slot, amount);
        }

        public void BlendParts(Graphics graphics, int from, int to, float amount, RectangleF slot, float partScale, PointF offset)
        {
            amount = Motion.Ease(amount);
            if (from == to)
            {
                DrawPart(graphics, from, slot, 1, partScale, offset);
                return;
            }
            DrawPart(graphics, from, slot, 1 - amount, partScale, offset);
            DrawPart(graphics, to, slot, amount, partScale, offset);
        }
    }
}
