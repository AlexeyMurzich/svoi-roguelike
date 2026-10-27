using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        bool mimicratorPreview, mimicratorPaused;
        double mimicratorSeconds;
        Bitmap mimicratorBody, mimicratorProps, mimicratorBackdrop;
        Bitmap mimicratorDisplayBody;
        readonly Bitmap[] mimicratorDisplayProps = new Bitmap[3];
        float mimicratorDisplayScale;
        readonly RectangleF[] mimicratorSources = new RectangleF[3];

        public void StartMimicratorPreview()
        {
            LoadMimicratorArt();
            ReturnToMainMenu();
            mimicratorPreview = true;
            mimicratorPaused = false;
            mimicratorSeconds = 0;
            WakeMotion();
            RefreshScene();
        }

        void DrawPreparedMimicratorBody(Graphics g)
        {
            var interpolation = g.InterpolationMode;
            try
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                SpriteDrawing.FitBottom(g, mimicratorDisplayBody, new RectangleF(-223, -330, 446, 650), 1);
            }
            finally { g.InterpolationMode = interpolation; }
        }

        void LoadMimicratorArt()
        {
            if (mimicratorBody == null)
            {
                mimicratorBody = assetStore.Get("enemies/mimicrator-body.png", "enemies.mimicrator-body.png");
                mimicratorProps = assetStore.Get("enemies/mimicrator-props.png", "enemies.mimicrator-props.png");
                FindMimicratorProps();
            }
        }

        void FindMimicratorProps()
        {
            var data = mimicratorProps.LockBits(new Rectangle(0, 0, mimicratorProps.Width, mimicratorProps.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                var row = new byte[mimicratorProps.Width * 4];
                for (int item = 0; item < 3; item++)
                {
                    int start = item * mimicratorProps.Width / 3, end = (item + 1) * mimicratorProps.Width / 3;
                    int left = end, top = mimicratorProps.Height, right = start, bottom = -1;
                    for (int y = 0; y < mimicratorProps.Height; y++)
                    {
                        Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                        for (int x = start; x < end; x++)
                            if (row[x * 4 + 3] > 16)
                            {
                                left = Math.Min(left, x); right = Math.Max(right, x);
                                top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                            }
                    }
                    if (bottom < top) throw new InvalidOperationException("Missing Mimicrator orbit sprite.");
                    mimicratorSources[item] = RectangleF.FromLTRB(left, top, right + 1, bottom + 1);
                }
            }
            finally { mimicratorProps.UnlockBits(data); }
        }

        void CloseMimicratorPreview()
        {
            ClearMimicratorBackdrop();
            mimicratorPreview = false;
            mimicratorPaused = false;
            if (combatTimer != null) combatTimer.Stop();
            ResetMotionClock();
            WakeMotion();
            RefreshScene();
        }

        void ToggleMimicratorPause()
        {
            mimicratorPaused = !mimicratorPaused;
            ResetMotionClock();
            if (mimicratorPaused) combatTimer.Stop();
            else WakeMotion();
            RefreshScene();
        }

        void AdvanceMimicrator(float elapsed)
        {
            if (!mimicratorPreview || mimicratorPaused || GameplayPaused) return;
            mimicratorSeconds += Motion.FrameDelta(elapsed);
            Invalidate();
        }

        bool HandleMimicratorKey(KeyEventArgs args)
        {
            if (!mimicratorPreview) return false;
            if (args.KeyCode == Keys.Escape) CloseMimicratorPreview();
            else if (args.KeyCode == Keys.Space) ToggleMimicratorPause();
            else return true;
            args.Handled = args.SuppressKeyPress = true;
            return true;
        }

        static void MimicratorGlow(Graphics g, PointF center, float width, float height, Color color)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(center.X - width / 2, center.Y - height / 2, width, height);
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = color;
                    brush.SurroundColors = new[] { Color.FromArgb(0, color) };
                    g.FillPath(brush, path);
                }
            }
        }


        void ClearMimicratorBackdrop()
        {
            if (mimicratorBackdrop != null) mimicratorBackdrop.Dispose();
            mimicratorBackdrop = null;
            ClearMimicratorDisplaySprites();
        }


        void ClearMimicratorDisplaySprites()
        {
            if (mimicratorDisplayBody != null) mimicratorDisplayBody.Dispose();
            mimicratorDisplayBody = null;
            for (int i = 0; i < 3; i++)
            {
                if (mimicratorDisplayProps[i] != null) mimicratorDisplayProps[i].Dispose();
                mimicratorDisplayProps[i] = null;
            }
        }

        static Bitmap PrepareMimicratorSprite(Bitmap source, RectangleF crop, float edge)
        {
            float fit = edge / Math.Max(crop.Width, crop.Height);
            var bitmap = new Bitmap(Math.Max(1, (int)Math.Ceiling(crop.Width * fit)),
                Math.Max(1, (int)Math.Ceiling(crop.Height * fit)), PixelFormat.Format32bppPArgb);
            try
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(source, new RectangleF(0, 0, bitmap.Width, bitmap.Height), crop, GraphicsUnit.Pixel);
                }
                return bitmap;
            }
            catch { bitmap.Dispose(); throw; }
        }

        void PrepareMimicratorDisplaySprites()
        {
            float scale = RenderViewport.Scale;
            if (mimicratorDisplayBody != null && mimicratorDisplayScale == scale) return;
            ClearMimicratorDisplaySprites();
            mimicratorDisplayScale = scale;
            mimicratorDisplayBody = PrepareMimicratorSprite(mimicratorBody,
                new RectangleF(0, 0, mimicratorBody.Width, mimicratorBody.Height), 650 * scale);
            for (int i = 0; i < 3; i++)
                mimicratorDisplayProps[i] = PrepareMimicratorSprite(mimicratorProps,
                    mimicratorSources[i], (i == 0 ? 108 : i == 1 ? 78 : 92) * scale);
        }

        void DrawMimicratorBackdrop(Graphics g)
        {
            var viewport = RenderViewport;
            var size = new Size(Math.Max(1, (int)Math.Ceiling(1280 * viewport.Scale)),
                Math.Max(1, (int)Math.Ceiling(840 * viewport.Scale)));
            if (mimicratorBackdrop == null || mimicratorBackdrop.Size != size)
            {
                ClearMimicratorBackdrop();
                mimicratorBackdrop = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
                using (var bg = Graphics.FromImage(mimicratorBackdrop))
                {
                    bg.ScaleTransform(size.Width / 1280f, size.Height / 840f);
                    bg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    bg.DrawImage(mapBackground, new RectangleF(0, 0, 1280, 840),
                        Viewport.CoverSource(mapBackground.Size, new SizeF(1280, 840)), GraphicsUnit.Pixel);
                    Rect(bg, 0, 0, 1280, 840, Color.FromArgb(220, 12, 12, 27));
                    MimicratorGlow(bg, new PointF(640, 420), 850, 750, Color.FromArgb(90, 93, 58, 116));
                    MimicratorGlow(bg, new PointF(640, 746), 400, 50, Color.FromArgb(140, 9, 8, 18));
                }
            }
            var saved = g.Save();
            try
            {
                g.ResetTransform();
                g.DrawImageUnscaled(mimicratorBackdrop, (int)viewport.OffsetX, (int)viewport.OffsetY);
            }
            finally { g.Restore(saved); }
        }

        void DrawMimicratorPreview(Graphics g)
        {
            g.InterpolationMode = InterpolationMode.Bilinear;
            DrawMimicratorBackdrop(g);
            PrepareMimicratorDisplaySprites();

            DrawMimicratorOrbitLayer(g, false, mimicratorSeconds);
            var saved = g.Save();
            try
            {
                float bob = (float)Math.Sin(mimicratorSeconds * 1.25) * 6;
                g.TranslateTransform(640, 444 + bob);
                g.RotateTransform((float)Math.Sin(mimicratorSeconds * .65) * .65f);
                DrawPreparedMimicratorBody(g);
            }
            finally { g.Restore(saved); }
            DrawMimicratorOrbitLayer(g, true, mimicratorSeconds);

            TextAt(g, "МИМИКРАТОР", 38, 29, 29, text, 600, true);
            TextAt(g, "ЭЛИТНЫЙ ПРОТИВНИК", 40, 73, 12, Color.FromArgb(185, 159, 200), 500, true);
            Button(g, "← В главное меню", 1010, 30, 230, 46, CloseMimicratorPreview);
            Button(g, mimicratorPaused ? "Продолжить" : "Пауза", 40, 756, 190, 46, ToggleMimicratorPause);
            TextAt(g, "Пробел — пауза   ·   Esc — назад", 257, 769, 13, muted, 700);
        }

        void DrawMimicratorOrbitLayer(Graphics g, bool front, double seconds)
        {
            // Sort only three indices; no per-frame image copies or scene snapshots.
            int first = 0, second = 1, third = 2;
            if (MimicratorOrbit.Sample(first, seconds).Depth > MimicratorOrbit.Sample(second, seconds).Depth)
            { int swap = first; first = second; second = swap; }
            if (MimicratorOrbit.Sample(second, seconds).Depth > MimicratorOrbit.Sample(third, seconds).Depth)
            { int swap = second; second = third; third = swap; }
            if (MimicratorOrbit.Sample(first, seconds).Depth > MimicratorOrbit.Sample(second, seconds).Depth)
            { int swap = first; first = second; second = swap; }
            DrawMimicratorProp(g, first, front, seconds);
            DrawMimicratorProp(g, second, front, seconds);
            DrawMimicratorProp(g, third, front, seconds);
        }

        void DrawMimicratorProp(Graphics g, int item, bool front, double seconds)
        {
            Color tint = item == 0 ? Color.FromArgb(208, 151, 190) : item == 1
                ? Color.FromArgb(150, 183, 223) : Color.FromArgb(173, 151, 224);
            const int segments = 22;
            for (int segment = segments; segment > 0; segment--)
            {
                double time = seconds - segment * .065;
                var a = MimicratorOrbit.Sample(item, time);
                var b = MimicratorOrbit.Sample(item, time + .065);
                if ((a.Depth >= 0) != front) continue;
                int alpha = (int)(175 * (1f - segment / (float)(segments + 1)));
                using (var halo = new Pen(Color.FromArgb(alpha / 3, tint), 9))
                using (var line = new Pen(Color.FromArgb(alpha, tint), 1.8f))
                {
                    g.DrawLine(halo, a.Position, b.Position);
                    g.DrawLine(line, a.Position, b.Position);
                }
                if (segment % 4 == 0)
                {
                    float size = 2 + (float)Math.Sin(time * 3 + item);
                    using (var brush = new SolidBrush(Color.FromArgb(alpha, tint)))
                        g.FillEllipse(brush, a.Position.X - size / 2, a.Position.Y - size / 2, size, size);
                }
            }

            var pose = MimicratorOrbit.Sample(item, seconds);
            if ((pose.Depth >= 0) != front) return;
            float glowAlpha = item == 2 ? 42 + 23 * (float)Math.Sin(seconds * Math.PI * 2) : 40;
            Color glow = item == 2 && Math.Sin(seconds * Math.PI * 2) > 0
                ? Color.FromArgb((int)glowAlpha, 190, 79, 123) : Color.FromArgb((int)glowAlpha, tint);
            MimicratorGlow(g, pose.Position, 120 * pose.Scale, 110 * pose.Scale, glow);
            var saved = g.Save();
            try
            {
                g.TranslateTransform(pose.Position.X, pose.Position.Y);
                g.RotateTransform(pose.Rotation);
                var image = mimicratorDisplayProps[item];
                var crop = new RectangleF(0, 0, image.Width, image.Height);
                float size = (item == 0 ? 104 : item == 1 ? 74 : 87) * pose.Scale;
                float ratio = Math.Min(size / crop.Width, size / crop.Height);
                var target = new RectangleF(-crop.Width * ratio / 2, -crop.Height * ratio / 2, crop.Width * ratio, crop.Height * ratio);
                SpriteDrawing.Image(g, image, target, crop, 1, false);
            }
            finally { g.Restore(saved); }
        }
    }
}
