using System;
using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        string RevealedIdentity(int nodeId)
        {
            IntelNode intel;
            return mapIntel.TryGetValue(nodeId, out intel) && intel.Revealed ? intel.Identity : "";
        }

        void DrawInvestigationTooltip(Graphics graphics)
        {
            if (inventoryView != "" || mapRun.Pending != null || hoveredRelic || mapDragging)
                return;
            string identity = RevealedIdentity(hoveredMapNode);
            if (identity.Length == 0)
                return;

            PointF node = MapNodePoint(mapRun.Map.Nodes[hoveredMapNode]);
            float width = Math.Min(340, CanvasWidth - 32);
            float x = Math.Max(16, Math.Min(CanvasWidth - width - 16, node.X + 40));
            float y = Math.Max(MapView.Top + 12, Math.Min(CanvasHeight - 110, node.Y - 45));
            Rect(graphics, x, y, width, 94, Color.FromArgb(255, 12, 27, 25));
            RelicFrame(graphics, x, y, width, 94, true);
            TextAt(graphics, "Расследование", x + 16, y + 12, 14, gold, width - 32, true);
            TextAt(graphics, identity, x + 16, y + 39, 17, text, width - 32, true);
        }
    }
}
