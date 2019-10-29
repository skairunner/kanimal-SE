using System;
using System.Drawing;

namespace kanimal
{
    public static class PointFExtensions
    {
        // Rotates around a point and also applies scale
        public static PointF RotateAround(this PointF point, float pivotX, float pivotY, float angle, float scaleX,
            float scaleY)
        {
            // order of transformations applied is:
            // 1. -pivot
            // 2. rotate angle
            // 3. scale
            // 4. +pivot
            var sin = (float) Math.Sin(angle);
            var cos = (float) Math.Cos(angle);
            var p1 = new PointF(point.X - pivotX, point.Y = pivotY);
            var p2 = new PointF(p1.X * cos - p1.Y * sin, p1.X * sin + p1.Y * cos);
            var p3 = new PointF(p2.X * scaleX, p2.Y * scaleY);
            var p4 = new PointF(p3.X + pivotX, p3.Y + pivotY);
            return p4;
        }
    }
}