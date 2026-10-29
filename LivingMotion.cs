using System;
using System.Drawing;

namespace ShpilSvoih
{
    // Absolute-time sampling: repainting never advances a pose or consumes randomness.
    public static class LivingMotion
    {
        public static float Wave(double seconds, double speed, double phase)
        {
            return (float)Math.Sin(seconds * speed + phase);
        }

        public static float Drop(double seconds, double phase)
        {
            double cycle = seconds / 2.8 + phase;
            return (float)(cycle - Math.Floor(cycle));
        }

        public static void Body(Graphics g, RectangleF area, double seconds, int character, bool flying)
        {
            float sway = Wave(seconds, .85, character * 2.1);
            float tremor = (character == 7 ? .0022f : .00035f) *
                Wave(seconds, 17 + character * .3, .7) * Wave(seconds, 2.3, character);
            // Feet and the bike base remain on their baseline; no chopped sprite seams.
            float lift = flying ? 3.5f * Wave(seconds, 1.25, .4) : 0;
            // Keep the sprite axis-aligned. Tiny per-row shear with point sampling
            // made contours ripple; filtered affine draws were also very costly.
            g.TranslateTransform(area.X + area.Width / 2 + sway * 1.25f + tremor * area.Width, area.Bottom + lift);
            g.TranslateTransform(-area.X - area.Width / 2, -area.Bottom);
        }

        public static void Saliva(Graphics g, PointF mouth, float scale, double seconds, double phase)
        {
            float cycle = Drop(seconds, phase);
            float grow = Math.Min(1, cycle / .65f);
            float length = (4 + 14 * grow) * scale;
            float bend = Wave(seconds, 2.1, phase * 6) * 2 * scale;
            using (var thread = new Pen(Color.FromArgb(155, 189, 218, 205), 1.15f * scale))
            using (var shine = new SolidBrush(Color.FromArgb(195, 216, 237, 225)))
            {
                if (cycle < .7f)
                {
                    var end = new PointF(mouth.X + bend, mouth.Y + length);
                    g.DrawBezier(thread, mouth, new PointF(mouth.X, mouth.Y + length * .4f),
                        new PointF(end.X, mouth.Y + length * .7f), end);
                    g.FillEllipse(shine, end.X - 1.3f * scale, end.Y - scale, 2.6f * scale, 3.7f * scale);
                }
                else
                {
                    float fall = (cycle - .7f) / .3f;
                    float y = mouth.Y + (18 + fall * fall * 30) * scale;
                    using (var drop = new SolidBrush(Color.FromArgb((int)(180 * (1 - fall)), 216, 237, 225)))
                        g.FillEllipse(drop, mouth.X + bend, y, 2.5f * scale, 4 * scale);
                }
            }
        }
    }

    public sealed partial class Game
    {
        double livingSeconds;
        bool LivingScene
        {
            get { return !mimicratorPreview && (!mapScreen || (HasLiveFight && combat != null &&
                (combat.Phase == CombatPhase.Player || combat.Phase == CombatPhase.Enemy))); }
        }

        void AdvanceLivingAnimation(float elapsed)
        {
            if (!LivingScene || GameplayPaused || !Motion.IsFinite(elapsed) || elapsed <= 0) return;
            livingSeconds += elapsed;
            fomatreshkaEntrance = Math.Min(FomatreshkaRules.EntranceDuration, fomatreshkaEntrance + elapsed);
            AdvanceCashier(elapsed);
            if (!CombatBusy) guardVisualTime = Math.Max(0, guardVisualTime - elapsed);
            InvalidateMotionFrame();
        }
    }
}
