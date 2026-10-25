using System;
using System.Drawing;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        float previousMapMaxScroll;
        float CanvasWidth
        {
            get
            {
                return DrawingViewport.Width;
            }
        }

        float CanvasHeight
        {
            get
            {
                return DrawingViewport.Height;
            }
        }

        RectangleF MapView
        {
            get
            {
                return new RectangleF(0, MapLayout.HudHeight, CanvasWidth, CanvasHeight - MapLayout.HudHeight);
            }
        }

        float MapMaxScroll
        {
            get
            {
                return Math.Max(0, MapLayout.WorldHeight - MapView.Height);
            }
        }

        float ClampMapScroll(float value)
        {
            return Math.Max(0, Math.Min(MapMaxScroll, value));
        }

        float FocusMap(MapNode node)
        {
            return ClampMapScroll(MapLayout.WorldPosition(node).Y - MapView.Height + 100);
        }

        void OpenCurrentMapRoom()
        {
            StopMapMotion();
            if (mapRun.Current.Kind != RoomKind.Start)
                mapRun.ReopenCurrent();
        }

        void DrawCenteredOverlay(Graphics g, Action<Graphics> draw)
        {
            float x = (CanvasWidth - 1280) / 2, y = (CanvasHeight - 840) / 2;
            int firstAction = actions.Count;
            var saved = g.Save();
            try
            {
                g.TranslateTransform(x, y);
                draw(g);
            }
            finally
            {
                g.Restore(saved);
                for (int i = firstAction; i < actions.Count; i++)
                {
                    var rect = actions[i].Item1;
                    rect.Offset(x, y);
                    actions[i] = Tuple.Create(rect, actions[i].Item2);
                }
            }
        }
    }
}
