using System;

namespace StairDrawer.Models
{
    internal struct Point2
    {
        public Point2(double x, double y)
        {
            if (!NumericValidation.IsFinite(x))
            {
                throw new ArgumentOutOfRangeException(nameof(x));
            }

            if (!NumericValidation.IsFinite(y))
            {
                throw new ArgumentOutOfRangeException(nameof(y));
            }

            X = x;
            Y = y;
        }

        public double X { get; }

        public double Y { get; }

        public double Length => Math.Sqrt((X * X) + (Y * Y));

        public Point2 Normalize()
        {
            double length = Length;
            if (!NumericValidation.IsFinite(length) || length <= 1e-9)
            {
                throw new InvalidOperationException("Cannot normalize a non-finite or zero-length vector.");
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
