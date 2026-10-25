using System.Drawing;

namespace ShpilSvoih
{
    // Drawing and pointer input must use exactly the same uniform transform.
    public struct Viewport
    {
        public const float DesignWidth = 1280f;
        public const float DesignHeight = 840f;
        public readonly float Scale;
        public readonly float OffsetX;
        public readonly float OffsetY;
        public readonly float Width, Height;
        private Viewport(float scale, float offsetX, float offsetY, float width = DesignWidth, float height = DesignHeight)
        {
            Scale = scale;
            OffsetX = offsetX;
            OffsetY = offsetY;
            Width = width;
            Height = height;
        }

        public static Viewport Fit(Size client)
        {
            if (client.Width <= 0 || client.Height <= 0)
                return new Viewport(0, 0, 0);
            float scale = System.Math.Min(client.Width / DesignWidth, client.Height / DesignHeight);
            return new Viewport(scale, (client.Width - DesignWidth * scale) / 2, (client.Height - DesignHeight * scale) / 2);
        }

        public bool TryMap(Point point, out PointF mapped)
        {
            mapped = PointF.Empty;
            if (Scale <= 0)
                return false;
            mapped = new PointF((point.X - OffsetX) / Scale, (point.Y - OffsetY) / Scale);
            return mapped.X >= 0 && mapped.Y >= 0 && mapped.X < Width && mapped.Y < Height;
        }

        public static Viewport FillMap(Size client)
        {
            if (client.Width <= 0 || client.Height <= 0)
                return new Viewport(0, 0, 0);
            float scale = System.Math.Min(client.Width / DesignWidth, client.Height / DesignHeight);
            return new Viewport(scale, 0, 0, client.Width / scale, client.Height / scale);
        }

        public Point ToClient(PointF point)
        {
            return new Point((int)System.Math.Round(OffsetX + point.X * Scale), (int)System.Math.Round(OffsetY + point.Y * Scale));
        }

        // Crop the art to fill the canvas without stretching the illustration.
        public static RectangleF CoverSource(Size image, SizeF target)
        {
            if (image.Width <= 0 || image.Height <= 0 || target.Width <= 0 || target.Height <= 0)
                return RectangleF.Empty;
            float scale = System.Math.Max(target.Width / image.Width, target.Height / image.Height);
            // Floating point division may exceed a source edge by a fraction of a pixel.
            float width = System.Math.Min(image.Width, target.Width / scale);
            float height = System.Math.Min(image.Height, target.Height / scale);
            return new RectangleF((image.Width - width) / 2, (image.Height - height) / 2, width, height);
        }
    }
}
