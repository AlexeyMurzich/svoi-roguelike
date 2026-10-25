using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        // Register the complete poses against the bike base. Full-opacity poses
        // avoid double faces, translucent legs and seams across the trousers.
        static readonly PointF[] ShutenPoseOffsets =
        {
            PointF.Empty, PointF.Empty, new PointF(-11, 11), new PointF(11, 10)
        };

        void DrawShutenPose(Graphics graphics, RectangleF area)
        {
            float time = attackTimeline.Time;
            bool guarding = combat.Block > 0 || guardVisualTime > .15f;
            int frame = guarding ? 3 : 0;
            if (attackTimeline.Kind == AttackAnimation.HeroGuard)
                frame = guardAtActionStart > .5f || time >= .12f ? 3 : 0;
            else if (attackTimeline.Kind == AttackAnimation.HeroStrike)
            {
                if (time < .13f) frame = guardAtActionStart > .5f ? 3 : 0;
                else if (time < .375f) frame = 1;
                else if (time < .80f) frame = 2;
            }

            shutenPoses.DrawPart(graphics, frame, area, 1, 1, ShutenPoseOffsets[frame]);
        }
    }
}
