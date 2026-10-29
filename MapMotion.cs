using System;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        float mapScrollTarget, mapScrollVelocity;
        bool MapMoving { get { return Math.Abs(mapScrollTarget - mapScroll) > .01f || Math.Abs(mapScrollVelocity) > .01f; } }

        void SnapMap(float value)
        {
            mapScroll = mapScrollTarget = ClampMapScroll(value);
            mapScrollVelocity = 0;
        }

        void StopMapMotion()
        {
            mapScrollTarget = mapScroll; mapScrollVelocity = 0;
            if (!CombatBusy && !presentationPending && combatTimer != null) combatTimer.Stop();
        }

        void MoveMapTo(float value)
        {
            if (!Motion.IsFinite(value))
                return;
            mapScrollTarget = ClampMapScroll(value);
            if (MapMoving) WakeMotion();
            hoveredMapNode = -1;
        }

        void AdvanceMapMotion(float elapsed)
        {
            if (!mapScreen || mapRun == null || mapRun.Pending != null || GameplayPaused || !MapMoving) return;
            float remaining = Motion.FrameDelta(elapsed);
            while (remaining > 0)
            {
                float step = Math.Min(1f / 60, remaining);
                mapScroll = ClampMapScroll(Motion.Damp(mapScroll, mapScrollTarget, ref mapScrollVelocity, step, mapDragging ? .045f : .18f));
                remaining -= step;
            }
            hoveredMapNode = -1;
            InvalidateMotionFrame();
        }

        void CancelMapDrag()
        {
            if (mapDragging && mapDragMoved)
                suppressMapClick = true;
            mapDragging = false;
            mapDragMoved = false;
            if (Capture)
                Capture = false;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (mapDragging && !Capture)
                CancelMapDrag();
        }
    }
}
