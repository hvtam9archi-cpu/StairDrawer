using System;
using System.Globalization;
using StairDrawer.Models;

namespace StairDrawer.Geometry
{
    internal static class StairPlanGeometry
    {
        private const double PlanTextHeight = 120.0;
        private const double ArrowGlobalWidth = 50.0;
        private const double HiddenLinetypeScale = 2.0;

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
            AddFlight(
                drawing,
                new Point2(0.0, 0.0),
                AxisX,
                AxisY,
                parameters.TotalRisers,
                1,
                0.0,
                parameters,
                "LEN");

            AddLevelText(drawing, new Point2(0.0, parameters.StairWidth + (PlanTextHeight * 1.2)), 0.0);
            AddLevelText(drawing, new Point2(run, parameters.StairWidth + (PlanTextHeight * 1.2)), parameters.FloorHeight);
            AddSummary(drawing, new Point2(0.0, -PlanTextHeight * 1.8), "MAT BANG THANG THANG", parameters);
        }

        private static void CreateLPlan(DrawingModel drawing, StairPlanParameters parameters)
        {
            SplitRisers(parameters.TotalRisers, out int firstFlightRisers, out int secondFlightRisers);
            double firstRun = GetFlightRun(firstFlightRisers, parameters.TreadDepth);
            double secondRun = GetFlightRun(secondFlightRisers, parameters.TreadDepth);
            double landingElevation = firstFlightRisers * parameters.RiserHeight;

            AddFlight(
                drawing,
                new Point2(0.0, 0.0),
                AxisX,
                AxisY,
                firstFlightRisers,
                1,
                0.0,
                parameters,
                "VE 1 LEN");

            AddRectangle(
                drawing,
                new Point2(firstRun, 0.0),
                parameters.LandingDepth,
                parameters.StairWidth,
                GetPlanLineLayer(landingElevation, parameters.PlanCutHeight));

            Point2 secondFlightStart = new Point2(firstRun, parameters.StairWidth);
            AddFlight(
                drawing,
                secondFlightStart,
                AxisY,
                AxisX,
                secondFlightRisers,
                firstFlightRisers + 1,
                landingElevation,
                parameters,
                "VE 2 LEN");

            AddLevelText(
                drawing,
                new Point2(firstRun + (parameters.LandingDepth * 0.15), parameters.StairWidth * 0.45),
                landingElevation);
            AddLevelText(
                drawing,
                secondFlightStart + (AxisY * secondRun) + (AxisX * (parameters.StairWidth * 0.15)),
                parameters.FloorHeight);
            AddSummary(drawing, new Point2(0.0, -PlanTextHeight * 1.8), "MAT BANG THANG CHU L", parameters);
        }

        private static void CreateUPlan(DrawingModel drawing, StairPlanParameters parameters)
        {
            SplitRisers(parameters.TotalRisers, out int firstFlightRisers, out int secondFlightRisers);
            double firstRun = GetFlightRun(firstFlightRisers, parameters.TreadDepth);
            double secondRun = GetFlightRun(secondFlightRisers, parameters.TreadDepth);
            double maximumRun = Math.Max(firstRun, secondRun);
            double totalWidth = (parameters.StairWidth * 2.0) + parameters.FlightGap;
            double landingElevation = firstFlightRisers * parameters.RiserHeight;
            Point2 firstFlightStart = new Point2(maximumRun - firstRun, 0.0);

            AddFlight(
                drawing,
                firstFlightStart,
                AxisX,
                AxisY,
                firstFlightRisers,
                1,
                0.0,
                parameters,
                "VE 1 LEN");

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
                parameters,
                "VE 2 LEN");

            AddLevelText(
                drawing,
                new Point2(maximumRun + (parameters.LandingDepth * 0.15), totalWidth * 0.45),
                landingElevation);
            AddLevelText(
                drawing,
                secondFlightStart + (new Point2(-1.0, 0.0) * secondRun) + (AxisY * (parameters.StairWidth * 0.15)),
                parameters.FloorHeight);
            AddSummary(drawing, new Point2(0.0, -PlanTextHeight * 1.8), "MAT BANG THANG CHU U", parameters);
        }

        private static void AddFlight(
            DrawingModel drawing,
            Point2 start,
            Point2 direction,
            Point2 widthDirection,
            int risers,
            int firstRiserNumber,
            double flightStartElevation,
            StairPlanParameters parameters,
            string label)
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

            AddFlightSide(drawing, start, direction, run, visibleRun);
            AddFlightSide(
                drawing,
                start + (widthDirection * parameters.StairWidth),
                direction,
                run,
                visibleRun);

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

            if (visibleRun > 1e-6 && visibleRun < run - 1e-6)
            {
                AddCutMark(drawing, start, direction, widthDirection, visibleRun, parameters);
            }

            AddDirectionArrow(drawing, start, direction, widthDirection, run, parameters.StairWidth, label);
            AddRiserNumbers(
                drawing,
                start,
                direction,
                widthDirection,
                run,
                risers,
                firstRiserNumber,
                parameters.TreadDepth,
                parameters.StairWidth);
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
            double cutDistance,
            StairPlanParameters parameters)
        {
            Point2 cutBase = flightStart + (direction * cutDistance);
            double offset = Math.Min(parameters.TreadDepth * 0.35, parameters.StairWidth * 0.08);
            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    cutBase + (direction * offset),
                    cutBase + (widthDirection * (parameters.StairWidth * 0.35)) - (direction * offset),
                    cutBase + (widthDirection * (parameters.StairWidth * 0.65)) + (direction * offset),
                    cutBase + (widthDirection * parameters.StairWidth) - (direction * offset)
                },
                false,
                StairLayers.PlanCut));

            drawing.Texts.Add(new TextPrimitive(
                cutBase + (widthDirection * (parameters.StairWidth + (PlanTextHeight * 0.45))),
                string.Format(CultureInfo.InvariantCulture, "MC +{0:0.000}", parameters.PlanCutHeight / 1000.0),
                PlanTextHeight,
                0.0,
                StairLayers.PlanText));
        }

        private static void AddDirectionArrow(
            DrawingModel drawing,
            Point2 start,
            Point2 direction,
            Point2 widthDirection,
            double run,
            double width,
            string label)
        {
            Point2 arrowStart = start + (direction * (run * 0.15)) + (widthDirection * (width * 0.5));
            Point2 arrowEnd = start + (direction * (run * 0.82)) + (widthDirection * (width * 0.5));
            double arrowLength = Math.Min(240.0, Math.Min(run * 0.15, width * 0.22));
            double arrowHalfWidth = arrowLength * 0.45;
            Point2 arrowBack = arrowEnd - (direction * arrowLength);

            drawing.Polylines.Add(new PolylinePrimitive(
                new[] { arrowStart, arrowBack },
                false,
                StairLayers.PlanAnnotation,
                ArrowGlobalWidth));
            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    arrowEnd,
                    arrowBack + (widthDirection * arrowHalfWidth),
                    arrowBack - (widthDirection * arrowHalfWidth)
                },
                true,
                StairLayers.PlanAnnotation,
                ArrowGlobalWidth));

            drawing.Texts.Add(new TextPrimitive(
                arrowStart + (widthDirection * (PlanTextHeight * 0.55)),
                label,
                PlanTextHeight,
                0.0,
                StairLayers.PlanText));
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
            double width)
        {
            for (int localRiserIndex = 1; localRiserIndex <= risers; localRiserIndex++)
            {
                int riserNumber = firstRiserNumber + localRiserIndex - 1;
                if ((riserNumber & 1) == 0)
                {
                    continue;
                }

                double distance = (localRiserIndex - 0.5) * treadDepth;
                distance = Math.Max(treadDepth * 0.25, Math.Min(run - (treadDepth * 0.25), distance));
                Point2 position = start
                    + (direction * distance)
                    + (widthDirection * (width * 0.16));
                drawing.Texts.Add(new TextPrimitive(
                    position,
                    riserNumber.ToString(CultureInfo.InvariantCulture),
                    PlanTextHeight,
                    0.0,
                    StairLayers.PlanText));
            }
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

        private static void AddLevelText(DrawingModel drawing, Point2 position, double elevation)
        {
            drawing.Texts.Add(new TextPrimitive(
                position,
                string.Format(CultureInfo.InvariantCulture, "CT +{0:0.000}", elevation / 1000.0),
                PlanTextHeight,
                0.0,
                StairLayers.PlanText));
        }

        private static void AddSummary(
            DrawingModel drawing,
            Point2 position,
            string title,
            StairPlanParameters parameters)
        {
            drawing.Texts.Add(new TextPrimitive(
                position,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} - {1} BAC, h={2:0.#}, b={3:0.#}, MC=+{4:0.000}",
                    title,
                    parameters.TotalRisers,
                    parameters.RiserHeight,
                    parameters.TreadDepth,
                    parameters.PlanCutHeight / 1000.0),
                PlanTextHeight,
                0.0,
                StairLayers.PlanText));
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
