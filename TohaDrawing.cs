using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        static readonly float[] TohaLightWheelXs = { .565f, .937f }, TohaDarkWheelXs = { .655f, .947f };

        void DrawToha(Graphics graphics, RectangleF area)
        {
            LoadTohaArt();
            bool morph = attackTimeline.Kind == AttackAnimation.TohaTransform;
            bool dark = tohaVisualDark;
            float opacity = 1, morphPulse = 0;
            if (morph)
            {
                float p = attackTimeline.Time / attackTimeline.Duration;
                dark = p >= .5f;
                morphPulse = (float)Math.Sin(p * Math.PI);
                opacity = .15f + .85f * Math.Abs(2 * p - 1);
            }
            var image = dark ? tohaDark : tohaLight;
            float fit = Math.Min(area.Width / image.Width, area.Height / image.Height);
            var target = new RectangleF(area.X + (area.Width - image.Width * fit) / 2, area.Bottom - image.Height * fit,
                image.Width * fit, image.Height * fit);
            float motion = IsTohaRamming ? TohaMotion.Drive(attackTimeline.Time, attackTimeline.HitTime, attackTimeline.Duration) : 0;
            float travel = Math.Max(100, target.Left - HeroCenter + (dark ? 100 : 70));
            target.Offset(-travel * motion, 0);
            using (var shadow = new SolidBrush(Color.FromArgb(dark ? 75 : 55, 8, 12, 17)))
                graphics.FillEllipse(shadow, target.X + target.Width * .13f, target.Bottom - 13, target.Width * .82f, 15);
            DrawTohaSmoke(graphics, target, dark, motion);
            float bounce = LivingMotion.Wave(livingSeconds, dark ? 12 : 8, .3) * (dark ? .85f : .5f);
            float rev = IsTohaRamming ? (float)Math.Sin(Math.Min(1, attackTimeline.Time / .18f) * Math.PI) : 0;
            var saved = graphics.Save();
            try
            {
                float pivotX = target.X + target.Width * .55f, pivotY = target.Bottom - target.Height * .08f;
                graphics.TranslateTransform(pivotX, pivotY + bounce);
                // Engine vibration is translation, not continuous resampling/tilting of the whole car.
                if (rev > 0) graphics.RotateTransform(rev * (dark ? -3f : -1.5f));
                if (morph) graphics.ScaleTransform(1 + morphPulse * .015f, 1 + morphPulse * .015f);
                graphics.TranslateTransform(-pivotX, -pivotY);
                actorArtCache.Draw(graphics, image, target, new RectangleF(0, 0, image.Width, image.Height), opacity, false);
                if (IsTohaRamming) DrawTohaWheelMotion(graphics, target, dark, motion);
            }
            finally { graphics.Restore(saved); }
            if (morph)
            {
                float radius = 35 + morphPulse * 100;
                using (var pen = new Pen(Color.FromArgb((int)(150 * morphPulse), 148, 190, 236), 3))
                    graphics.DrawEllipse(pen, target.X + target.Width * .56f - radius, target.Y + target.Height * .47f - radius * .45f, radius * 2, radius * .9f);
            }
            if (IsTohaRamming && motion > .1f)
            {
                int alpha = (int)(Math.Min(1, motion) * (dark ? 155 : 80));
                using (var streak = new Pen(Color.FromArgb(alpha, dark ? Color.LightSteelBlue : Color.Wheat), dark ? 3 : 2))
                    for (int i = 0; i < (dark ? 6 : 3); i++)
                    {
                        float y = target.Y + target.Height * (.34f + .075f * i);
                        graphics.DrawLine(streak, target.Right - 6, y, target.Right + (dark ? 90 : 45), y + 2);
                    }
            }
        }

        void DrawTohaSmoke(Graphics graphics, RectangleF target, bool dark, float drive)
        {
            for (int i = 0; i < (dark ? 6 : 4); i++)
            {
                double clock = livingSeconds * .7 + i * .19;
                float p = (float)(clock - Math.Floor(clock));
                float size = (5 + p * 18 + Math.Max(0, drive) * 14) * target.Width / 520;
                float x = target.Right - target.Width * .06f + p * (25 + Math.Max(0, drive) * 45);
                float y = target.Bottom - target.Height * .13f - p * 18;
                using (var smoke = new SolidBrush(Color.FromArgb((int)((dark ? 75 : 40) * (1 - p)), 164, 168, 177)))
                    graphics.FillEllipse(smoke, x, y, size * 1.6f, size);
            }
            if (IsTohaRamming && drive > .2f)
            {
                using (var tire = new Pen(Color.FromArgb(dark ? 135 : 65, 20, 23, 27), dark ? 6 : 4))
                    graphics.DrawLine(tire, target.X + target.Width * .6f, target.Bottom - 5,
                        target.Right + Math.Max(0, drive) * (dark ? 95 : 50), target.Bottom - 5);
            }
        }

        void DrawTohaWheelMotion(Graphics graphics, RectangleF target, bool dark, float drive)
        {
            if (drive <= 0) return;
            float[] xs = dark ? TohaDarkWheelXs : TohaLightWheelXs;
            foreach (float x in xs)
            {
                float cx = target.X + target.Width * x, cy = target.Y + target.Height * (x < .8f ? .82f : .775f);
                float radius = target.Height * (x < .8f ? .105f : .074f);
                using (var rim = new Pen(Color.FromArgb((int)(100 * drive), 206, 214, 222), 1.5f))
                    for (int spoke = 0; spoke < 4; spoke++)
                    {
                        float a = attackTimeline.Time * (dark ? 48 : 32) + spoke * (float)Math.PI / 2;
                        graphics.DrawLine(rim, cx, cy, cx + (float)Math.Cos(a) * radius * .45f, cy + (float)Math.Sin(a) * radius);
                    }
            }
        }
    }
}
