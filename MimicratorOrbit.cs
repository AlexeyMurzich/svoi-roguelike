using System;
using System.Drawing;

namespace ShpilSvoih
{
    // Pure, absolute-time orbit: paint order follows depth, independent of frame rate.
    public struct MimicratorPose
    {
        public readonly PointF Position;
        public readonly float Depth, Scale, Rotation;
        public MimicratorPose(PointF position, float depth, float scale, float rotation)
        {
            Position = position; Depth = depth; Scale = scale; Rotation = rotation;
        }
    }

    public static class MimicratorOrbit
    {
        public const double Period = 12;
        public static MimicratorPose Sample(int item, double seconds)
        {
            if (item < 0 || item > 2) throw new ArgumentOutOfRangeException("item");
            if (double.IsNaN(seconds) || double.IsInfinity(seconds)) throw new ArgumentOutOfRangeException("seconds");
            double angle = seconds * Math.PI * 2 / Period + item * Math.PI * 2 / 3;
            float x = (float)Math.Sin(angle), depth = (float)Math.Cos(angle);
            float bob = (float)Math.Sin(seconds * 1.25 + item) * 7;
            return new MimicratorPose(new PointF(640 + x * 282, 447 + depth * 100 - x * 68 + bob),
                depth, .85f + .18f * depth, (float)Math.Sin(angle + .4) * 13);
        }
    }
}
