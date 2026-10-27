using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ShpilSvoih
{
    // Views only; ImageAssets owns the bitmap. Registration is calculated once.
    sealed class HeroPoseAtlas
    {
        readonly Bitmap image;
        readonly ScaledImageCache cache;
        readonly RectangleF[] frames = new RectangleF[4];
        readonly float[] centers = new float[4];
        readonly float width, height;

        public HeroPoseAtlas(Bitmap image, int hero, ScaledImageCache cache)
        {
            this.image = image; this.cache = cache;
            float unit = image.Width / 1280f;
            // The casting hand extends into the transparent gutter. These explicit
            // cell boundaries retain it without sampling the adjacent defender.
            int splitY = (int)((hero == 2 ? 630 : 635) * image.Height / 1280f);
            int splitBottom = (int)((hero == 1 ? 640 : 680) * unit);
            var cells = new[] {
                new Rectangle(0, 0, image.Width / 2, splitY),
                new Rectangle(image.Width / 2, 0, image.Width / 2, splitY),
                new Rectangle(0, splitY, splitBottom, image.Height - splitY),
                new Rectangle(splitBottom, splitY, image.Width - splitBottom, image.Height - splitY)
            };
            var data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[image.Width * 4];
                for (int i = 0; i < 4; i++)
                {
                    var cell = cells[i];
                    int left = cell.Right, right = cell.Left, top = cell.Bottom, bottom = cell.Top;
                    for (int y = cell.Top; y < cell.Bottom; y++)
                    {
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                        for (int x = cell.Left; x < cell.Right; x++)
                            if (row[x * 4 + 3] > 32)
                            { left = Math.Min(left, x); right = Math.Max(right, x); top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
                    }
                    if (right <= left || bottom <= top) throw new ArgumentException("Empty hero pose.");
                    frames[i] = RectangleF.FromLTRB(left, top, right + 1, bottom + 1);
                    centers[i] = (i % 2 == 0 ? 320 : 960) * unit;
                    width = Math.Max(width, frames[i].Width);
                    height = Math.Max(height, frames[i].Height);
                }
            }
            finally { image.UnlockBits(data); }
        }

        public void Draw(Graphics g, int frame, RectangleF area)
        {
            var source = frames[frame];
            float scale = Math.Min(area.Width / width, area.Height / height);
            var target = new RectangleF(area.X + area.Width / 2 + (source.X - centers[frame]) * scale,
                area.Bottom - source.Height * scale, source.Width * scale, source.Height * scale);
            cache.Draw(g, image, target, source, 1, false);
        }
    }

    public sealed partial class Game
    {
        readonly HeroPoseAtlas[] heroCombatPoses = new HeroPoseAtlas[3];

        void DrawHeroCombatPose(Graphics graphics, RectangleF area)
        {
            if (heroCombatPoses[selected] == null)
            {
                string path = "heroes/" + filenames[selected];
                heroCombatPoses[selected] = new HeroPoseAtlas(assetStore.Get(path, "heroes." + filenames[selected]), selected, actorArtCache);
            }
            float time = attackTimeline.Time;
            int frame = combat.Block > 0 || guardVisualTime > .15f ? 3 : 0;
            bool strike = attackTimeline.IsRunning && attackTimeline.Kind == AttackAnimation.HeroStrike;
            bool guard = attackTimeline.IsRunning && attackTimeline.Kind == AttackAnimation.HeroGuard;
            if (strike)
            {
                if (time < .13f) frame = guardAtActionStart > .5f ? 3 : 0;
                else if (time < .37f) frame = 1;
                else if (time < .78f) frame = 2;
            }
            else if (guard) frame = time >= .12f || guardAtActionStart > .5f ? 3 : 0;
            // Move the complete silhouette; never transform the face separately.
            if (strike)
            {
                float pulse = time < .5f ? Motion.Ease(Math.Max(0, time - .26f) / .24f) : 1 - Motion.Ease((time - .5f) / .45f);
                area.Offset(pulse * (selected == 1 ? 28 : 10), selected == 0 ? -pulse * 12 : 0);
            }
            heroCombatPoses[selected].Draw(graphics, frame, area);
        }

        void DrawHeroProjectile(Graphics g, float x, float y, float flight)
        {
            if (selected == 0)
            {
                for (int i = 0; i < 3; i++)
                {
                    float xx = x - i * 15, yy = y + (float)Math.Sin(flight * 16 + i) * 10;
                    float wing = 3 + Math.Abs((float)Math.Sin(flight * 28 + i)) * 6;
                    using (var brush = new SolidBrush(Color.FromArgb(210 - i * 35, 115, 245, 175)))
                    {
                        g.FillEllipse(brush, xx - wing, yy - 7, wing, 12);
                        g.FillEllipse(brush, xx, yy - 7, wing, 12);
                    }
                }
            }
            else if (selected == 1)
            {
                using (var pen = new Pen(Color.FromArgb(210, 234, 192, 114), 4))
                    g.DrawArc(pen, x - 28, y - 32, 56, 64, -70, 135);
            }
            else
            {
                using (var pen = new Pen(Color.FromArgb(220, 169, 135, 224), 5))
                    g.DrawBezier(pen, new PointF(x - 27, y + 8), new PointF(x - 12, y - 16), new PointF(x + 5, y + 16), new PointF(x + 16, y - 5));
            }
        }
    }
}
