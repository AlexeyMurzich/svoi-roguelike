using System;
using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        static readonly float[] DogHeadStarts = { 0, 780, 1500 }, DogHeadWidths = { 780, 720, 672 }, DogHeadPivots = { 615, 580, 535 };

        void DrawDogPose(Graphics graphics, RectangleF area)
        {
            DrawDogPoseFor(graphics, area, 0);
        }

        void DrawDogPoseFor(Graphics graphics, RectangleF area, int index)
        {
            bool warm = index == 1;
            float bodyWidth = area.Width - 60;
            var body = new RectangleF(area.X + 50, area.Bottom - bodyWidth * dogBody.Height / dogBody.Width, bodyWidth, bodyWidth * dogBody.Height / dogBody.Width);
            SpriteDrawing.Image(graphics, dogBody, body, new RectangleF(0, 0, dogBody.Width, dogBody.Height), 1, warm);
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

        void DrawDogHeadBlend(Graphics graphics, RectangleF body, int from, int to, float amount, bool warm)
        {
            amount = Motion.Ease(amount);
            DrawDogHead(graphics, body, from, 1 - amount, warm);
            DrawDogHead(graphics, body, to, amount, warm);
        }

        void DrawDogHead(Graphics graphics, RectangleF body, int frame, float opacity, bool warm)
        {
            // Explicit source gutters prevent a head sampling a neighboring frame.
            float sourceScale = dogHeads.Width / 2172f;
            var source = new RectangleF(DogHeadStarts[frame] * sourceScale, 0, DogHeadWidths[frame] * sourceScale, dogHeads.Height);
            float scale = body.Height * .92f / dogHeads.Height;
            float anchorX = body.X + body.Width * .28f, anchorY = body.Y + body.Height * .3f;
            var target = new RectangleF(anchorX - DogHeadPivots[frame] * sourceScale * scale, anchorY - dogHeads.Height * .9f * scale, source.Width * scale, dogHeads.Height * scale);
            SpriteDrawing.Image(graphics, dogHeads, target, source, opacity, warm);
        }
    }
}
