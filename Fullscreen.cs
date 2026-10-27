using System;
using System.Drawing;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        bool isFullscreen;
        Rectangle windowedBounds;
        internal void StartFullscreen()
        {
            if (!isFullscreen)
                windowedBounds = Bounds;
            var screen = Visible ? Screen.FromControl(this) : Screen.FromPoint(Cursor.Position);
            isFullscreen = true;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = screen.Bounds;
            TopMost = true;
            RefreshScene();
        }

        void ToggleFullscreen()
        {
            if (!isFullscreen)
            {
                StartFullscreen();
                return;
            }

            isFullscreen = false;
            TopMost = false;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.Sizable;
            Bounds = windowedBounds;
            RefreshScene();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            SyncMusic();
            ResetMotionClock();
            if (CombatBusy || CombatAmbientMoving || LivingScene || MapMoving || (mimicratorPreview && !mimicratorPaused)) WakeMotion();
            if (isFullscreen)
                TopMost = true;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            music.SetTrack(music.Track, true);
            howlSound.Stop();
            CancelMapDrag();
            if (combatTimer != null) combatTimer.Stop();
            ResetMotionClock();
            if (isFullscreen)
                TopMost = false;
            base.OnDeactivate(e);
        }
    }
}
