using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        RectangleF BabaiBodyArea
        {
            get { float width = Math.Min(480, CanvasWidth * .4f); return new RectangleF(EnemyCenter(0) - width / 2, CombatFloor - width, width, width); }
        }
        PointF BabaiTargetPoint { get { var area = BabaiCrouchArea; return new PointF(area.X + area.Width * .20f, area.Y + area.Height * .65f); } }
        RectangleF BabaiBedArea
        {
            get { LoadBabaiArt(); var body = BabaiBodyArea; return BabaiStageLayout.Bed(body.X, CombatFloor, body.Width, babaiBedSource.Size); }
        }
        void DrawBabaiBed(Graphics graphics)
        {
            if (combat.Babai == null) return;
            LoadBabaiArt();
            actorArtCache.Draw(graphics, babaiBed, BabaiBedArea, babaiBedSource, 1, false);
        }
        RectangleF BabaiCrouchArea
        {
            get
            {
                var body = BabaiBodyArea;
                const float scale = .58f;
                float reach = IsBabaiLasso ? BabaiMotion.Reach(attackTimeline.Time) : 0;
                return new RectangleF(body.X + reach * body.Width * .015f,
                    body.Bottom - body.Height * scale + body.Width * .045f, body.Width * scale, body.Height * scale);
            }
        }
        void DrawBabaiBody(Graphics graphics)
        {
            LoadBabaiArt();
            var area = BabaiCrouchArea;
            // Keep the complete alpha silhouette. The foreground bed supplies occlusion.
            DrawBabaiPose(graphics, 0, area, 1);
        }
        void DrawBabaiPose(Graphics graphics, int frame, RectangleF area, float opacity)
        {
            float width = babaiBodies.Width / 3f;
            const float emptyTop = .38f;
            area.Y += area.Height * emptyTop; area.Height *= 1 - emptyTop;
            var crop = new RectangleF(frame * width + 1, babaiBodies.Height * emptyTop, width - 2, babaiBodies.Height * (1 - emptyTop));
            actorArtCache.DrawShaded(graphics, babaiBodies, area, crop, opacity);
        }
        PointF BabaiRopeHand
        {
            get { var area = BabaiCrouchArea; return new PointF(area.X + area.Width * .23f, area.Y + area.Height * .81f); }
        }
        void DrawBabaiRope(Graphics graphics)
        {
            if (!IsBabaiLasso) return;
            float time = attackTimeline.Time, flight = BabaiMotion.RopeExtension(time);
            if (flight <= 0) return;
            var start = BabaiRopeHand;
            float size = Math.Min(1, .35f + .65f * BabaiDisplayDistance);
            var target = new PointF(HeroSpriteCenter + 8 * size, CombatFloor - 155 * size);
            var end = new PointF(start.X + (target.X - start.X) * flight, start.Y + (target.Y - start.Y) * flight);
            float fade = 1 - Motion.Ease((time - 1.45f) / .25f);
            float sag = attackTimeline.HitApplied ? 5 : 12 + 55 * (1 - flight);
            var bend = new PointF((start.X + end.X) / 2, Math.Max(start.Y, end.Y) + sag);
            using (var rope = new Pen(Color.FromArgb((int)(235 * fade), 140, 110, 69), 3))
            using (var light = new Pen(Color.FromArgb((int)(160 * fade), 210, 184, 135), 1))
            {
                graphics.DrawBezier(rope, start, bend, bend, end);
                graphics.DrawBezier(light, start, bend, bend, end);
                float radius = (attackTimeline.HitApplied ? 33 : 15 + 18 * flight) * size;
                graphics.DrawEllipse(rope, end.X - radius, end.Y - 20 * size, radius * 2, 40 * size);
                graphics.DrawEllipse(light, end.X - radius, end.Y - 20 * size, radius * 2, 40 * size);
            }
        }
        void ApplyBabaiCapture(Graphics graphics, RectangleF area)
        {
            if (combat.Babai == null || BabaiDisplayDistance >= 1) return;
            float scale = .35f + .65f * Math.Max(0, BabaiDisplayDistance);
            graphics.TranslateTransform(area.X + area.Width / 2, area.Bottom);
            graphics.ScaleTransform(scale, scale);
            graphics.TranslateTransform(-area.X - area.Width / 2, -area.Bottom);
        }
        void DrawBabaiEffects(Graphics graphics)
        {
            if (combat.Babai == null) return;
            DrawBabaiRope(graphics);
            var trap = combat.Babai;
            float cx = CanvasWidth / 2;
            TextAt(graphics, "До кровати: " + trap.Distance + " / " + BabaiRules.InitialDistance,
                cx - 104, 133, 16, trap.Distance <= 1 ? Color.OrangeRed : gold, 250, true);
            for (int i = 0; i < BabaiRules.InitialDistance; i++)
            {
                var pip = new RectangleF(cx - 53 + i * 24, 158, 15, 7);
                Rect(graphics, pip.X, pip.Y, pip.Width, pip.Height, i < trap.Distance ? gold : Color.FromArgb(120, 50, 40, 35));
            }
        }
    }
}
