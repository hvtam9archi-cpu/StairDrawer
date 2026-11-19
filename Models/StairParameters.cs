using System;

namespace StairDrawer.Models
{
    internal enum StairPlanType
    {
        Straight,
        LShape,
        UShape
    }

    internal enum StairRailingType
    {
        Iron,
        Glass
    }

    internal sealed class StairPlanParameters
    {
        public StairPlanParameters(
            StairPlanType planType,
            double floorHeight,
            int totalRisers,
            int firstFlightRisers,
            double treadDepth,
            double stairWidth,
            double landingDepth,
            double flightGap,
            double planCutHeight,
            UTurnSteps turnSteps = default,
            bool hasStraightLanding = false,
            bool drawRailing = false,
            StairCutSelection sectionCut = default)
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

            StairParameterValidation.ValidateRiserDistribution(
                planType,
                totalRisers,
                firstFlightRisers,
                turnSteps,
                hasStraightLanding);

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
            FirstFlightRisers = firstFlightRisers;
            TreadDepth = treadDepth;
            StairWidth = stairWidth;
            LandingDepth = landingDepth;
            FlightGap = flightGap;
            PlanCutHeight = planCutHeight;
            TurnSteps = turnSteps;
            HasStraightLanding = hasStraightLanding;
            DrawRailing = drawRailing;
            SectionCut = sectionCut;
        }

        public StairPlanType PlanType { get; }

        public double FloorHeight { get; }

        public int TotalRisers { get; }

        public int FirstFlightRisers { get; }

        public int SecondFlightRisers => TotalRisers - FirstFlightRisers - (int)TurnSteps.TotalRisers;

        public double TreadDepth { get; }

        public double StairWidth { get; }

        public double LandingDepth { get; }

        public double FlightGap { get; }

        public double PlanCutHeight { get; }

        public UTurnSteps TurnSteps { get; }

        public bool HasStraightLanding { get; }

        public bool DrawRailing { get; }

        public StairCutSelection SectionCut { get; }

        public double RiserHeight => FloorHeight / TotalRisers;
    }

    internal sealed class StairSectionParameters
    {
        public StairSectionParameters(
            StairPlanType planType,
            double floorHeight,
            int totalRisers,
            int firstFlightRisers,
            double treadDepth,
            double landingLength,
            double waistSlabThickness,
            double finishThickness,
            bool drawRailing,
            StairRailingType railingType,
            double railingHeight,
            UTurnSteps turnSteps = default,
            double planStairWidth = 0.0,
            double planLandingDepth = 0.0,
            bool hasStraightLanding = false,
            double planFlightGap = 0.0,
            StairCutSelection sectionCut = default)
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

            StairParameterValidation.ValidateRiserDistribution(
                planType,
                totalRisers,
                firstFlightRisers,
                turnSteps,
                hasStraightLanding);

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

            if (railingType != StairRailingType.Iron && railingType != StairRailingType.Glass)
            {
                throw new ArgumentOutOfRangeException(nameof(railingType));
            }

            if (drawRailing
                && (!NumericValidation.IsFinite(railingHeight)
                    || railingHeight < 600.0
                    || railingHeight > 1500.0))
            {
                throw new ArgumentOutOfRangeException(nameof(railingHeight));
            }

            if (planType != StairPlanType.Straight && planStairWidth == 0.0 && planLandingDepth == 0.0)
            {
                planStairWidth = landingLength;
                planLandingDepth = landingLength;
            }

            if ((planType != StairPlanType.Straight || hasStraightLanding)
                && (!NumericValidation.IsFinite(planStairWidth) || planStairWidth <= 0.0
                    || !NumericValidation.IsFinite(planLandingDepth) || planLandingDepth <= 0.0
                    || (!hasStraightLanding && planLandingDepth < planStairWidth)))
            {
                throw new ArgumentException("The plan dimensions must define a valid landing.", nameof(planStairWidth));
            }

            if (!NumericValidation.IsFinite(planFlightGap) || planFlightGap < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(planFlightGap));
            }

            PlanType = planType;
            FloorHeight = floorHeight;
            TotalRisers = totalRisers;
            FirstFlightRisers = firstFlightRisers;
            TreadDepth = treadDepth;
            LandingLength = landingLength;
            WaistSlabThickness = waistSlabThickness;
            FinishThickness = finishThickness;
            DrawRailing = drawRailing;
            RailingType = railingType;
            RailingHeight = railingHeight;
            TurnSteps = turnSteps;
            PlanStairWidth = planStairWidth;
            PlanLandingDepth = planLandingDepth;
            HasStraightLanding = hasStraightLanding;
            PlanFlightGap = planFlightGap;
            SectionCut = sectionCut;
        }

        public StairPlanType PlanType { get; }

        public double FloorHeight { get; }

        public int TotalRisers { get; }

        public int FirstFlightRisers { get; }

        public int SecondFlightRisers => TotalRisers - FirstFlightRisers - (int)TurnSteps.TotalRisers;

        public double TreadDepth { get; }

        public double LandingLength { get; }

        public double WaistSlabThickness { get; }

        public double FinishThickness { get; }

        public bool DrawRailing { get; }

        public StairRailingType RailingType { get; }

        public double RailingHeight { get; }

        public UTurnSteps TurnSteps { get; }
        public double PlanStairWidth { get; }
        public double PlanLandingDepth { get; }
        public bool HasStraightLanding { get; }
        public double PlanFlightGap { get; }
        public StairCutSelection SectionCut { get; }

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

        public static void ValidateRiserDistribution(
            StairPlanType planType,
            int totalRisers,
            int firstFlightRisers,
            UTurnSteps turnSteps,
            bool hasStraightLanding)
        {
            if (planType != StairPlanType.UShape && turnSteps.HasSteps)
            {
                throw new ArgumentException("Turn steps require a U-shaped stair.", nameof(turnSteps));
            }

            if (planType != StairPlanType.Straight && hasStraightLanding)
            {
                throw new ArgumentException("The straight landing option requires a straight stair.", nameof(hasStraightLanding));
            }

            long secondFlightRisers = (long)totalRisers - firstFlightRisers - turnSteps.TotalRisers;
            if (planType == StairPlanType.Straight && !hasStraightLanding)
            {
                if (firstFlightRisers != totalRisers)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(firstFlightRisers),
                        "A straight stair must contain all risers in its first flight.");
                }

                return;
            }

            if (firstFlightRisers < 2 || secondFlightRisers < 2)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(firstFlightRisers),
                    "Each flight must contain at least two risers.");
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
