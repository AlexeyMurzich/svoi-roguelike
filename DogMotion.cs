using System;
using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        int DogPoseFrame(int index)
        {
            if (actingEnemyIndex != index || !attackTimeline.IsRunning) return 0;
            float time = attackTimeline.Time;
            if (attackTimeline.Kind == AttackAnimation.DogHowl) return time >= .20f && time < 1.0f ? 2 : 0;
            if (attackTimeline.Kind == AttackAnimation.DogBite) return time >= .18f && time < .62f ? 1 : 0;
            return 0;
        }
        PointF DogTargetPoint(int index)
        {
            var target = dogPoses.Target(index == 1 ? 1 : 0, DogPoseFrame(index), EnemyArea(index));
            return new PointF(target.Left + target.Width * .32f, target.Top + target.Height * .45f);
        }
        void DrawDogPose(Graphics graphics, RectangleF area) { DrawDogPoseFor(graphics, area, 0); }
        void DrawDogPoseFor(Graphics graphics, RectangleF area, int index)
        {
            int dog = index == 1 ? 1 : 0, pose = DogPoseFrame(index);
            var saved = graphics.Save();
            try
            {
                // The whole dog breathes and lunges as one opaque connected silhouette.
                LivingMotion.Body(graphics, area, livingSeconds, 4 + dog, false);
                dogPoses.Draw(graphics, dog, pose, area);
                if (pose == 2)
                {
                    var target = dogPoses.Target(dog, pose, area);
                    float pulse = Motion.Ease((attackTimeline.Time - .2f) / .8f);
                    float unit = area.Width / 270;
                    using (var pen = new Pen(Color.FromArgb((int)(170 * (1 - pulse)), 214, 178, 222), 2 * unit))
                        for (int i = 0; i < 3; i++)
                        {
                            float radius = (12 + pulse * 36 + i * 10) * unit;
                            float x = target.Left + target.Width * .12f, y = target.Top + target.Height * .08f;
                            graphics.DrawArc(pen, x - radius, y - radius, radius * 2, radius * 2, 200, 120);
                        }
                }
            }
            finally { graphics.Restore(saved); }
        }
    }
}
