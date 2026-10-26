using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Bitmap sceneBackground;
        Size sceneViewport;
        bool sceneIsCombat;
        Image ordinaryCombatBackground, eliteCombatBackground, sceneSource;

        void DrawSceneBackground(Graphics graphics, bool fighting)
        {
            bool ordinary = fighting && combat != null && combat.Enemy != EnemyKind.Mimicrator;
            Image source = fighting ? ordinary ? ordinaryCombatBackground : eliteCombatBackground : mapBackground;
            float scale = DrawingViewport.Scale;
            int top = (int)Math.Round(MapView.Top * scale);
            var pixels = new Size(ClientSize.Width, Math.Max(1, ClientSize.Height - top));
            if (sceneBackground == null || sceneViewport != pixels || sceneIsCombat != fighting || !object.ReferenceEquals(sceneSource, source))
            {
                if (sceneBackground != null) sceneBackground.Dispose();
                sceneViewport = pixels; sceneIsCombat = fighting; sceneSource = source;
                float fit = Math.Max(pixels.Width / (float)source.Width, pixels.Height / (float)source.Height);
                int height = fighting ? pixels.Height : Math.Max(pixels.Height, (int)Math.Ceiling(source.Height * fit));
                sceneBackground = new Bitmap(pixels.Width, height, PixelFormat.Format32bppPArgb);
                using (var backdrop = Graphics.FromImage(sceneBackground))
                {
                    backdrop.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    backdrop.DrawImage(source, new Rectangle(0, 0, pixels.Width, height), Viewport.CoverSource(source.Size, new SizeF(pixels.Width, height)), GraphicsUnit.Pixel);
                    using (var shade = new SolidBrush(fighting ? Color.FromArgb(32, 7, 16, 21) : Color.FromArgb(145, 8, 16, 23)))
                        backdrop.FillRectangle(shade, 0, 0, pixels.Width, height);
                }
            }
            float progress = fighting ? .5f : mapScroll / Math.Max(1, MapMaxScroll);
            int crop = (int)Math.Round((sceneBackground.Height - pixels.Height) * progress);
            var saved = graphics.Save();
            try
            {
                graphics.SetClip(MapView, CombineMode.Intersect);
                graphics.ResetTransform();
                graphics.DrawImageUnscaled(sceneBackground, 0, top - crop);
            }
            finally { graphics.Restore(saved); }
        }
    }
}
