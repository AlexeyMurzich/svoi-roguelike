using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        readonly Image[] districtArt = new Image[3];
        Bitmap districtBackdrop;
        Size districtBackdropPixels;
        SizeF districtBackdropLogical;
        float districtBackdropFloor;
        int districtBackdropIndex = -1;
        int BattleDistrict { get { return combat.Enemy == EnemyKind.Babai ? 2 : mapRun.Pending.District; } }
        bool districtBackdropHasBed;
        // Elite encounters retain their own approved scenes. Test Babai also uses the Estate.
        bool UsesDistrictBackdrop
        {
            get
            {
                return combat != null && combat.Enemy != EnemyKind.Toha && combat.Enemy != EnemyKind.Mimicrator;
            }
        }
        void ClearDistrictBackdrop()
        {
            if (districtBackdrop != null) districtBackdrop.Dispose();
            districtBackdrop = null;
        }
        Image LoadDistrictArt(int district)
        {
            if (districtArt[district] == null)
            {
                string file = district == 0 ? "combat-social-stage.png" : district == 1 ? "combat-old-center-stage.png" : "combat-estate-stage.png";
                districtArt[district] = district == 0 ? ordinaryCombatBackground : assetStore.Get(file, file);
            }
            return districtArt[district];
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
            var body = BabaiCrouchArea;
            SoftGroundShadow(graphics, new RectangleF(bed.X + 20, bed.Bottom - 130, bed.Width * .89f, 145), 120);
            SoftGroundShadow(graphics, new RectangleF(body.X + 12, body.Bottom - 63, body.Width * .85f, 30), 150);
            // Perspective feet: nearest foot shares CombatFloor; rear feet sit farther back on the same plane.
            var feet = new[] { new PointF(.05f, .93f), new PointF(.34f, 1), new PointF(.98f, .84f) };
            foreach (var foot in feet)
            {
                float x = bed.X + bed.Width * foot.X, y = bed.Y + bed.Height * foot.Y;
                SoftGroundShadow(graphics, new RectangleF(x - 21, y - 5, 42, 12), 175);
            }
        }
        void DrawDistrictHeroContact(Graphics graphics)
        {
            if (!UsesDistrictBackdrop || currentHero.Health <= 0) return;
            float size = combat.Babai != null && combat.Babai.Caught ? Math.Max(0, BabaiDisplayDistance) : 1;
            SoftGroundShadow(graphics, new RectangleF(HeroSpriteCenter - 95 * size, CombatFloor - 6, 190 * size, 18 * size), 125);
        }
        void DrawDistrictBackdrop(Graphics graphics)
        {
            int district = BattleDistrict;
            Image art = LoadDistrictArt(district);
            bool hasBed = combat.Enemy == EnemyKind.Babai;
            float scale = RenderViewport.Scale;
            int top = (int)Math.Round(MapView.Top * scale);
            var pixels = new Size(Math.Max(1, RenderPixels.Width), Math.Max(1, RenderPixels.Height - top));
            var logical = new SizeF(CanvasWidth, CanvasHeight);
            if (districtBackdrop == null || districtBackdropPixels != pixels || districtBackdropLogical != logical || districtBackdropFloor != CombatFloor || districtBackdropHasBed != hasBed || districtBackdropIndex != district)
            {
                ClearDistrictBackdrop();
                // Keep only the active opaque scene cache; the map is rebuilt on return.
                if (sceneBackground != null) { sceneBackground.Dispose(); sceneBackground = null; }
                var frame = new Bitmap(pixels.Width, pixels.Height, PixelFormat.Format32bppPArgb);
                try
                {
                    using (var canvas = Graphics.FromImage(frame))
                    {
                        int seam = Math.Max(1, Math.Min(pixels.Height - 1, (int)Math.Round(DistrictStageLayout.WallBase(CombatFloor, district) * scale) - top));
                        canvas.Clear(Color.FromArgb(18, 17, 16));
                        canvas.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        // Separate planes: the wall never extends under the standing strip.
                        canvas.DrawImage(art, new Rectangle(0, 0, pixels.Width, seam), DistrictStageLayout.SourcePlane(art.Size, district, true, new SizeF(pixels.Width, seam)), GraphicsUnit.Pixel);
                        canvas.DrawImage(art, new Rectangle(0, seam, pixels.Width, pixels.Height - seam), DistrictStageLayout.SourcePlane(art.Size, district, false, new SizeF(pixels.Width, pixels.Height - seam)), GraphicsUnit.Pixel);
                        using (var ambient = new LinearGradientBrush(new Rectangle(0, seam, pixels.Width, pixels.Height - seam), Color.FromArgb(28, 4, 8, 12), Color.FromArgb(40, 4, 6, 9), 90f))
                            canvas.FillRectangle(ambient, 0, seam, pixels.Width, pixels.Height - seam);
                        canvas.ScaleTransform(scale, scale);
                        canvas.TranslateTransform(0, -MapView.Top);
                        using (var edge = new Pen(Color.FromArgb(180, 10, 8, 6), 4))
                            canvas.DrawLine(edge, 0, DistrictStageLayout.WallBase(CombatFloor, district), CanvasWidth, DistrictStageLayout.WallBase(CombatFloor, district));
                        if (hasBed) DrawBabaiBedContact(canvas);
                    }
                    districtBackdrop = frame; frame = null;
                    districtBackdropPixels = pixels; districtBackdropLogical = logical; districtBackdropFloor = CombatFloor; districtBackdropHasBed = hasBed; districtBackdropIndex = district;
                }
                finally { if (frame != null) frame.Dispose(); }
            }
            var saved = graphics.Save();
            try
            {
                graphics.SetClip(MapView, CombineMode.Intersect);
                graphics.ResetTransform(); graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.DrawImageUnscaled(districtBackdrop, 0, top);
            }
            finally { graphics.Restore(saved); }
        }
    }
}
