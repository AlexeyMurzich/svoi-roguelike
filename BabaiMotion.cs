using System;

namespace ShpilSvoih
{
    // Pure absolute-time sampling: drawing cannot move a hero or apply another pull.
    public static class BabaiMotion
    {
        public const float Hit = .9f, Duration = 1.8f, PullDuration = .48f;
        public static float DisplayDistance(int distance, int before, bool lasso, bool hitApplied, float time)
        {
            if (!lasso || !hitApplied) return distance;
            return distance + (before - distance) * (1 - Motion.Ease((time - Hit) / PullDuration));
        }
        public static float RopeExtension(float time)
        {
            if (time < .22f || time >= Duration) return 0;
            if (time < Hit) return Motion.Ease((time - .22f) / (Hit - .22f));
            if (time < 1.38f) return 1;
            return 1 - Motion.Ease((time - 1.38f) / (Duration - 1.38f));
        }
        public static float Reach(float time)
        {
            if (time < .65f) return Motion.Ease(time / .65f);
            if (time < 1.38f) return 1;
            return 1 - Motion.Ease((time - 1.38f) / (Duration - 1.38f));
        }
    }
}
