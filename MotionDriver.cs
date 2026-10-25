using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Timer combatTimer;
        long lastMotionTick;

        void InitializeMotionTimer()
        {
            combatTimer = new Timer { Interval = 16 };
            combatTimer.Tick += OnMotionTick;
        }

        void ResetMotionClock()
        {
            lastMotionTick = Stopwatch.GetTimestamp();
        }

        void WakeMotion()
        {
            if (combatTimer == null || inventoryView != "")
                return;
            if (!combatTimer.Enabled)
                ResetMotionClock();
            combatTimer.Start();
        }

        void OnMotionTick(object sender, EventArgs args)
        {
            long now = Stopwatch.GetTimestamp();
            float elapsed = (float)((now - lastMotionTick) / (double)Stopwatch.Frequency);
            lastMotionTick = now;
            if (inventoryView != "" || !ContainsFocus)
            {
                howlSound.Stop();
                combatTimer.Stop();
                return;
            }

            // Preserve real elapsed time on slower frames; focus/overlay transitions
            // reset the clock separately, rather than slowing every animation down.
            elapsed = Motion.FrameDelta(elapsed);
            if (HasLiveFight)
                AdvanceCombatAnimation(elapsed);
            else
                AdvanceMapMotion(elapsed);
            if (!CombatBusy && !MapMoving)
                combatTimer.Stop();
        }

        void SetInventory(string view)
        {
            if (view != "" && view != "pause" && view != "deck" && view != "relics")
                throw new ArgumentOutOfRangeException("view");
            CancelMapDrag();
            inventoryView = view;
            ResetMotionClock();
            if (view != "")
            {
                howlSound.Stop();
                if (combatTimer != null)
                    combatTimer.Stop();
            }
            else if (CombatBusy || MapMoving)
                WakeMotion();
        }
    }
}
