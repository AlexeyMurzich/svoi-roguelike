using System;

namespace ShpilSvoih
{
    public enum AttackAnimation
    {
        None,
        HeroStrike,
        HeroGuard,
        RatBite,
        DogBite,
        DogHowl,
        HedgehogNeedles,
        HedgehogBite,
        MimicratorMagic,
        MimicratorBeacon,
        MimicratorBear,
        MimicratorShell,
        MimicratorTransform,
        MimicratorRest,
        TohaRam,
        TohaDarkRam,
        TohaTransform,
        BabaiLasso,
        FomatreshkaSummon, FomatreshkaDrink, FomatreshkaShot, FanStrike
    }

    // Animation owns its clock and one impact callback, never the combat rules.
    public sealed class AttackTimeline
    {
        public const float Speed = 1.2f;
        public AttackAnimation Kind { get; private set; }
        public float Time { get; private set; }
        public bool HitApplied { get; private set; }
        public bool IsRunning { get { return Kind != AttackAnimation.None; } }
        public float Duration { get; private set; }
        public float HitTime { get; private set; }
        Action impact;
        int revision;
        public float SecondsToBoundary
        {
            get { return IsRunning ? Math.Max(.000001f, ((HitApplied ? Duration : HitTime) - Time) / Speed) : 0; }
        }

        public void Start(AttackAnimation kind, Action onImpact)
        {
            if (kind == AttackAnimation.None || !Enum.IsDefined(typeof(AttackAnimation), kind))
                throw new ArgumentOutOfRangeException("kind");
            if (onImpact == null)
                throw new ArgumentNullException("onImpact");
            if (IsRunning)
                throw new InvalidOperationException("An attack is already running.");

            Kind = kind;
            Duration = kind == AttackAnimation.HedgehogNeedles ? 1.45f : kind == AttackAnimation.HedgehogBite ? 1.05f : kind == AttackAnimation.HeroGuard ? .65f : kind == AttackAnimation.DogHowl ? 1.2f : kind == AttackAnimation.HeroStrike ? .95f : 1.05f;
            HitTime = kind == AttackAnimation.HedgehogNeedles ? 1.12f : kind == AttackAnimation.HedgehogBite ? .62f : kind == AttackAnimation.HeroGuard ? .2f : kind == AttackAnimation.DogHowl ? .6f : kind == AttackAnimation.HeroStrike ? .5f : .52f;
            if (kind == AttackAnimation.MimicratorMagic) { Duration = 1.05f; HitTime = .6f; }
            if (kind == AttackAnimation.MimicratorBeacon) { Duration = 1.15f; HitTime = .65f; }
            if (kind == AttackAnimation.MimicratorBear) { Duration = 1.25f; HitTime = .7f; }
            if (kind == AttackAnimation.MimicratorShell) { Duration = 1.05f; HitTime = .62f; }
            if (kind == AttackAnimation.MimicratorTransform) { Duration = .85f; HitTime = .425f; }
            if (kind == AttackAnimation.MimicratorRest) { Duration = .95f; HitTime = .48f; }
            if (kind == AttackAnimation.TohaRam) { Duration = 1.35f; HitTime = .62f; }
            if (kind == AttackAnimation.TohaDarkRam) { Duration = 1.5f; HitTime = .58f; }
            if (kind == AttackAnimation.TohaTransform) { Duration = 1.2f; HitTime = .6f; }
            if (kind == AttackAnimation.BabaiLasso) { Duration = BabaiMotion.Duration; HitTime = BabaiMotion.Hit; }
            if (kind == AttackAnimation.FomatreshkaSummon) { Duration = 1.65f; HitTime = .5f; }
            if (kind == AttackAnimation.FomatreshkaDrink) { Duration = 1.3f; HitTime = .7f; }
            if (kind == AttackAnimation.FomatreshkaShot) { Duration = .34f; HitTime = .12f; }
            Time = 0;
            HitApplied = false;
            impact = onImpact;
            revision++;
        }

        public void Advance(float seconds)
        {
            if (!IsRunning || !Motion.IsFinite(seconds) || seconds <= 0)
                return;

            int currentRevision = revision;
            Time = Math.Min(Duration, Time + seconds * Speed);
            if (!HitApplied && Time >= HitTime)
            {
                HitApplied = true;
                Action callback = impact;
                impact = null;
                callback();
            }

            // A callback may cancel the scene or start a new animation.
            if (revision == currentRevision && Time >= Duration)
            {
                Kind = AttackAnimation.None;
                impact = null;
            }
        }

        public void Cancel()
        {
            Kind = AttackAnimation.None;
            impact = null;
            Time = 0;
            HitApplied = false;
            revision++;
        }
    }
}
