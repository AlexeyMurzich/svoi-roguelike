using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ShpilSvoih
{
    public sealed partial class Game
    {
        const int RenderPixelBudget = 1920 * 1080;
        Bitmap renderSurface;
        float renderDensity = 1;
        Viewport RenderViewport { get { return DrawingViewport.AtDensity(renderDensity); } }
        Size RenderPixels { get { return !framePixels.IsEmpty ? framePixels : renderSurface != null && renderDensity < 1 ? renderSurface.Size : ClientSize; } }

        void ClearRenderSurface()
        {
            if (renderSurface != null) renderSurface.Dispose();
            renderSurface = null;
        }

        void PaintFrame(Graphics destination)
        {
            long pixels = (long)ClientSize.Width * ClientSize.Height;
            if (pixels <= 0) return;
            if (pixels <= RenderPixelBudget)
            {
                ClearRenderSurface();
                PaintScene(destination);
                return;
            }
            float density = (float)Math.Sqrt(RenderPixelBudget / (double)pixels);
            var size = new Size(Math.Max(1, (int)(ClientSize.Width * density)), Math.Max(1, (int)(ClientSize.Height * density)));
            if (renderSurface == null || renderSurface.Size != size)
            {
                ClearRenderSurface();
                renderSurface = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb);
            }
            try
            {
                renderDensity = density;
                using (var graphics = Graphics.FromImage(renderSurface)) PaintScene(graphics);
            }
            finally { renderDensity = 1; }
            var saved = destination.Save();
            try
            {
                destination.CompositingMode = CompositingMode.SourceCopy;
                destination.InterpolationMode = InterpolationMode.NearestNeighbor;
                destination.PixelOffsetMode = PixelOffsetMode.Half;
                destination.DrawImage(renderSurface, new Rectangle(Point.Empty, ClientSize), 0, 0, size.Width, size.Height, GraphicsUnit.Pixel);
            }
            finally { destination.Restore(saved); }
        }
    }
}
