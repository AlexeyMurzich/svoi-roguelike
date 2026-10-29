using System;
using System.Drawing;

namespace ShpilSvoih
{
    // Approved full images stay intact. Two registered source planes share one actor floor.
    public static class DistrictStageLayout
    {
        public static float WallBase(float floor, int district)
        {
            return floor - (district == 2 ? 140 : 50);
        }
        public static RectangleF SourcePlane(Size image, int district, bool wall, SizeF target)
        {
            if (district < 0 || district > 2) throw new ArgumentOutOfRangeException("district");
            if (image.Width <= 0 || image.Height <= 0 || target.Width <= 0 || target.Height <= 0)
                throw new ArgumentException("Scene dimensions must be positive.");
            // Registration from the approved 1672 x 941 mockups, independent of render density.
            float seam = image.Height * (district == 0 ? 538f : district == 1 ? 574f : 456f) / 941f;
            var plane = new SizeF(image.Width, wall ? seam : image.Height - seam);
            float scale = Math.Max(target.Width / plane.Width, target.Height / plane.Height);
            float width = Math.Min(plane.Width, target.Width / scale);
            float height = Math.Min(plane.Height, target.Height / scale);
            // Anchor both crops to the junction: no sliding wall/ground gap during resize.
            return new RectangleF((plane.Width - width) / 2, wall ? seam - height : seam, width, height);
        }
    }
}
