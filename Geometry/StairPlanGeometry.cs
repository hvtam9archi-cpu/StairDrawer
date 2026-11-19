using System;
using System.Collections.Generic;
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

            AddSectionTrace(drawing, StairSectionCut.FromPlan(parameters), parameters.StairWidth);
            return drawing;
        }

        private static void AddSectionTrace(DrawingModel drawing, StairSectionCut cut, double stairWidth)
        {
            double overhang = Math.Min(240.0, stairWidth * 0.28);
            double arrowHeight = Math.Min(150.0, stairWidth * 0.16);
            double direction = cut.ReverseView ? -1.0 : 1.0;
            double left = cut.StartX - overhang;
            double right = cut.EndX + overhang;
            AddPlanLine(drawing, new Point2(left, cut.Y), new Point2(right, cut.Y), StairLayers.SectionTrace);

            foreach (double x in new[] { left, right })
            {
                Point2 start = new Point2(x, cut.Y);
                Point2 tip = new Point2(x, cut.Y + (arrowHeight * direction));
                AddPlanLine(drawing, start, tip, StairLayers.SectionTraceMark);
                double head = arrowHeight * 0.27;
                drawing.Polylines.Add(new PolylinePrimitive(
                    new[]
                    {
                        new Point2(x - head, tip.Y - (head * 1.7 * direction)), tip,
                        new Point2(x + head, tip.Y - (head * 1.7 * direction))
                    },
                    false, StairLayers.SectionTraceMark, drawOnTop: true));
                drawing.Texts.Add(new TextPrimitive(
                    new Point2(x, tip.Y + (arrowHeight * 0.63 * direction)), "A", GetPlanTextHeight(stairWidth),
                    0.0, StairLayers.SectionTraceMark, TextAnchor.MiddleCenter));
            }
        }

        private static void CreateStraightPlan(DrawingModel drawing, StairPlanParameters parameters)
        {
            double run = GetFlightRun(parameters.FirstFlightRisers, parameters.TreadDepth);
            double textHeight = GetPlanTextHeight(parameters.StairWidth);

            AddFlight(
                drawing,
                new Point2(0.0, 0.0),
                AxisX,
                AxisY,
                parameters.FirstFlightRisers,
                1,
                0.0,
                OuterNumberOffsetRatio,
                textHeight,
                parameters);

            if (parameters.HasStraightLanding)
            {
                double upperStartX = run + parameters.LandingDepth;
                double landingElevation = parameters.FirstFlightRisers * parameters.RiserHeight;
                string layer = GetPlanLineLayer(landingElevation, parameters.PlanCutHeight);
                AddPlanLine(drawing, new Point2(run, 0.0), new Point2(upperStartX, 0.0), layer);
                AddPlanLine(
                    drawing, new Point2(run, parameters.StairWidth), new Point2(upperStartX, parameters.StairWidth), layer);
                AddFlight(
                    drawing, new Point2(upperStartX, 0.0), AxisX, AxisY,
                    parameters.SecondFlightRisers, parameters.FirstFlightRisers + 1, landingElevation,
                    OuterNumberOffsetRatio, textHeight, parameters);
                run = upperStartX + GetFlightRun(parameters.SecondFlightRisers, parameters.TreadDepth);
            }

            AddDirectionPath(
                drawing,
                new[]
                {
                    new Point2(0.0, parameters.StairWidth * 0.5),
                    new Point2(run, parameters.StairWidth * 0.5)
                },
                parameters.StairWidth);

            if (parameters.DrawRailing)
            {
                double inset = StairRailingDimensions.GetInset(parameters.StairWidth);
                double farSide = parameters.StairWidth - inset;
                AddRailingPath(drawing, parameters, inset, new Point2(0.0, inset), new Point2(run, inset));
                AddRailingPath(drawing, parameters, inset, new Point2(0.0, farSide), new Point2(run, farSide));
            }
        }

        private static void CreateLPlan(DrawingModel drawing, StairPlanParameters parameters)
        {
            int firstFlightRisers = parameters.FirstFlightRisers;
            int secondFlightRisers = parameters.SecondFlightRisers;
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

            double secondFlightOffset = (parameters.LandingDepth - parameters.StairWidth) * 0.5;
            Point2 secondFlightStart = new Point2(
                GetLFlightStartX(firstRun, parameters.LandingDepth, parameters.StairWidth), parameters.StairWidth);
            Point2 landingBottomLeft = new Point2(firstRun, 0.0);
            Point2 landingBottomRight = landingBottomLeft + (AxisX * parameters.LandingDepth);
            Point2 landingTopRight = landingBottomRight + (AxisY * parameters.StairWidth);
            string landingLayer = GetPlanLineLayer(landingElevation, parameters.PlanCutHeight);
            AddPlanLine(drawing, landingBottomLeft, landingBottomRight, landingLayer);
            AddPlanLine(drawing, landingBottomRight, landingTopRight, landingLayer);
            if (secondFlightOffset > 1e-6)
            {
                Point2 landingTopLeft = landingBottomLeft + (AxisY * parameters.StairWidth);
                Point2 secondFlightOpeningEnd = secondFlightStart + (AxisX * parameters.StairWidth);
                AddPlanLine(drawing, landingTopLeft, secondFlightStart, landingLayer);
                AddPlanLine(drawing, secondFlightOpeningEnd, landingTopRight, landingLayer);
            }

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

            if (parameters.DrawRailing)
            {
                double inset = StairRailingDimensions.GetInset(parameters.StairWidth);
                double insideY = parameters.StairWidth - inset;
                double landingRight = landingBottomRight.X - inset;
                double upperLeft = secondFlightStart.X + inset;
                double upperRight = secondFlightStart.X + parameters.StairWidth - inset;
                double endY = secondFlightStart.Y + secondRun;
                AddRailingPath(drawing, parameters, inset,
                    new Point2(0.0, inset), new Point2(landingRight, inset),
                    new Point2(landingRight, insideY), new Point2(upperRight, insideY), new Point2(upperRight, endY));
                AddRailingPath(drawing, parameters, inset,
                    new Point2(0.0, insideY), new Point2(upperLeft, insideY), new Point2(upperLeft, endY));
            }
        }

        internal static double GetLFlightStartX(double firstRun, double landingDepth, double stairWidth)
        {
            return firstRun + ((landingDepth - stairWidth) * 0.5);
        }

        private static void CreateUPlan(DrawingModel drawing, StairPlanParameters parameters)
        {
            int firstFlightRisers = parameters.FirstFlightRisers;
            int secondFlightRisers = parameters.SecondFlightRisers;
            double firstRun = GetFlightRun(firstFlightRisers, parameters.TreadDepth);
            double secondRun = GetFlightRun(secondFlightRisers, parameters.TreadDepth);
            double maximumRun = Math.Max(firstRun, secondRun);
            var turn = new UTurnLayout(
                parameters.StairWidth, parameters.LandingDepth, parameters.FlightGap,
                parameters.TreadDepth, parameters.TurnSteps);
            double totalWidth = (turn.Width * 2.0) + turn.Gap;
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
                turn.Steps.HasSteps ? 0.20 : OuterNumberOffsetRatio,
                textHeight,
                parameters,
                labelLastRiser: !turn.Steps.HasSteps);

            Point2 secondFlightStart = new Point2(
                maximumRun,
                turn.Width + turn.Gap);
            if (turn.Steps.HasSteps)
            {
                AddUSteppedTurn(drawing, parameters, turn, secondFlightStart, textHeight);
            }
            else
            {
                Point2 landingBottomLeft = new Point2(maximumRun, 0.0);
                Point2 landingBottomRight = landingBottomLeft + (AxisX * turn.Depth);
                Point2 landingTopRight = landingBottomRight + (AxisY * totalWidth);
                Point2 landingTopLeft = landingBottomLeft + (AxisY * totalWidth);
                string landingLayer = GetPlanLineLayer(landingElevation, parameters.PlanCutHeight);
                AddPlanLine(drawing, landingBottomLeft, landingBottomRight, landingLayer);
                AddPlanLine(drawing, landingBottomRight, landingTopRight, landingLayer);
                AddPlanLine(drawing, landingTopRight, landingTopLeft, landingLayer);
                if (turn.Gap > 1e-6)
                {
                    Point2 firstFlightOpeningEnd = landingBottomLeft + (AxisY * turn.Width);
                    AddPlanLine(drawing, firstFlightOpeningEnd, secondFlightStart, landingLayer);
                }
            }

            int risersBeforeUpperFlight = parameters.TotalRisers - secondFlightRisers;

            AddFlight(
                drawing,
                secondFlightStart,
                new Point2(-1.0, 0.0),
                AxisY,
                secondFlightRisers,
                risersBeforeUpperFlight + 1,
                risersBeforeUpperFlight * parameters.RiserHeight,
                turn.Steps.HasSteps ? 0.80 : OppositeOuterNumberOffsetRatio,
                textHeight,
                parameters);

            double firstFlightCenterY = parameters.StairWidth * 0.5;
            double secondFlightCenterY = totalWidth - firstFlightCenterY;
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

            if (parameters.DrawRailing)
            {
                double inset = StairRailingDimensions.GetInset(turn.Width);
                double outerX = maximumRun + turn.Depth - inset;
                double innerX = maximumRun + inset;
                double lowerInnerY = turn.Width - inset;
                double upperInnerY = secondFlightStart.Y + inset;
                double upperOuterY = totalWidth - inset;
                double endX = maximumRun - secondRun;
                AddRailingPath(drawing, parameters, inset,
                    new Point2(firstFlightStart.X, inset), new Point2(outerX, inset),
                    new Point2(outerX, upperOuterY), new Point2(endX, upperOuterY));
                AddRailingPath(drawing, parameters, inset,
                    new Point2(firstFlightStart.X, lowerInnerY), new Point2(innerX, lowerInnerY),
                    new Point2(innerX, upperInnerY), new Point2(endX, upperInnerY));
            }
        }

        private static void AddRailingPath(DrawingModel drawing, StairPlanParameters parameters, double inset, params Point2[] points)
        {
            var centerline = new List<Point2>(points.Length);
            foreach (Point2 point in points)
            {
                if (centerline.Count == 0 || (point - centerline[centerline.Count - 1]).Length > 1e-9)
                {
                    centerline.Add(point);
                }
            }

            double halfWidth = Math.Min(StairRailingDimensions.HandrailWidth, inset) * 0.5;
            var outline = new Point2[centerline.Count * 2];
            for (int index = 0; index < centerline.Count; index++)
            {
                Point2 previous = index > 0
                    ? (centerline[index] - centerline[index - 1]).Normalize()
                    : (centerline[1] - centerline[0]).Normalize();
                Point2 next = index < centerline.Count - 1
                    ? (centerline[index + 1] - centerline[index]).Normalize()
                    : previous;
                double dot = (previous.X * next.X) + (previous.Y * next.Y);
                Point2 offset = new Point2(-previous.Y - next.Y, previous.X + next.X) * (halfWidth / (1.0 + dot));
                outline[index] = centerline[index] + offset;
                outline[outline.Length - 1 - index] = centerline[index] - offset;
            }

            drawing.Polylines.Add(new PolylinePrimitive(
                outline, true, StairLayers.Railing, drawOnTop: true));
            StairPlanRailingGeometry.AppendFixings(drawing, parameters, centerline);
        }

        private static void AddUSteppedTurn(
            DrawingModel drawing,
            StairPlanParameters parameters,
            UTurnLayout turn,
            Point2 upperCorner,
            double textHeight)
        {
            Point2 lowerCorner = new Point2(upperCorner.X, turn.Width);
            int afterLowerCorner = parameters.FirstFlightRisers + turn.Steps.FirstWinderRisers;
            AddWinderCorner(drawing, parameters, turn, lowerCorner, false, parameters.FirstFlightRisers, textHeight, true);

            if (turn.Steps.HasMiddleFlight)
            {
                AddFlight(
                    drawing, lowerCorner, AxisY, AxisX, turn.Steps.MiddleFlightRisers,
                    afterLowerCorner + 1, afterLowerCorner * parameters.RiserHeight,
                    0.80, textHeight, parameters, turn.Depth, labelLastRiser: false);
            }
            else if (turn.Gap > 1e-6)
            {
                string layer = GetPlanLineLayer(afterLowerCorner * parameters.RiserHeight, parameters.PlanCutHeight);
                Point2 offset = AxisX * turn.Depth;
                AddPlanLine(drawing, lowerCorner, upperCorner, layer);
                AddPlanLine(drawing, lowerCorner + offset, upperCorner + offset, layer);
            }

            AddWinderCorner(
                drawing, parameters, turn, upperCorner, true, afterLowerCorner + turn.Steps.MiddleFlightRisers,
                textHeight, turn.Steps.HasMiddleFlight);
        }

        private static void AddWinderCorner(
            DrawingModel drawing,
            StairPlanParameters parameters,
            UTurnLayout turn,
            Point2 pivot,
            bool isUpperCorner,
            int previousRisers,
            double textHeight,
            bool labelEntry)
        {
            int risers = turn.GetCornerRiserCount(isUpperCorner);
            Point2 outsideCorner = pivot + new Point2(turn.Depth, isUpperCorner ? turn.Width : -turn.Width);
            Point2 previousOuter = pivot + turn.GetOuterPoint(isUpperCorner, 0);
            for (int index = 1; index <= risers + 1; index++)
            {
                Point2 outer = pivot + turn.GetOuterPoint(isUpperCorner, index);
                int riserNumber = previousRisers + index;
                double elevation = riserNumber * parameters.RiserHeight;
                string edgeLayer = GetPlanLineLayer(elevation - parameters.RiserHeight, parameters.PlanCutHeight);
                if (Math.Abs(outer.X - previousOuter.X) > 1e-6 && Math.Abs(outer.Y - previousOuter.Y) > 1e-6)
                {
                    AddPlanLine(drawing, previousOuter, outsideCorner, edgeLayer);
                    previousOuter = outsideCorner;
                }

                AddPlanLine(drawing, previousOuter, outer, edgeLayer);
                if (index <= risers)
                {
                    AddPlanLine(drawing, pivot, outer, GetPlanLineLayer(elevation, parameters.PlanCutHeight));
                }

                int treadNumber = riserNumber - 1;
                if ((index > 1 || labelEntry) && (treadNumber & 1) != 0)
                {
                    Point2 offset = turn.GetOuterPoint(isUpperCorner, index - 0.5) * 0.74;
                    double halfSectorAngle = Math.PI / (4.0 * (risers + 1));
                    double clearance = offset.Length * Math.Sin(halfSectorAngle);
                    double labelHeight = Math.Min(textHeight, clearance);
                    AddRiserNumber(drawing, treadNumber, pivot + offset, labelHeight);
                }

                previousOuter = outer;
            }

            double cutIndex = (parameters.PlanCutHeight / parameters.RiserHeight) - previousRisers;
            if (cutIndex > 1e-6 && cutIndex <= risers)
            {
                Point2 ray = turn.GetOuterPoint(isUpperCorner, cutIndex);
                Point2 normal = new Point2(-ray.Y, ray.X).Normalize();
                Point2 tooth = normal * Math.Min(parameters.TreadDepth * 0.12, ray.Length * 0.035);
                drawing.Polylines.Add(new PolylinePrimitive(
                    new[]
                    {
                        pivot,
                        pivot + (ray * 0.42),
                        pivot + (ray * 0.47) + tooth,
                        pivot + (ray * 0.53) - tooth,
                        pivot + (ray * 0.58),
                        pivot + ray
                    },
                    false, StairLayers.PlanCut, drawOnTop: true));
            }
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
            StairPlanParameters parameters,
            double? flightWidth = null,
            bool labelLastRiser = true)
        {
            double width = flightWidth ?? parameters.StairWidth;
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
                start + (widthDirection * width),
                direction,
                run,
                secondSideVisibleRun);

            AddPlanLine(
                drawing,
                start,
                start + (widthDirection * width),
                GetPlanLineLayer(firstTreadElevation, parameters.PlanCutHeight));

            Point2 end = start + (direction * run);
            AddPlanLine(
                drawing,
                end,
                end + (widthDirection * width),
                GetPlanLineLayer(flightEndElevation, parameters.PlanCutHeight));

            for (int treadIndex = 1; treadIndex < treadCount; treadIndex++)
            {
                Point2 riserStart = start + (direction * (treadIndex * parameters.TreadDepth));
                double riserTopElevation = flightStartElevation + ((treadIndex + 1) * parameters.RiserHeight);
                AddPlanLine(
                    drawing,
                    riserStart,
                    riserStart + (widthDirection * width),
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
                    width);
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
                width,
                numberOffsetRatio,
                textHeight,
                parameters.TurnSteps.HasSteps,
                labelLastRiser);
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
            double textHeight,
            bool steppedTurn,
            bool labelLastRiser)
        {
            int lastRiserToLabel = labelLastRiser ? risers : risers - 1;
            double labelHeight = steppedTurn ? Math.Min(textHeight, treadDepth * 0.56) : textHeight;
            for (int localRiserIndex = 1; localRiserIndex <= lastRiserToLabel; localRiserIndex++)
            {
                int riserNumber = firstRiserNumber + localRiserIndex - 1;
                if ((riserNumber & 1) == 0)
                {
                    continue;
                }

                double distance = (localRiserIndex - 0.5) * treadDepth;
                if (!steppedTurn)
                {
                    distance = Math.Min(run - (treadDepth * 0.5), distance);
                }
                Point2 position = start
                    + (direction * distance)
                    + (widthDirection * (width * numberOffsetRatio));
                AddRiserNumber(drawing, riserNumber, position, labelHeight);
            }
        }

        private static void AddRiserNumber(DrawingModel drawing, int riserNumber, Point2 position, double height)
        {
            drawing.Texts.Add(new TextPrimitive(
                position, riserNumber.ToString(CultureInfo.InvariantCulture), height,
                0.0, StairLayers.PlanText, TextAnchor.MiddleCenter));
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

        private static double GetFlightRun(int risers, double treadDepth)
        {
            return Math.Max(1, risers - 1) * treadDepth;
        }
    }
}
