using System;
using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        static readonly float[] DogHeadStarts = { 0, 780, 1500 }, DogHeadWidths = { 780, 720, 672 }, DogHeadPivots = { 480, 420, 350 };

        void DrawDogPose(Graphics graphics, RectangleF area)
        {
            DrawDogPoseFor(graphics, area, 0);
        }

        void DrawDogPoseFor(Graphics graphics, RectangleF area, int index)
        {
            bool warm = index == 1;
            var body = DogBodyArea(area);
            actorArtCache.Draw(graphics, dogBody, body, new RectangleF(0, 0, dogBody.Width, dogBody.Height), 1, warm);
            bool active = actingEnemyIndex == index;
            float time = attackTimeline.Time;
            if (active && attackTimeline.Kind == AttackAnimation.DogHowl)
            {
                if (time < .25f) DrawDogHeadBlend(graphics, body, 0, 2, time / .25f, warm);
                else if (time < .9f) DrawDogHead(graphics, body, 2, 1, warm);
                else DrawDogHeadBlend(graphics, body, 2, 0, (time - .9f) / .3f, warm);
                if (time >= .25f && time < 1f)
                {
                    float pulse = (time - .25f) / .75f;
                    using (var pen = new Pen(Color.FromArgb((int)(180 * (1 - pulse)), 214, 178, 222), 2))
                        for (int i = 0; i < 3; i++)
                        {
                            float radius = 12 + pulse * 36 + i * 10;
                            graphics.DrawArc(pen, area.Left + 45 - radius, area.Top + 35 - radius, radius * 2, radius * 2, 200, 120);
                        }
                }
            }
            else if (active && attackTimeline.Kind == AttackAnimation.DogBite)
            {
                if (time < .15f) DrawDogHead(graphics, body, 0, 1, warm);
                else if (time < .36f) DrawDogHeadBlend(graphics, body, 0, 1, (time - .15f) / .21f, warm);
                else if (time < .47f) DrawDogHead(graphics, body, 1, 1, warm);
                else DrawDogHeadBlend(graphics, body, 1, 0, (time - .47f) / .11f, warm);
            }
            else DrawDogHead(graphics, body, 0, 1, warm);
        }

        RectangleF DogBodyArea(RectangleF area)
        {
            // Uniformly scale offsets as well as the image, including small previews.
            float unit = area.Width / 270, width = 210 * unit;
            float height = width * dogBody.Height / dogBody.Width;
            return new RectangleF(area.X + 50 * unit, area.Bottom - height, width, height);
        }

        void DrawDogHeadBlend(Graphics graphics, RectangleF body, int from, int to, float amount, bool warm)
        {
            // Never dissolve two faces: both the neck and jaw must stay opaque.
            DrawDogHead(graphics, body, amount < .5f ? from : to, 1, warm);
        }

        void DrawDogHead(Graphics graphics, RectangleF body, int frame, float opacity, bool warm)
        {
            // Explicit source gutters prevent a head sampling a neighboring frame.
            float sourceScale = dogHeads.Width / 2172f;
            var source = new RectangleF(DogHeadStarts[frame] * sourceScale, 0, DogHeadWidths[frame] * sourceScale, dogHeads.Height);
            float scale = body.Height * .92f / dogHeads.Height;
            float anchorX = body.X + body.Width * .28f, anchorY = body.Y + body.Height * .34f;
            var target = new RectangleF(anchorX - DogHeadPivots[frame] * sourceScale * scale, anchorY - dogHeads.Height * .9f * scale, source.Width * scale, dogHeads.Height * scale);
            var saved = graphics.Save();
            try
            {
                // Only the head moves: the shared body and paws stay opaque and planted.
                float phase = warm ? 2.3f : .4f;
                float sway = LivingMotion.Wave(livingSeconds, 1.9, phase);
                graphics.TranslateTransform(anchorX, anchorY);
                graphics.RotateTransform(sway * .65f);
                graphics.TranslateTransform(-anchorX, -anchorY);
                actorArtCache.Draw(graphics, dogHeads, target, source, opacity, warm);
                if (frame == 0 && opacity >= 1)
                    LivingMotion.Saliva(graphics, new PointF(target.Left + target.Width * .25f, target.Top + target.Height * .60f), body.Width / 210, livingSeconds, warm ? .6 : .15);
            }
            finally { graphics.Restore(saved); }
        }
    }
}
