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
        readonly Image[] districtCombatBackgrounds = new Image[3];

        int CombatDistrict
        {
            get
            {
                // The encounter owns its location; camera scroll and the previous room do not.
                return mapRun != null && combatNode >= 0 && combatNode < mapRun.Map.Nodes.Count
                    ? mapRun.Map.Nodes[combatNode].District : 0;
            }
        }

        Image DistrictCombatBackground()
        {
            int district = CombatDistrict;
            if (district == 0) return ordinaryCombatBackground;
            if (districtCombatBackgrounds[district] == null)
            {
                string path = district == 1 ? "combat-old-centre.png" : "combat-fomchenko.png";
                districtCombatBackgrounds[district] = assetStore.Get(path, path);
            }
            return districtCombatBackgrounds[district];
        }

        void DrawSceneBackground(Graphics graphics, bool fighting)
        {
            bool ordinary = fighting && combat != null && combat.Enemy != EnemyKind.Mimicrator;
            Image source = fighting ? ordinary ? DistrictCombatBackground() : eliteCombatBackground : mapBackground;
            float scale = RenderViewport.Scale;
            int top = (int)Math.Round(MapView.Top * scale);
            var pixels = new Size(RenderPixels.Width, Math.Max(1, RenderPixels.Height - top));
            if (sceneBackground == null || sceneViewport != pixels || sceneIsCombat != fighting || !object.ReferenceEquals(sceneSource, source))
            {
                if (sceneBackground != null) sceneBackground.Dispose();
                sceneViewport = pixels; sceneIsCombat = fighting; sceneSource = source;
                float fit = Math.Max(pixels.Width / (float)source.Width, pixels.Height / (float)source.Height);
                int height = fighting ? pixels.Height : Math.Max(pixels.Height, (int)Math.Ceiling(source.Height * fit));
                sceneBackground = new Bitmap(pixels.Width, height, PixelFormat.Format32bppPArgb);
                using (var backdrop = Graphics.FromImage(sceneBackground))
                {
                    backdrop.Clear(Color.FromArgb(7, 16, 21));
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
                // Backdrop is opaque: copy instead of blending every screen pixel.
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.DrawImageUnscaled(sceneBackground, 0, top - crop);
            }
            finally { graphics.Restore(saved); }
        }
    }
}
