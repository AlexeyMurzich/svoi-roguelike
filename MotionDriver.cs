using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        const int ActiveFrameInterval = 16, IdleFrameInterval = 33;
        Timer combatTimer;
        long lastMotionTick;

        bool NeedsMotion
        {
            get { return CombatBusy || CombatAmbientMoving || LivingScene || MapMoving ||
                (mimicratorPreview && !mimicratorPaused); }
        }

        int MotionInterval
        {
            get { return CombatBusy || MapMoving ? ActiveFrameInterval : IdleFrameInterval; }
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
            if (combatTimer == null || GameplayPaused)
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
            if (GameplayPaused || !ContainsFocus)
            {
                howlSound.Stop();
                combatTimer.Stop();
                return;
            }

            // Preserve real elapsed time on slower frames; focus/overlay transitions
            // reset the clock separately, rather than slowing every animation down.
            elapsed = Motion.FrameDelta(elapsed);
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
            if (view != "" && view != "pause" && view != "deck" && view != "relics")
                throw new ArgumentOutOfRangeException("view");
            CancelMapDrag();
            inventoryView = view;
            ResetMotionClock();
            SyncMusic();
            if (view != "")
            {
                howlSound.Stop();
                if (combatTimer != null)
                    combatTimer.Stop();
            }
            else if (NeedsMotion)
                WakeMotion();
        }
    }
}
