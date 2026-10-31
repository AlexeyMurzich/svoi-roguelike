using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        // Values just below 16/32 ms avoid an extra coarse WM_TIMER quantum.
        // Actual elapsed time still drives all animation and camera math.
        const int ActiveFrameInterval = 15, IdleFrameInterval = 30;
        Timer combatTimer;
        long lastMotionTick;

        bool NeedsMotion
        {
            get { return presentationPending || AudioNeedsMotion || CombatBusy || CombatAmbientMoving || LivingScene || MapMoving ||
                (mimicratorPreview && !mimicratorPaused); }
        }

        int MotionInterval
        {
            get { return presentationPending || CombatBusy || MapMoving ? ActiveFrameInterval : IdleFrameInterval; }
        }

        void InitializeMotionTimer()
        {
            combatTimer = new Timer { Interval = ActiveFrameInterval };
            combatTimer.Tick += OnMotionTick;
        }

        void ResetMotionClock()
        {
            lastMotionTick = Stopwatch.GetTimestamp();
        }

        void WakeMotion()
        {
            if (combatTimer == null || (GameplayPaused && !presentationPending))
                return;
            if (!combatTimer.Enabled)
                ResetMotionClock();
            combatTimer.Interval = MotionInterval;
            combatTimer.Start();
        }

        void OnMotionTick(object sender, EventArgs args)
        {
            long now = Stopwatch.GetTimestamp();
            float elapsed = (float)((now - lastMotionTick) / (double)Stopwatch.Frequency);
            lastMotionTick = now;
            if (presentationPending && ContainsFocus) InvalidateMotionFrame();
            if (GameplayPaused || !ContainsFocus)
            {
                cashierSound.SetPaused(true);
                tohaSound.SetPaused(true);
                cardSounds.SetPaused(true);
                skaterSounds.SetPaused(true);
                howlSound.SetPaused(true);
                if (!ContainsFocus || !presentationPending) combatTimer.Stop();
                return;
            }

            // Preserve real elapsed time on slower frames; focus/overlay transitions
            // reset the clock separately, rather than slowing every animation down.
            elapsed = Motion.FrameDelta(elapsed);
            AdvanceAudio(elapsed);
            AdvanceLivingAnimation(elapsed);
            if (mimicratorPreview)
                AdvanceMimicrator(elapsed);
            else if (HasLiveFight)
            {
                AdvanceCombatAmbient(elapsed);
                AdvanceCombatAnimation(elapsed);
            }
            else
                AdvanceMapMotion(elapsed);
            SyncMusic();
            combatTimer.Interval = MotionInterval;
            if (!NeedsMotion)
                combatTimer.Stop();
        }

        void SetInventory(string view)
        {
            if (view != "" && view != "pause" && view != "deck" && view != "relics" && view != "draw" && view != "discard")
                throw new ArgumentOutOfRangeException("view");
            CancelMapDrag();
            inventoryView = view;
            hoveredCombatCard = -1;
            ResetMotionClock();
            SyncMusic();
            if (view != "")
            {
                howlSound.SetPaused(true);
                if (combatTimer != null)
                    combatTimer.Stop();
            }
            else if (NeedsMotion)
                WakeMotion();
        }
    }
}
