using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace ShpilSvoih
{
    // Curves depend on the route and canvas width, never on the scrolling camera.
    sealed class MapRouteCache : IDisposable
    {
        readonly Dictionary<int, GraphicsPath> paths = new Dictionary<int, GraphicsPath>();
        ActMap map;
        float width;
        public int Count { get { return paths.Count; } }

        public GraphicsPath Get(ActMap current, float canvasWidth, int key, PointF a, PointF b, PointF c1, PointF c2)
        {
            if (map != current || width != canvasWidth)
            {
                Clear(); map = current; width = canvasWidth;
            }
            GraphicsPath path;
            if (!paths.TryGetValue(key, out path))
            {
                path = new GraphicsPath();
                path.AddBezier(a, c1, c2, b);
                paths.Add(key, path);
            }
            return path;
        }

        public void Clear()
        {
            foreach (var path in paths.Values) path.Dispose();
            paths.Clear(); map = null;
        }
        public void Dispose() { Clear(); }
    }

    public sealed partial class Game
    {
        readonly MapRouteCache mapRoutes = new MapRouteCache();
    }
}
