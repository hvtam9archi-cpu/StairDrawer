using System;

namespace StairDrawer.Models
{
    internal enum StairPlanType
    {
        Straight,
        LShape,
        UShape
    }

    internal sealed class StairPlanParameters
    {
        public StairPlanParameters(
            StairPlanType planType,
            double floorHeight,
            int totalRisers,
            double treadDepth,
            double stairWidth,
            double landingDepth,
            double flightGap,
            double planCutHeight)
        {
            if (floorHeight <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(floorHeight));
            }

            int minimumRisers = planType == StairPlanType.Straight ? 2 : 4;
            if (totalRisers < minimumRisers)
            {
                throw new ArgumentOutOfRangeException(nameof(totalRisers));
            }

            if (treadDepth <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(treadDepth));
            }

            if (stairWidth <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(stairWidth));
            }

            if (planType != StairPlanType.Straight && landingDepth < stairWidth)
            {
                throw new ArgumentOutOfRangeException(nameof(landingDepth), "Landing depth must not be smaller than stair width.");
            }

            if (flightGap < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(flightGap));
            }

            if (planCutHeight <= 0.0 || planCutHeight >= floorHeight)
            {
                throw new ArgumentOutOfRangeException(nameof(planCutHeight));
            }

            PlanType = planType;
            FloorHeight = floorHeight;
            TotalRisers = totalRisers;
            TreadDepth = treadDepth;
            StairWidth = stairWidth;
            LandingDepth = landingDepth;
            FlightGap = flightGap;
            PlanCutHeight = planCutHeight;
        }

        public StairPlanType PlanType { get; }

        public double FloorHeight { get; }

        public int TotalRisers { get; }

        public double TreadDepth { get; }

        public double StairWidth { get; }

        public double LandingDepth { get; }

        public double FlightGap { get; }

        public double PlanCutHeight { get; }

        public double RiserHeight => FloorHeight / TotalRisers;
    }

    internal sealed class StairSectionParameters
    {
        public StairSectionParameters(
            double floorHeight,
            int totalRisers,
            double treadDepth,
            double landingLength,
            double waistSlabThickness,
            double finishThickness,
            double concreteCover)
        {
            if (floorHeight <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(floorHeight));
            }

            if (totalRisers < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(totalRisers));
            }

            if (treadDepth <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(treadDepth));
            }

            if (landingLength <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(landingLength));
            }

            if (waistSlabThickness <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(waistSlabThickness));
            }

            if (finishThickness < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(finishThickness));
            }

            if (concreteCover <= 0.0 || concreteCover >= waistSlabThickness)
            {
                throw new ArgumentOutOfRangeException(nameof(concreteCover));
            }

            FloorHeight = floorHeight;
            TotalRisers = totalRisers;
            TreadDepth = treadDepth;
            LandingLength = landingLength;
            WaistSlabThickness = waistSlabThickness;
            FinishThickness = finishThickness;
            ConcreteCover = concreteCover;
        }

        public double FloorHeight { get; }

        public int TotalRisers { get; }

        public double TreadDepth { get; }

        public double LandingLength { get; }

        public double WaistSlabThickness { get; }

        public double FinishThickness { get; }

        public double ConcreteCover { get; }

        public double RiserHeight => FloorHeight / TotalRisers;
    }
}
