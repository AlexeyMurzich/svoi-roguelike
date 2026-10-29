using System;

namespace ShpilSvoih
{
    // Scene-local clock. Rendering samples it without triggering audio or consuming RNG.
    public sealed class CashierAnimation
    {
        public const double GlitchDuration = 2;
        static readonly double[] Gaps = { 14.2, 17.6, 12.9, 19.4 };
        public double Seconds { get; private set; }
        public double LastGlitch { get; private set; }
        public double NextGlitch { get; private set; }
        public int Events { get; private set; }
        public double GlitchAge { get { return Seconds - LastGlitch; } }
        public bool Glitching { get { return LastGlitch >= 0 && GlitchAge >= 0 && GlitchAge < GlitchDuration; } }
        public double PoseSeconds { get { return Glitching ? LastGlitch : Seconds; } }
        public bool SplitFrame
        {
            get
            {
                if (!Glitching) return false;
                double t = GlitchAge;
                return t < .10 || (t >= .24 && t < .39) || (t >= .54 && t < .61) ||
                    (t >= .90 && t < 1.02) || (t >= 1.30 && t < 1.43) || (t >= 1.72 && t < 1.84);
            }
        }
        public CashierAnimation() { Reset(); }
        public void Reset() { Seconds = 0; LastGlitch = -1; NextGlitch = 6; Events = 0; }
        public bool Advance(float elapsed)
        {
            if (!Motion.IsFinite(elapsed) || elapsed <= 0) return false;
            Seconds += elapsed;
            bool crossed = false;
            while (Seconds >= NextGlitch)
            {
                LastGlitch = NextGlitch;
                NextGlitch += GlitchDuration + Gaps[Events % Gaps.Length];
                Events++; crossed = true;
            }
            return crossed && Glitching;
        }
    }
}
