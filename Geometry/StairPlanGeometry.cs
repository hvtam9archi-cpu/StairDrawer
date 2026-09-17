using System;
using System.Globalization;
using StairDrawer.Models;

namespace StairDrawer.Geometry
{
    internal static class StairPlanGeometry
    {
        private const double MinimumPlanTextHeight = 100.0;
        private const double MaximumPlanTextHeight = 180.0;
        private const double PlanTextHeightRatio = 0.16;
        private const double OuterNumberOffsetRatio = 0.12;
        private const double OppositeOuterNumberOffsetRatio = 0.88;
        private const double HiddenLinetypeScale = 2.0;
        private const double DirectionStartLength = 150.0;
        private const double DirectionArrowLengthRatio = 0.22;
        private const double DirectionArrowHalfWidthRatio = 0.34;
        private const double DirectionStrokeWidth = 45.0;
        private const double CutFirstShoulderWidthRatio = 0.427;
        private const double CutFirstToothWidthRatio = 0.440;
        private const double CutSecondToothWidthRatio = 0.560;
        private const double CutSecondShoulderWidthRatio = 0.573;
        private const double CutFirstShoulderRunRatio = 0.364;
        private const double CutFirstToothRunRatio = 0.800;
        private const double CutSecondToothRunRatio = 0.067;
        private const double CutSecondShoulderRunRatio = 0.503;

        private static readonly Point2 AxisX = new Point2(1.0, 0.0);
        private static readonly Point2 AxisY = new Point2(0.0, 1.0);

        public static DrawingModel Create(StairPlanParameters parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            var drawing = new DrawingModel();
            switch (parameters.PlanType)
            {
                case StairPlanType.Straight:
                    CreateStraightPlan(drawing, parameters);
                    break;

                case StairPlanType.LShape:
                    CreateLPlan(drawing, parameters);
                    break;

                case StairPlanType.UShape:
                    CreateUPlan(drawing, parameters);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(parameters), "Unsupported stair plan type.");
            }

            return drawing;
        }

        private static void CreateStraightPlan(DrawingModel drawing, StairPlanParameters parameters)
        {
            double run = GetFlightRun(parameters.TotalRisers, parameters.TreadDepth);
            double textHeight = GetPlanTextHeight(parameters.StairWidth);

            AddFlight(
                drawing,
                new Point2(0.0, 0.0),
                AxisX,
                AxisY,
                parameters.TotalRisers,
                1,
                0.0,
                OuterNumberOffsetRatio,
                textHeight,
                parameters);

            AddDirectionPath(
                drawing,
                new[]
                {
                    new Point2(0.0, parameters.StairWidth * 0.5),
                    new Point2(run, parameters.StairWidth * 0.5)
                },
                parameters.StairWidth);
        }

        private static void CreateLPlan(DrawingModel drawing, StairPlanParameters parameters)
        {
            SplitRisers(parameters.TotalRisers, out int firstFlightRisers, out int secondFlightRisers);
            double firstRun = GetFlightRun(firstFlightRisers, parameters.TreadDepth);
            double secondRun = GetFlightRun(secondFlightRisers, parameters.TreadDepth);
            double landingElevation = firstFlightRisers * parameters.RiserHeight;
            double textHeight = GetPlanTextHeight(parameters.StairWidth);

            AddFlight(
                drawing,
                new Point2(0.0, 0.0),
                AxisX,
                AxisY,
                firstFlightRisers,
                1,
                0.0,
                OuterNumberOffsetRatio,
                textHeight,
                parameters);

            AddRectangle(
                drawing,
                new Point2(firstRun, 0.0),
                parameters.LandingDepth,
                parameters.StairWidth,
                GetPlanLineLayer(landingElevation, parameters.PlanCutHeight));

            double secondFlightOffset = (parameters.LandingDepth - parameters.StairWidth) * 0.5;
            Point2 secondFlightStart = new Point2(firstRun + secondFlightOffset, parameters.StairWidth);
            AddFlight(
                drawing,
                secondFlightStart,
                AxisY,
                AxisX,
                secondFlightRisers,
                firstFlightRisers + 1,
                landingElevation,
                OppositeOuterNumberOffsetRatio,
                textHeight,
                parameters);

            double secondFlightCenterX = secondFlightStart.X + (parameters.StairWidth * 0.5);
            AddDirectionPath(
                drawing,
                new[]
                {
                    new Point2(0.0, parameters.StairWidth * 0.5),
                    new Point2(secondFlightCenterX, parameters.StairWidth * 0.5),
                    new Point2(secondFlightCenterX, parameters.StairWidth + secondRun)
                },
                parameters.StairWidth);
        }

        private static void CreateUPlan(DrawingModel drawing, StairPlanParameters parameters)
        {
            SplitRisers(parameters.TotalRisers, out int firstFlightRisers, out int secondFlightRisers);
            double firstRun = GetFlightRun(firstFlightRisers, parameters.TreadDepth);
            double secondRun = GetFlightRun(secondFlightRisers, parameters.TreadDepth);
            double maximumRun = Math.Max(firstRun, secondRun);
            double totalWidth = (parameters.StairWidth * 2.0) + parameters.FlightGap;
            double landingElevation = firstFlightRisers * parameters.RiserHeight;
            double textHeight = GetPlanTextHeight(parameters.StairWidth);
            Point2 firstFlightStart = new Point2(maximumRun - firstRun, 0.0);

            AddFlight(
                drawing,
                firstFlightStart,
                AxisX,
                AxisY,
                firstFlightRisers,
                1,
                0.0,
                OuterNumberOffsetRatio,
                textHeight,
                parameters);

            AddRectangle(
                drawing,
                new Point2(maximumRun, 0.0),
                parameters.LandingDepth,
                totalWidth,
                GetPlanLineLayer(landingElevation, parameters.PlanCutHeight));

            Point2 secondFlightStart = new Point2(
                maximumRun,
                parameters.StairWidth + parameters.FlightGap);
            AddFlight(
                drawing,
                secondFlightStart,
                new Point2(-1.0, 0.0),
                AxisY,
                secondFlightRisers,
                firstFlightRisers + 1,
                landingElevation,
                OppositeOuterNumberOffsetRatio,
                textHeight,
                parameters);

            double firstFlightCenterY = parameters.StairWidth * 0.5;
            double secondFlightCenterY = parameters.StairWidth
                + parameters.FlightGap
                + (parameters.StairWidth * 0.5);
            double landingCenterX = maximumRun + (parameters.LandingDepth * 0.5);
            AddDirectionPath(
                drawing,
                new[]
                {
                    new Point2(firstFlightStart.X, firstFlightCenterY),
                    new Point2(landingCenterX, firstFlightCenterY),
                    new Point2(landingCenterX, secondFlightCenterY),
                    new Point2(maximumRun - secondRun, secondFlightCenterY)
                },
                parameters.StairWidth);
        }

        private static void AddFlight(
            DrawingModel drawing,
            Point2 start,
            Point2 direction,
            Point2 widthDirection,
            int risers,
            int firstRiserNumber,
            double flightStartElevation,
            double numberOffsetRatio,
            double textHeight,
            StairPlanParameters parameters)
        {
            direction = direction.Normalize();
            widthDirection = widthDirection.Normalize();
            int treadCount = Math.Max(1, risers - 1);
            double run = treadCount * parameters.TreadDepth;
            double firstTreadElevation = flightStartElevation + parameters.RiserHeight;
            double flightEndElevation = flightStartElevation + (risers * parameters.RiserHeight);
            double visibleRun = GetVisibleRun(
                run,
                firstTreadElevation,
                flightEndElevation,
                parameters.PlanCutHeight);
            bool hasCut = visibleRun > 1e-6 && visibleRun < run - 1e-6;
            double firstSideVisibleRun = visibleRun;
            double secondSideVisibleRun = visibleRun;
            if (hasCut)
            {
                GetCutDistances(
                    run,
                    visibleRun,
                    parameters.TreadDepth,
                    out firstSideVisibleRun,
                    out secondSideVisibleRun);
            }

            AddFlightSide(drawing, start, direction, run, firstSideVisibleRun);
            AddFlightSide(
                drawing,
                start + (widthDirection * parameters.StairWidth),
                direction,
                run,
                secondSideVisibleRun);

            AddPlanLine(
                drawing,
                start,
                start + (widthDirection * parameters.StairWidth),
                GetPlanLineLayer(firstTreadElevation, parameters.PlanCutHeight));

            Point2 end = start + (direction * run);
            AddPlanLine(
                drawing,
                end,
                end + (widthDirection * parameters.StairWidth),
                GetPlanLineLayer(flightEndElevation, parameters.PlanCutHeight));

            for (int treadIndex = 1; treadIndex < treadCount; treadIndex++)
            {
                Point2 riserStart = start + (direction * (treadIndex * parameters.TreadDepth));
                double riserTopElevation = flightStartElevation + ((treadIndex + 1) * parameters.RiserHeight);
                AddPlanLine(
                    drawing,
                    riserStart,
                    riserStart + (widthDirection * parameters.StairWidth),
                    GetPlanLineLayer(riserTopElevation, parameters.PlanCutHeight));
            }

            if (hasCut)
            {
                AddCutMark(
                    drawing,
                    start,
                    direction,
                    widthDirection,
                    firstSideVisibleRun,
                    secondSideVisibleRun,
                    parameters.StairWidth);
            }

            AddRiserNumbers(
                drawing,
                start,
                direction,
                widthDirection,
                run,
                risers,
                firstRiserNumber,
                parameters.TreadDepth,
                parameters.StairWidth,
                numberOffsetRatio,
                textHeight);
        }

        private static void AddFlightSide(
            DrawingModel drawing,
            Point2 start,
            Point2 direction,
            double run,
            double visibleRun)
        {
            Point2 end = start + (direction * run);
            if (visibleRun <= 1e-6)
            {
                AddPlanLine(drawing, start, end, StairLayers.PlanHidden);
                return;
            }

            if (visibleRun >= run - 1e-6)
            {
                AddPlanLine(drawing, start, end, StairLayers.PlanVisible);
                return;
            }

            Point2 cutPoint = start + (direction * visibleRun);
            AddPlanLine(drawing, start, cutPoint, StairLayers.PlanVisible);
            AddPlanLine(drawing, cutPoint, end, StairLayers.PlanHidden);
        }

        private static void AddPlanLine(DrawingModel drawing, Point2 start, Point2 end, string layerName)
        {
            double linetypeScale = layerName == StairLayers.PlanHidden ? HiddenLinetypeScale : 1.0;
            drawing.Lines.Add(new LinePrimitive(start, end, layerName, linetypeScale));
        }

        private static void AddCutMark(
            DrawingModel drawing,
            Point2 flightStart,
            Point2 direction,
            Point2 widthDirection,
            double firstSideCutDistance,
            double secondSideCutDistance,
            double stairWidth)
        {
            double cutRun = firstSideCutDistance - secondSideCutDistance;
            Point2 cutStart = flightStart + (direction * firstSideCutDistance);
            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    cutStart,
                    cutStart
                        + (widthDirection * (stairWidth * CutFirstShoulderWidthRatio))
                        - (direction * (cutRun * CutFirstShoulderRunRatio)),
                    cutStart
                        + (widthDirection * (stairWidth * CutFirstToothWidthRatio))
                        - (direction * (cutRun * CutFirstToothRunRatio)),
                    cutStart
                        + (widthDirection * (stairWidth * CutSecondToothWidthRatio))
                        - (direction * (cutRun * CutSecondToothRunRatio)),
                    cutStart
                        + (widthDirection * (stairWidth * CutSecondShoulderWidthRatio))
                        - (direction * (cutRun * CutSecondShoulderRunRatio)),
                    flightStart
                        + (direction * secondSideCutDistance)
                        + (widthDirection * stairWidth)
                },
                false,
                StairLayers.PlanCut,
                drawOnTop: true));
        }

        private static void AddDirectionPath(
            DrawingModel drawing,
            Point2[] pathVertices,
            double stairWidth)
        {
            if (pathVertices == null || pathVertices.Length < 2)
            {
                throw new ArgumentException("A stair direction path requires at least two vertices.", nameof(pathVertices));
            }

            Point2 initialDirection = (pathVertices[1] - pathVertices[0]).Normalize();
            Point2 finalDirection = (pathVertices[pathVertices.Length - 1]
                - pathVertices[pathVertices.Length - 2]).Normalize();
            Point2 finalWidthDirection = new Point2(-finalDirection.Y, finalDirection.X);
            Point2 start = pathVertices[0];
            Point2 end = pathVertices[pathVertices.Length - 1];
            double firstSegmentLength = (pathVertices[1] - pathVertices[0]).Length;
            double finalSegmentLength = (end - pathVertices[pathVertices.Length - 2]).Length;
            double startLength = Math.Min(DirectionStartLength, firstSegmentLength * 0.30);
            double arrowLength = Math.Min(
                stairWidth * DirectionArrowLengthRatio,
                finalSegmentLength * 0.45);
            double arrowHalfWidth = Math.Min(
                stairWidth * DirectionArrowHalfWidthRatio,
                finalSegmentLength * 0.40);
            Point2 arrowBack = end - (finalDirection * arrowLength);

            drawing.Polylines.Add(new PolylinePrimitive(
                pathVertices,
                false,
                StairLayers.PlanAnnotation,
                drawOnTop: true));

            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    start,
                    start + (initialDirection * startLength)
                },
                false,
                StairLayers.PlanAnnotation,
                DirectionStrokeWidth,
                drawOnTop: true));

            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    arrowBack + (finalWidthDirection * arrowHalfWidth),
                    end,
                    arrowBack - (finalWidthDirection * arrowHalfWidth)
                },
                false,
                StairLayers.PlanAnnotation,
                DirectionStrokeWidth,
                drawOnTop: true));
        }

        private static void AddRiserNumbers(
            DrawingModel drawing,
            Point2 start,
            Point2 direction,
            Point2 widthDirection,
            double run,
            int risers,
            int firstRiserNumber,
            double treadDepth,
            double width,
            double numberOffsetRatio,
            double textHeight)
        {
            for (int localRiserIndex = 1; localRiserIndex <= risers; localRiserIndex++)
            {
                int riserNumber = firstRiserNumber + localRiserIndex - 1;
                if ((riserNumber & 1) == 0)
                {
                    continue;
                }

                double distance = (localRiserIndex - 0.5) * treadDepth;
                distance = Math.Max(treadDepth * 0.5, Math.Min(run - (treadDepth * 0.5), distance));
                Point2 position = start
                    + (direction * distance)
                    + (widthDirection * (width * numberOffsetRatio));
                drawing.Texts.Add(new TextPrimitive(
                    position,
                    riserNumber.ToString(CultureInfo.InvariantCulture),
                    textHeight,
                    0.0,
                    StairLayers.PlanText,
                    TextAnchor.MiddleCenter));
            }
        }

        private static void GetCutDistances(
            double run,
            double visibleRun,
            double treadDepth,
            out double firstSideVisibleRun,
            out double secondSideVisibleRun)
        {
            double maximumHalfSpan = Math.Min(visibleRun, run - visibleRun);
            double halfSpan = Math.Min(treadDepth * 0.5, maximumHalfSpan);
            firstSideVisibleRun = visibleRun + halfSpan;
            secondSideVisibleRun = visibleRun - halfSpan;
        }

        private static void AddRectangle(
            DrawingModel drawing,
            Point2 bottomLeft,
            double length,
            double width,
            string layerName)
        {
            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    bottomLeft,
                    bottomLeft + new Point2(length, 0.0),
                    bottomLeft + new Point2(length, width),
                    bottomLeft + new Point2(0.0, width)
                },
                true,
                layerName,
                0.0,
                layerName == StairLayers.PlanHidden ? HiddenLinetypeScale : 1.0));
        }

        private static double GetPlanTextHeight(double stairWidth)
        {
            return Math.Max(
                MinimumPlanTextHeight,
                Math.Min(MaximumPlanTextHeight, stairWidth * PlanTextHeightRatio));
        }

        private static string GetPlanLineLayer(double elevation, double planCutHeight)
        {
            return elevation <= planCutHeight + 1e-6
                ? StairLayers.PlanVisible
                : StairLayers.PlanHidden;
        }

        private static double GetVisibleRun(
            double run,
            double firstTreadElevation,
            double flightEndElevation,
            double planCutHeight)
        {
            if (planCutHeight <= firstTreadElevation)
            {
                return 0.0;
            }

            if (planCutHeight >= flightEndElevation)
            {
                return run;
            }

            double elevationRange = flightEndElevation - firstTreadElevation;
            return run * ((planCutHeight - firstTreadElevation) / elevationRange);
        }

        private static void SplitRisers(int totalRisers, out int firstFlightRisers, out int secondFlightRisers)
        {
            firstFlightRisers = totalRisers / 2;
            secondFlightRisers = totalRisers - firstFlightRisers;
        }

        private static double GetFlightRun(int risers, double treadDepth)
        {
            return Math.Max(1, risers - 1) * treadDepth;
        }
    }
}
