using System;
using StairDrawer.Models;

namespace StairDrawer.Geometry
{
    internal sealed class StairSectionCut
    {
        public StairSectionCut(
            StairPlanType type, double stairWidth, double firstRun, double secondRun, double landingDepth,
            bool hasStraightLanding, double gap = 0.0, StairCutSelection selection = default)
        {
            StairWidth = stairWidth;
            ReverseView = selection.ReverseView;
            UpperFlightY = stairWidth + gap;
            TotalWidth = stairWidth;
            if (type == StairPlanType.LShape)
            {
                TotalWidth += secondRun;
            }
            else if (type == StairPlanType.UShape)
            {
                TotalWidth += UpperFlightY;
            }

            Y = selection.Position.HasValue ? TotalWidth * selection.Position.Value : stairWidth / 3.0;
            double edgeTolerance = Math.Min(0.01, stairWidth * 0.0001);
            if (type != StairPlanType.Straight && Math.Abs(Y - stairWidth) < edgeTolerance)
            {
                Y = stairWidth - edgeTolerance;
            }
            if (type == StairPlanType.UShape && gap > edgeTolerance && Math.Abs(Y - UpperFlightY) < edgeTolerance)
            {
                Y = UpperFlightY + edgeTolerance;
            }

            double turnX = type == StairPlanType.UShape ? Math.Max(firstRun, secondRun) : firstRun;
            if (type == StairPlanType.UShape && Y >= UpperFlightY && Y > stairWidth)
            {
                StartX = turnX - secondRun;
            }
            else if (type == StairPlanType.UShape)
            {
                StartX = Y <= stairWidth ? turnX - firstRun : turnX;
            }
            else if (type == StairPlanType.LShape && Y > stairWidth)
            {
                StartX = StairPlanGeometry.GetLFlightStartX(firstRun, landingDepth, stairWidth);
            }

            EndX = turnX + (type != StairPlanType.Straight || hasStraightLanding ? landingDepth : 0.0);
            if (hasStraightLanding)
            {
                EndX += secondRun;
            }

            if (type == StairPlanType.LShape && Y > stairWidth)
            {
                EndX = StartX + stairWidth;
            }
        }

        public double StartX { get; }
        public double EndX { get; }
        public double Y { get; }
        public double TotalWidth { get; }
        public double UpperFlightY { get; }
        public bool ReverseView { get; }
        public double StairWidth { get; }

        public static StairSectionCut FromPlan(StairPlanParameters parameters)
        {
            double firstRun = (parameters.FirstFlightRisers - 1) * parameters.TreadDepth;
            double secondRun = Math.Max(0, parameters.SecondFlightRisers - 1) * parameters.TreadDepth;
            double gap = parameters.TurnSteps.GetGap(parameters.FlightGap, parameters.TreadDepth);
            return new StairSectionCut(
                parameters.PlanType, parameters.StairWidth, firstRun, secondRun, parameters.LandingDepth,
                parameters.HasStraightLanding, gap, parameters.SectionCut);
        }

        public static StairSectionCut FromSection(StairSectionParameters parameters)
        {
            double width = parameters.PlanStairWidth > 0.0 ? parameters.PlanStairWidth : parameters.LandingLength;
            double firstRun = (parameters.FirstFlightRisers - 1) * parameters.TreadDepth;
            double secondRun = Math.Max(0, parameters.SecondFlightRisers - 1) * parameters.TreadDepth;
            double gap = parameters.TurnSteps.GetGap(parameters.PlanFlightGap, parameters.TreadDepth);
            return new StairSectionCut(
                parameters.PlanType, width, firstRun, secondRun, parameters.PlanLandingDepth,
                parameters.HasStraightLanding, gap, parameters.SectionCut);
        }

        // From the lower corner pivot, a riser runs toward the near edge.
        // Only the part between the pivot and A–A is in the direction of view.
        public bool IntersectLowerWinder(Point2 outerPoint, out double visibleWidth)
        {
            double distanceFromPivot = StairWidth - Y;
            if (-outerPoint.Y <= distanceFromPivot + 1e-7)
            {
                visibleWidth = outerPoint.X;
                return false;
            }

            visibleWidth = outerPoint.X * (distanceFromPivot / -outerPoint.Y);
            return true;
        }
    }
}
