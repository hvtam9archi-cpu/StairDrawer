using System;

namespace StairDrawer.Models
{
    internal struct Point2
    {
        public Point2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }

        public double Length => Math.Sqrt((X * X) + (Y * Y));

        public Point2 Normalize()
        {
            double length = Length;
            if (length <= 1e-9)
            {
                throw new InvalidOperationException("Cannot normalize a zero-length vector.");
            }

            return this / length;
        }

        public static Point2 operator +(Point2 left, Point2 right)
        {
            return new Point2(left.X + right.X, left.Y + right.Y);
        }

        public static Point2 operator -(Point2 left, Point2 right)
        {
            return new Point2(left.X - right.X, left.Y - right.Y);
        }

        public static Point2 operator *(Point2 point, double factor)
        {
            return new Point2(point.X * factor, point.Y * factor);
        }

        public static Point2 operator /(Point2 point, double divisor)
        {
            return new Point2(point.X / divisor, point.Y / divisor);
        }
    }
}
