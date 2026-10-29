using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ShpilSvoih
{
    // Six complete silhouettes in one ImageAssets-owned bitmap. Metadata only, no frame clones.
    sealed class DogAtlas
    {
        public readonly Bitmap Image;
        readonly RectangleF[] sources = new RectangleF[6];
        readonly float[] feetX = new float[6];
        readonly SizeF[] reference = new SizeF[2];
        readonly ScaledImageCache cache;
        public int FrameCount { get { return 6; } }
        public DogAtlas(Bitmap image, ScaledImageCache cache)
        {
            if (image == null) throw new ArgumentNullException("image");
            Image = image; this.cache = cache;
            var edges = new[] { 0, (int)Math.Round(image.Width * 606f / 1774), (int)Math.Round(image.Width * 1222f / 1774), image.Width };
            int split = (int)Math.Round(image.Height * 446f / 887);
            var data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[image.Width * 4];
                for (int dog = 0; dog < 2; dog++)
                    for (int pose = 0; pose < 3; pose++)
                    {
                        int left = image.Width, top = image.Height, right = -1, bottom = -1;
                        int y0 = dog == 0 ? 0 : split, y1 = dog == 0 ? split : image.Height;
                        for (int y = y0; y < y1; y++)
                        {
                            Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                            for (int x = edges[pose]; x < edges[pose + 1]; x++)
                                if (row[x * 4 + 3] > 64)
                                { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
                        }
                        if (right < left) throw new ArgumentException("Empty complete dog pose.");
                        int id = dog * 3 + pose;
                        sources[id] = RectangleF.FromLTRB(left, top, right + 1, bottom + 1);
                        reference[dog] = new SizeF(Math.Max(reference[dog].Width, right - left + 1), Math.Max(reference[dog].Height, bottom - top + 1));
                        int foot = left;
                        // Anchor to the rear grounded paw, never to a wagging tail or raised muzzle.
                        for (int y = Math.Max(top, bottom - (bottom - top) / 8); y <= bottom; y++)
                        {
                            Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                            for (int x = left; x <= right; x++) if (row[x * 4 + 3] > 128) foot = Math.Max(foot, x);
                        }
                        feetX[id] = foot;
                    }
            }
            finally { image.UnlockBits(data); }
        }
        public RectangleF Source(int dog, int pose)
        {
            if (dog < 0 || dog > 1 || pose < 0 || pose > 2) throw new ArgumentOutOfRangeException("pose");
            return sources[dog * 3 + pose];
        }
        public RectangleF Target(int dog, int pose, RectangleF slot)
        {
            var source = Source(dog, pose);
            float scale = Math.Min(slot.Width * .96f / reference[dog].Width, slot.Height * .96f / reference[dog].Height);
            float anchor = slot.Right - slot.Width * .06f;
            return new RectangleF(anchor - (feetX[dog * 3 + pose] - source.X) * scale, slot.Bottom - source.Height * scale, source.Width * scale, source.Height * scale);
        }
        public void Draw(Graphics graphics, int dog, int pose, RectangleF slot)
        {
            cache.Draw(graphics, Image, Target(dog, pose, slot), Source(dog, pose), 1, false);
        }
    }
}
