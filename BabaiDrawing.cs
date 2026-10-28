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
        PointF BabaiTargetPoint { get { var area = BabaiBodyArea; return new PointF(area.X + area.Width * .12f, CombatFloor - area.Height * .2f); } }
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
        void DrawBabaiBody(Graphics graphics)
        {
            LoadBabaiArt();
            var area = BabaiBodyArea;
            // One atlas, three cached crops. The bed never participates in pose blending.
            float pose = IsBabaiLasso ? BabaiMotion.Pose(attackTimeline.Time) : 0;
            int first = Math.Min(2, (int)pose), next = Math.Min(2, first + 1);
            float blend = pose - first;
            DrawBabaiPose(graphics, first, area, 1 - blend);
            if (blend > 0) DrawBabaiPose(graphics, next, area, blend);
        }
        void DrawBabaiPose(Graphics graphics, int frame, RectangleF area, float opacity)
        {
            if (frame == 0)
            {
                const float hiddenScale = .58f;
                area = new RectangleF(area.X, area.Bottom - area.Height * hiddenScale, area.Width * hiddenScale, area.Height * hiddenScale);
            }
            float width = babaiBodies.Width / 3f;
            // Remove the empty top band, preserving registration. Besides saving cache
            // memory this keeps the draw rectangle inside the battle dirty region.
            const float emptyTop = .38f;
            area.Y += area.Height * emptyTop; area.Height *= 1 - emptyTop;
            var crop = new RectangleF(frame * width + 1, babaiBodies.Height * emptyTop, width - 2, babaiBodies.Height * (1 - emptyTop));
            actorArtCache.Draw(graphics, babaiBodies, area, crop, opacity, false);
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
            var trap = combat.Babai;
            float cx = CanvasWidth / 2;
            TextAt(graphics, "До кровати: " + trap.Distance + " / " + BabaiRules.InitialDistance,
                cx - 104, 133, 16, trap.Distance <= 1 ? Color.OrangeRed : gold, 250, true);
            for (int i = 0; i < BabaiRules.InitialDistance; i++)
            {
                var pip = new RectangleF(cx - 53 + i * 24, 158, 15, 7);
                Rect(graphics, pip.X, pip.Y, pip.Width, pip.Height, i < trap.Distance ? gold : Color.FromArgb(120, 50, 40, 35));
            }
            if (!IsBabaiLasso || attackTimeline.Time < .67f || attackTimeline.Time > 1.58f) return;
            var body = BabaiBodyArea;
            var start = new PointF(body.X + body.Width * .095f, body.Y + body.Height * .57f);
            float flight = Motion.Ease((attackTimeline.Time - .67f) / (BabaiMotion.Hit - .67f));
            float heroX = attackTimeline.HitApplied ? HeroSpriteCenter : HeroCenter +
                (CanvasWidth * .64f - HeroCenter) * (BabaiRules.InitialDistance - trap.Distance) / BabaiRules.InitialDistance;
            var target = new PointF(heroX, CombatFloor - 160 * Math.Min(1, .35f + .65f * BabaiDisplayDistance));
            var end = new PointF(start.X + (target.X - start.X) * flight, start.Y + (target.Y - start.Y) * flight);
            float fade = 1 - Motion.Ease((attackTimeline.Time - 1.38f) / .2f);
            using (var rope = new Pen(Color.FromArgb((int)(240 * fade), 157, 123, 72), 4))
            using (var light = new Pen(Color.FromArgb((int)(170 * fade), 225, 196, 141), 1))
            {
                var bend = new PointF((start.X + end.X) / 2, Math.Max(start.Y, end.Y) + (1 - flight) * 90);
                graphics.DrawBezier(rope, start, bend, bend, end);
                graphics.DrawBezier(light, start, bend, bend, end);
                float radius = attackTimeline.HitApplied ? 34 : 18 + flight * 20;
                graphics.DrawEllipse(rope, end.X - radius, end.Y - 22, radius * 2, 44);
                graphics.DrawEllipse(light, end.X - radius, end.Y - 22, radius * 2, 44);
            }
        }
    }
}
