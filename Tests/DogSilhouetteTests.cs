using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ShpilSvoih
{
    public static partial class UiTests
    {
        // Largest opaque component catches the old visually detached head, not just a tiny neck overlap.
        static Tuple<int, float, int> DogSilhouette(Bitmap image)
        {
            int width = image.Width, height = image.Height, total = 0, lowest = -1, largest = 0;
            var mask = new byte[width * height]; var row = new byte[width * 4];
            var data = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                    for (int x = 0; x < width; x++) if (row[x * 4 + 3] > 128) { mask[y * width + x] = 1; total++; lowest = y; }
                }
            }
            finally { image.UnlockBits(data); }
            var queue = new int[mask.Length];
            for (int at = 0; at < mask.Length; at++)
            {
                if (mask[at] != 1) continue;
                int read = 0, count = 1; queue[0] = at; mask[at] = 2;
                while (read < count)
                {
                    int pixel = queue[read++], x = pixel % width, y = pixel / width;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                        int next = ny * width + nx;
                        if (mask[next] == 1) { mask[next] = 2; queue[count++] = next; }
                    }
                }
                largest = Math.Max(largest, count);
            }
            return Tuple.Create(total, total == 0 ? 0 : largest / (float)total, lowest);
        }
    }
}
