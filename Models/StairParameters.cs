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
            StairParameterValidation.ValidatePlanType(planType);

            if (!NumericValidation.IsFinite(floorHeight) || floorHeight <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(floorHeight));
            }

            int minimumRisers = planType == StairPlanType.Straight ? 2 : 4;
            if (totalRisers < minimumRisers)
            {
                throw new ArgumentOutOfRangeException(nameof(totalRisers));
            }

            if (!NumericValidation.IsFinite(treadDepth) || treadDepth <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(treadDepth));
            }

            if (!NumericValidation.IsFinite(stairWidth) || stairWidth <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(stairWidth));
            }

            if (!NumericValidation.IsFinite(landingDepth)
                || landingDepth <= 0.0
                || (planType != StairPlanType.Straight && landingDepth < stairWidth))
            {
                throw new ArgumentOutOfRangeException(nameof(landingDepth), "Landing depth must not be smaller than stair width.");
            }

            if (!NumericValidation.IsFinite(flightGap) || flightGap < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(flightGap));
            }

            if (!NumericValidation.IsFinite(planCutHeight)
                || planCutHeight <= 0.0
                || planCutHeight >= floorHeight)
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
            StairPlanType planType,
            double floorHeight,
            int totalRisers,
            double treadDepth,
            double landingLength,
            double waistSlabThickness,
            double finishThickness)
        {
            StairParameterValidation.ValidatePlanType(planType);

            if (!NumericValidation.IsFinite(floorHeight) || floorHeight <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(floorHeight));
            }

            int minimumRisers = planType == StairPlanType.Straight ? 2 : 4;
            if (totalRisers < minimumRisers)
            {
                throw new ArgumentOutOfRangeException(nameof(totalRisers));
            }

            if (!NumericValidation.IsFinite(treadDepth) || treadDepth <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(treadDepth));
            }

            if (!NumericValidation.IsFinite(landingLength) || landingLength <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(landingLength));
            }

            if (!NumericValidation.IsFinite(waistSlabThickness) || waistSlabThickness <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(waistSlabThickness));
            }

            if (!NumericValidation.IsFinite(finishThickness) || finishThickness <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(finishThickness));
            }

            PlanType = planType;
            FloorHeight = floorHeight;
            TotalRisers = totalRisers;
            TreadDepth = treadDepth;
            LandingLength = landingLength;
            WaistSlabThickness = waistSlabThickness;
            FinishThickness = finishThickness;
        }

        public StairPlanType PlanType { get; }

        public double FloorHeight { get; }

        public int TotalRisers { get; }

        public double TreadDepth { get; }

        public double LandingLength { get; }

        public double WaistSlabThickness { get; }

        public double FinishThickness { get; }

        public double RiserHeight => FloorHeight / TotalRisers;
    }

    internal static class StairParameterValidation
    {
        public static void ValidatePlanType(StairPlanType planType)
        {
            if (planType != StairPlanType.Straight
                && planType != StairPlanType.LShape
                && planType != StairPlanType.UShape)
            {
                throw new ArgumentOutOfRangeException(nameof(planType));
            }
        }
    }

    internal static class NumericValidation
    {
        public static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
