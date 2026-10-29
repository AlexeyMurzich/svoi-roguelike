using System;
using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        Image routeButtonArt;
        bool routePressed;
        RectangleF RouteButtonArea { get { return new RectangleF(CanvasWidth - 272, CanvasHeight - 209, 247, 68); } }
        bool RouteButtonVisible
        {
            get { return HasLiveFight && combat != null && combat.Phase == CombatPhase.Victory && !CombatBusy && inventoryView == ""; }
        }

        void DrawRouteButton(Graphics g)
        {
            var area = RouteButtonArea;
            bool hover = combatPointer.HasValue && area.Contains(combatPointer.Value);
            int state = hover ? routePressed ? 2 : 1 : 0;
            float cell = routeButtonArt.Width / 3f;
            g.DrawImage(routeButtonArt, area, new RectangleF(cell * state, routeButtonArt.Height * .29f, cell, routeButtonArt.Height * .37f), GraphicsUnit.Pixel);
            float inset = state == 2 ? 1 : 0;
            var label = new RectangleF(area.X + 76, area.Y + 12 + inset, area.Width - 89, 29);
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var brush = new SolidBrush(text))
            using (var hint = new SolidBrush(gold))
            {
                g.DrawString("На карту", HudFont(23, true), brush, label, format);
                label.Y += 26; label.Height = 17;
                g.DrawString("Enter", HudFont(12, false), hint, label, format);
            }
            actions.Add(Tuple.Create(area, (Action)CompleteMapRoom));
        }
    }
}
