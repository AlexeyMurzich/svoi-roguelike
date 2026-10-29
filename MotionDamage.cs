using System;
using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Rectangle MotionDamage
        {
            get
            {
                if (mapScreen && mapRun != null && mapRun.Pending == null && !GameplayPaused &&
                    (long)ClientSize.Width * ClientSize.Height <= RenderPixelBudget)
                    return MapFrameDamage;
                if (!HasLiveFight || combat == null || GameplayPaused || deathVisuals.IsRunning ||
                    attackTimeline.Kind == AttackAnimation.TohaTransform ||
                    (long)ClientSize.Width * ClientSize.Height > RenderPixelBudget) return ClientRectangle;
                // Include actors, intents, HP, statuses and the combat notice;
                // the hand and HUD remain untouched until a gameplay change.
                var viewport = DrawingViewport;
                int top = (int)Math.Floor(MapView.Top * viewport.Scale);
                int bottom = (int)Math.Ceiling(Math.Min(CanvasHeight - 260, CombatFloor + 65) * viewport.Scale);
                return Rectangle.Intersect(ClientRectangle, new Rectangle(0, top, ClientSize.Width, Math.Max(1, bottom - top)));
            }
        }
        Rectangle MapFrameDamage
        {
            get
            {
                var viewport = DrawingViewport;
                int top = (int)Math.Floor(MapView.Top * viewport.Scale);
                return Rectangle.Intersect(ClientRectangle, new Rectangle(0, top, ClientSize.Width, Math.Max(1, ClientSize.Height - top)));
            }
        }
        void InvalidateMotionFrame() { Invalidate(MotionDamage); }
    }
}
