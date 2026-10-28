using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ShpilSvoih
{
    public static class BabaiStageLayout
    {
        public static float WallBase(float floor) { return floor - 180; }
        // Registration is measured once from the original alpha, not the PNG canvas.
        public static RectangleF FootCrop(Bitmap image)
        {
            if (image == null) throw new ArgumentNullException("image");
            var data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[image.Width * 4];
                for (int y = image.Height - 1; y >= 0; y--)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                    for (int x = 0; x < image.Width; x++)
                        if (row[x * 4 + 3] > 32) return new RectangleF(0, 0, image.Width, y + 1);
                }
                throw new ArgumentException("Empty bed sprite.", "image");
            }
            finally { image.UnlockBits(data); }
        }
        public static RectangleF Bed(float left, float floor, float width, SizeF source)
        {
            float height = width * source.Height / source.Width;
            return new RectangleF(left, floor - height, width, height);
        }
    }
}
