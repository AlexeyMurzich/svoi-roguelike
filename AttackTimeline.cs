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
        DogHowl
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
            Duration = kind == AttackAnimation.HeroGuard ? .65f : kind == AttackAnimation.DogHowl ? 1.2f : kind == AttackAnimation.HeroStrike ? .95f : 1.05f;
            HitTime = kind == AttackAnimation.HeroGuard ? .2f : kind == AttackAnimation.DogHowl ? .6f : kind == AttackAnimation.HeroStrike ? .5f : .52f;
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
