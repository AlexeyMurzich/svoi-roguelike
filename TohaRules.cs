using System;

namespace ShpilSvoih
{
    public static class TohaRules
    {
        public const int MaxHealth = 100;
        public const int DarkThresholdPercent = 30;
        public const int LightDamage = 10, DarkDamage = 16;
    }

    public static class TohaMotion
    {
        public static float Drive(float time, float hitTime, float duration)
        {
            if (!Motion.IsFinite(time) || time <= 0 || time >= duration) return 0;
            const float windup = .18f;
            if (time < windup) return -.025f * Motion.Ease(time / windup);
            if (time <= hitTime)
            {
                float p = (time - windup) / (hitTime - windup);
                return -.025f + 1.025f * p * p;
            }
            return 1 - Motion.Ease((time - hitTime) / (duration - hitTime));
        }
    }
}
