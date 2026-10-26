using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ShpilSvoih
{
    // Generated contact sheets have overlapping rectangular gutters. Separate connected
    // silhouettes once at load, then register each pose to a common baseline.
    sealed class HedgehogAtlas : IDisposable
    {
        const int Width = 768, Height = 560;
        readonly Bitmap[] frames = new Bitmap[7];
        public int LargeComponents { get; private set; }
        public int FrameCount { get { return 6; } }
        readonly ActorArtCache cache;
        public HedgehogAtlas(Bitmap source, bool rabid, ActorArtCache cache = null)
        {
            this.cache = cache;
            int w = source.Width, h = source.Height;
            var pixels = new byte[w * h * 4];
            var locked = source.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try { for (int y = 0; y < h; y++) Marshal.Copy(IntPtr.Add(locked.Scan0, y * locked.Stride), pixels, y * w * 4, w * 4); }
            finally { source.UnlockBits(locked); }
            int[] labels = new int[w * h], queue = new int[w * h];
            var owners = new List<int> { -1 };
            float sx = w / 1536f, sy = h / 1024f;
            float[] anchorsX = rabid ? new float[] { 270, 800, 1290, 310, 830, 1290 } : new float[] { 270, 800, 1280, 250, 850, 1290 };
            float[] anchorsY = rabid ? new float[] { 480, 480, 480, 950, 950, 950 } : new float[] { 500, 500, 500, 960, 960, 960 };
            for (int start = 0; start < labels.Length; start++)
            {
                if (labels[start] != 0 || pixels[start * 4 + 3] <= 16) continue;
                int id = owners.Count, head = 0, tail = 1; queue[0] = start; labels[start] = id;
                long sumX = 0, sumY = 0;
                int left = w, right = 0, top = h;
                while (head < tail)
                {
                    int at = queue[head++], x = at % w, y = at / w;
                    sumX += x; sumY += y; left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int next = ny * w + nx;
                            if (labels[next] == 0 && pixels[next * 4 + 3] > 16) { labels[next] = id; queue[tail++] = next; }
                        }
                }
                if (tail > 4000 * sx * sy) LargeComponents++;
                float cx = sumX / (float)tail / sx, cy = sumY / (float)tail / sy;
                int row = cy < 512 ? 0 : 1, owner = row * 3;
                float best = float.MaxValue;
                for (int i = row * 3; i < row * 3 + 3; i++)
                {
                    float distance = Math.Abs(cx - anchorsX[i]);
                    if (distance < best) { best = distance; owner = i; }
                }
                // Detached needles belong to the firing ball, not the adjacent curled pose.
                if (!rabid && top / sy > 512 && left / sx >= 475 && right / sx < 700) owner = 6;
                owners.Add(owner);
            }
            var output = new byte[7][];
            for (int i = 0; i < 7; i++) output[i] = new byte[Width * Height * 4];
            for (int at = 0; at < labels.Length; at++)
            {
                int id = labels[at]; if (id == 0) continue;
                int owner = owners[id], pose = owner == 6 ? 4 : owner;
                int x = (int)Math.Round(at % w / sx - anchorsX[pose] + Width / 2f);
                int y = (int)Math.Round(at / w / sy - anchorsY[pose] + 520);
                if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                Buffer.BlockCopy(pixels, at * 4, output[owner], (y * Width + x) * 4, 4);
            }
            try
            {
                for (int i = 0; i < 7; i++)
                {
                    frames[i] = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
                    var target = frames[i].LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                    try { for (int y = 0; y < Height; y++) Marshal.Copy(output[i], y * Width * 4, IntPtr.Add(target.Scan0, y * target.Stride), Width * 4); }
                    finally { frames[i].UnlockBits(target); }
                }
            }
            catch { Dispose(); throw; }
        }
        public void Draw(Graphics graphics, int frame, RectangleF area, float opacity)
        {
            float scale = Math.Min(area.Width / Width, area.Height / Height);
            var target = new RectangleF(area.X + area.Width / 2 - Width * scale / 2, area.Bottom - 520 * scale, Width * scale, Height * scale);
            if (cache == null) SpriteDrawing.Image(graphics, frames[frame], target, new RectangleF(0, 0, Width, Height), opacity, false);
            else cache.Draw(graphics, frames[frame], target, new RectangleF(0, 0, Width, Height), opacity, false);
        }
        public void Blend(Graphics graphics, int from, int to, float amount, RectangleF area)
        {
            amount = Motion.Ease(amount);
            Draw(graphics, from, area, 1 - amount); Draw(graphics, to, area, amount);
        }
        public void Dispose() { foreach (var frame in frames) if (frame != null) frame.Dispose(); }
    }

    public sealed partial class Game
    {
        static readonly float[] HedgehogBiteTimes = { 0, .17f, .33f, .49f, .62f, .79f, 1.05f };
        static readonly float[] HedgehogCurlTimes = { 0, .19f, .38f, .59f, .87f, 1.16f, 1.45f };
        void DrawHedgehogPose(Graphics graphics, RectangleF area, bool rabid, bool active)
        {
            var atlas = rabid ? rabidHedgehogPoses : hedgehogPoses;
            var kind = rabid ? AttackAnimation.HedgehogBite : AttackAnimation.HedgehogNeedles;
            if (!active || attackTimeline.Kind != kind) { atlas.Draw(graphics, 0, area, 1); return; }
            float[] times = rabid ? HedgehogBiteTimes : HedgehogCurlTimes;
            float time = attackTimeline.Time;
            int step = 0; while (step < 5 && time >= times[step + 1]) step++;
            // Discrete registered poses preserve opaque paws and avoid doubled faces.
            // Continuous attack displacement is applied by PaintCombatEnemy.
            atlas.Draw(graphics, step, area, 1);
            if (!rabid && time >= .87f && time <= 1.12f)
            {
                float flight = Motion.Ease((time - .87f) / .25f);
                var needles = area;
                needles.Offset((HeroCenter - EnemyCenter(actingEnemyIndex) + 160) * flight, 0);
                atlas.Draw(graphics, 6, needles, 1);
            }
        }
    }
}
