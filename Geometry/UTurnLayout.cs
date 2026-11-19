using System;
using StairDrawer.Models;

namespace StairDrawer.Geometry
{
    internal sealed class UTurnLayout
    {
        public UTurnLayout(double width, double depth, double flatGap, double treadDepth, UTurnSteps steps)
        {
            Width = width;
            Depth = depth;
            Gap = steps.GetGap(flatGap, treadDepth);
            Steps = steps;
        }

        public double Width { get; }
        public double Depth { get; }
        public double Gap { get; }
        public UTurnSteps Steps { get; }

        public int GetCornerRiserCount(bool isUpperCorner)
        {
            return isUpperCorner ? Steps.SecondWinderRisers : Steps.FirstWinderRisers;
        }

        // Corner points are measured from the inside corner, starting at the lower opening.
        public Point2 GetOuterPoint(bool isUpperCorner, double index)
        {
            int intervals = GetCornerRiserCount(isUpperCorner) + 1;
            if (index == 0)
            {
                return isUpperCorner ? new Point2(Depth, 0.0) : new Point2(0.0, -Width);
            }

            if (index == intervals)
            {
                return isUpperCorner ? new Point2(0.0, Width) : new Point2(Depth, 0.0);
            }

            double angle = (Math.PI * 0.5) * index / intervals;
            double x = isUpperCorner ? Math.Cos(angle) : Math.Sin(angle);
            double y = isUpperCorner ? Math.Sin(angle) : Math.Cos(angle);
            double distance = Math.Min(Depth / x, Width / y);
            return new Point2(x * distance, (isUpperCorner ? y : -y) * distance);
        }
    }
}
