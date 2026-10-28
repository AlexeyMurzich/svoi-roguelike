using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Image babaiBackdropSource;
        Bitmap babaiBackdrop;
        Size babaiBackdropPixels;
        SizeF babaiBackdropLogical;
        float babaiBackdropFloor;
        void ClearBabaiBackdrop()
        {
            if (babaiBackdrop != null) babaiBackdrop.Dispose();
            babaiBackdrop = null;
        }
        static void SoftGroundShadow(Graphics graphics, RectangleF area, int alpha)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(area);
                using (var shade = new PathGradientBrush(path))
                {
                    shade.CenterColor = Color.FromArgb(alpha, 6, 5, 4);
                    shade.SurroundColors = new[] { Color.FromArgb(0, 6, 5, 4) };
                    graphics.FillPath(shade, path);
                }
            }
        }
        void DrawBabaiBedContact(Graphics graphics)
        {
            var bed = BabaiBedArea;
            SoftGroundShadow(graphics, new RectangleF(bed.X + 8, bed.Bottom - 110, bed.Width * .96f, 126), 115);
            // Perspective feet: nearest foot shares CombatFloor; rear feet sit farther back on the same plane.
            var feet = new[] { new PointF(.05f, .93f), new PointF(.34f, 1), new PointF(.98f, .84f) };
            foreach (var foot in feet)
            {
                float x = bed.X + bed.Width * foot.X, y = bed.Y + bed.Height * foot.Y;
                SoftGroundShadow(graphics, new RectangleF(x - 21, y - 5, 42, 12), 175);
            }
        }
        void DrawBabaiHeroContact(Graphics graphics)
        {
            if (combat.Babai == null || currentHero.Health <= 0) return;
            float size = combat.Babai.Caught ? Math.Max(0, BabaiDisplayDistance) : 1;
            SoftGroundShadow(graphics, new RectangleF(HeroSpriteCenter - 95 * size, CombatFloor - 6, 190 * size, 18 * size), 125);
        }
        void DrawBabaiBackdrop(Graphics graphics)
        {
            LoadBabaiArt();
            Image source = DistrictCombatBackground();
            float scale = RenderViewport.Scale;
            int top = (int)Math.Round(MapView.Top * scale);
            var pixels = new Size(Math.Max(1, RenderPixels.Width), Math.Max(1, RenderPixels.Height - top));
            var logical = new SizeF(CanvasWidth, CanvasHeight);
            if (babaiBackdrop == null || babaiBackdropPixels != pixels || babaiBackdropLogical != logical || babaiBackdropFloor != CombatFloor || !ReferenceEquals(babaiBackdropSource, source))
            {
                ClearBabaiBackdrop();
                // Keep only the active opaque scene cache; the map is rebuilt on return.
                if (sceneBackground != null) { sceneBackground.Dispose(); sceneBackground = null; }
                var frame = new Bitmap(pixels.Width, pixels.Height, PixelFormat.Format32bppPArgb);
                try
                {
                    using (var canvas = Graphics.FromImage(frame))
                    {
                        // Bicubic filtering can leave fractional alpha at the outermost pixels.
                        canvas.Clear(Color.FromArgb(7, 16, 21));
                        canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        canvas.DrawImage(source, new Rectangle(Point.Empty, pixels), Viewport.CoverSource(source.Size, pixels), GraphicsUnit.Pixel);
                        using (var shade = new SolidBrush(Color.FromArgb(32, 7, 16, 21)))
                            canvas.FillRectangle(shade, new Rectangle(Point.Empty, pixels));
                        canvas.ScaleTransform(scale, scale);
                        canvas.TranslateTransform(0, -MapView.Top);
                        DrawBabaiBedContact(canvas);
                    }
                    babaiBackdrop = frame; frame = null;
                    babaiBackdropPixels = pixels; babaiBackdropLogical = logical; babaiBackdropFloor = CombatFloor;
                    babaiBackdropSource = source;
                }
                finally { if (frame != null) frame.Dispose(); }
            }
            var saved = graphics.Save();
            try
            {
                graphics.SetClip(MapView, CombineMode.Intersect);
                graphics.ResetTransform(); graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.DrawImageUnscaled(babaiBackdrop, 0, top);
            }
            finally { graphics.Restore(saved); }
        }
    }
}
