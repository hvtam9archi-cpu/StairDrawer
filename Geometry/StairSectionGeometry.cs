using System;
using System.Collections.Generic;
using StairDrawer.Models;

namespace StairDrawer.Geometry
{
    internal static class StairSectionGeometry
    {
        private const double NosingProjection = 20.0;
        private const double NosingChamfer = 5.0;
        private const double RiserFinishBottomClearance = 20.0;
        private const double IronHandrailDiameter = 40.0;
        private const double IronTerminalPostWidth = 40.0;
        private const double IronFirstPostClearance = 60.0;
        private const double IronMemberThickness = 20.0;
        private const double IronMainPostSpacing = 560.0;
        private const double IronIntermediatePostSpacing = 120.0;
        private const int IronIntermediatePostPatternCount = 4;
        private const double IronUpperRailClearance = 100.0;
        private const double IronLowerRailClearance = 100.0;
        private const double StairSupportWidth = 220.0;
        private const double StairSupportDepth = 350.0;
        private const double GlassBottomGap = 80.0;
        private const double GlassHandrailClearance = 50.0;
        private const int GlassPanelTreadSpan = 3;
        private const double PointTolerance = 1e-9;

        public static DrawingModel Create(StairSectionParameters parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            IReadOnlyList<FlightLayout> flights = CreateFlightLayouts(parameters);
            var drawing = new DrawingModel();

            FlightLayout cutFlight = flights[0];
            AddCutConcreteSlab(
                drawing,
                parameters,
                cutFlight,
                flights.Count == 1);
            AddCutBrickStepsAndFinishes(drawing, parameters, cutFlight);
            if (flights.Count > 1)
            {
                AddVisibleFlight(drawing, parameters, flights[1]);
                AddCutArrivalFloorAndBeam(drawing, parameters, flights[1]);
                AddVisibleFinishes(drawing, parameters, flights[1]);
            }

            if (parameters.DrawRailing)
            {
                if (parameters.RailingType == StairRailingType.Iron)
                {
                    AddIronContinuousHandrail(drawing, parameters, flights);
                }

                for (int flightIndex = flights.Count - 1; flightIndex >= 0; flightIndex--)
                {
                    int firstRailingPolylineIndex = drawing.Polylines.Count;
                    AddRailing(
                        drawing,
                        parameters,
                        flights[flightIndex],
                        flightIndex == 0,
                        flightIndex == flights.Count - 1);
                    if (flights.Count > 1
                        && flightIndex == 0
                        && parameters.RailingType == StairRailingType.Iron)
                    {
                        MarkRailingPolylinesAsForeground(
                            drawing,
                            firstRailingPolylineIndex);
                    }
                }

                if (flights.Count > 1 && parameters.RailingType == StairRailingType.Iron)
                {
                    AddIronFlightTransition(
                        drawing,
                        parameters,
                        flights[0],
                        flights[1]);
                }
            }

            return drawing;
        }

        private static IReadOnlyList<FlightLayout> CreateFlightLayouts(StairSectionParameters parameters)
        {
            var flights = new List<FlightLayout>();
            if (parameters.PlanType == StairPlanType.Straight)
            {
                flights.Add(new FlightLayout(
                    0.0,
                    0.0,
                    parameters.TotalRisers,
                    parameters.RiserHeight,
                    parameters.TreadDepth,
                    1.0));
                return flights;
            }

            int firstFlightRisers = parameters.FirstFlightRisers;
            int secondFlightRisers = parameters.SecondFlightRisers;
            var firstFlight = new FlightLayout(
                0.0,
                0.0,
                firstFlightRisers,
                parameters.RiserHeight,
                parameters.TreadDepth,
                1.0);
            flights.Add(firstFlight);
            double secondFlightDirection = parameters.PlanType == StairPlanType.UShape
                ? -1.0
                : 1.0;
            flights.Add(new FlightLayout(
                firstFlight.EndX,
                firstFlight.LandingElevation,
                secondFlightRisers,
                parameters.RiserHeight,
                parameters.TreadDepth,
                secondFlightDirection));

            return flights;
        }

        private static void AddCutConcreteSlab(
            DrawingModel drawing,
            StairSectionParameters parameters,
            FlightLayout flight,
            bool addArrivalBeam)
        {
            var vertices = new List<Point2>();
            AddVertexIfDistinct(vertices, new Point2(
                flight.StartX - parameters.LandingLength,
                flight.StartElevation));
            AddVertexIfDistinct(vertices, new Point2(flight.StartX, flight.StartElevation));
            AddVertexIfDistinct(vertices, new Point2(flight.EndX, flight.LastTreadElevation));
            AddVertexIfDistinct(vertices, new Point2(flight.EndX, flight.LandingElevation));
            double landingEndX = flight.EndX + parameters.LandingLength;
            double landingBottomElevation = flight.LandingElevation
                - parameters.WaistSlabThickness;
            AddVertexIfDistinct(vertices, new Point2(landingEndX, flight.LandingElevation));
            AddVertexIfDistinct(vertices, new Point2(landingEndX, landingBottomElevation));

            Point2 downNormal = new Point2(flight.InclinedRise, -flight.Run).Normalize();
            Point2 slabOffset = downNormal * parameters.WaistSlabThickness;
            Point2 inclinedSlabStart = new Point2(
                flight.StartX,
                flight.StartElevation) + slabOffset;
            Point2 inclinedSlabEnd = new Point2(flight.EndX, flight.LastTreadElevation) + slabOffset;
            double inclinedSlope = flight.InclinedRise / flight.Run;
            if (addArrivalBeam)
            {
                double beamFarX = flight.EndX + StairSupportWidth;
                double beamBottomElevation = flight.LandingElevation - StairSupportDepth;
                AddVertexIfDistinct(vertices, new Point2(beamFarX, landingBottomElevation));
                AddVertexIfDistinct(vertices, new Point2(beamFarX, beamBottomElevation));
                AddVertexIfDistinct(vertices, new Point2(flight.EndX, beamBottomElevation));
                AddVertexIfDistinct(vertices, new Point2(
                    flight.EndX,
                    GetInclinedUndersideElevation(
                        inclinedSlabStart,
                        inclinedSlope,
                        flight.EndX)));
            }
            else
            {
                double intersectionRun = (landingBottomElevation - inclinedSlabEnd.Y)
                    * flight.Run
                    / flight.InclinedRise;
                double landingIntersectionX = Math.Min(
                    landingEndX,
                    inclinedSlabEnd.X + intersectionRun);
                AddVertexIfDistinct(vertices, new Point2(
                    landingIntersectionX,
                    landingBottomElevation));
                AddVertexIfDistinct(vertices, inclinedSlabEnd);
            }

            double stairFootWidth = Math.Min(StairSupportWidth, flight.Run);
            double stairFootFarX = flight.StartX + stairFootWidth;
            AddVertexIfDistinct(vertices, new Point2(
                stairFootFarX,
                GetInclinedUndersideElevation(
                    inclinedSlabStart,
                    inclinedSlope,
                    stairFootFarX)));
            double stairFootBottomElevation = flight.StartElevation - StairSupportDepth;
            AddVertexIfDistinct(vertices, new Point2(
                stairFootFarX,
                stairFootBottomElevation));
            AddVertexIfDistinct(vertices, new Point2(
                flight.StartX,
                stairFootBottomElevation));
            AddVertexIfDistinct(vertices, new Point2(
                flight.StartX,
                flight.StartElevation - parameters.WaistSlabThickness));
            AddVertexIfDistinct(vertices, new Point2(
                flight.StartX - parameters.LandingLength,
                flight.StartElevation - parameters.WaistSlabThickness));

            drawing.Polylines.Add(new PolylinePrimitive(
                vertices,
                true,
                StairLayers.Concrete));
        }

        private static double GetInclinedUndersideElevation(
            Point2 inclinedSlabStart,
            double inclinedSlope,
            double x)
        {
            return inclinedSlabStart.Y
                + (inclinedSlope * (x - inclinedSlabStart.X));
        }

        private static void AddCutBrickStepsAndFinishes(
            DrawingModel drawing,
            StairSectionParameters parameters,
            FlightLayout flight)
        {
            AddHorizontalFinish(
                drawing,
                flight.StartX - parameters.LandingLength,
                flight.StartX,
                flight.StartElevation,
                parameters.FinishThickness);

            for (int treadIndex = 0; treadIndex < flight.TreadCount; treadIndex++)
            {
                double x0 = flight.StartX + (treadIndex * parameters.TreadDepth);
                double x1 = x0 + parameters.TreadDepth;
                double y0 = flight.StartElevation + (treadIndex * parameters.RiserHeight);
                double y1 = y0 + parameters.RiserHeight;

                drawing.Polylines.Add(new PolylinePrimitive(
                    new[]
                    {
                        new Point2(x0, y0),
                        new Point2(x0, y1),
                        new Point2(x1, y1)
                    },
                    true,
                    StairLayers.Brick));

                AddTreadFinish(
                    drawing,
                    x0,
                    x1,
                    y1,
                    parameters.FinishThickness,
                    1.0);
                AddRiserFinish(
                    drawing,
                    x0,
                    y0,
                    y1,
                    parameters.FinishThickness,
                    1.0);
            }

            AddRiserFinish(
                drawing,
                flight.EndX,
                flight.LastTreadElevation,
                flight.LandingElevation,
                parameters.FinishThickness,
                1.0);
            AddTreadFinish(
                drawing,
                flight.EndX,
                flight.EndX + parameters.LandingLength,
                flight.LandingElevation,
                parameters.FinishThickness,
                1.0);
        }

        private static void AddVisibleFlight(
            DrawingModel drawing,
            StairSectionParameters parameters,
            FlightLayout flight)
        {
            var stepVertices = new List<Point2>
            {
                new Point2(flight.StartX, flight.StartElevation)
            };
            double currentX = flight.StartX;
            double currentElevation = flight.StartElevation;
            for (int treadIndex = 0; treadIndex < flight.TreadCount; treadIndex++)
            {
                currentElevation += parameters.RiserHeight;
                stepVertices.Add(new Point2(currentX, currentElevation));
                currentX += flight.Direction * parameters.TreadDepth;
                stepVertices.Add(new Point2(currentX, currentElevation));
            }

            stepVertices.Add(new Point2(flight.EndX, flight.LandingElevation));
            drawing.Polylines.Add(new PolylinePrimitive(
                stepVertices,
                false,
                StairLayers.SectionVisible));

            double lowerStartElevation = flight.StartElevation - parameters.WaistSlabThickness;
            double lowerEndElevation = flight.LastTreadElevation - parameters.WaistSlabThickness;
            drawing.Lines.Add(new LinePrimitive(
                new Point2(flight.StartX, lowerStartElevation),
                new Point2(flight.EndX, lowerEndElevation),
                StairLayers.SectionVisible));
            drawing.Lines.Add(new LinePrimitive(
                new Point2(flight.StartX, flight.StartElevation),
                new Point2(flight.StartX, lowerStartElevation),
                StairLayers.SectionVisible));
        }

        private static void AddCutArrivalFloorAndBeam(
            DrawingModel drawing,
            StairSectionParameters parameters,
            FlightLayout flight)
        {
            double landingBottomElevation = flight.LandingElevation
                - parameters.WaistSlabThickness;
            double landingEndX = flight.EndX
                + (flight.Direction * parameters.LandingLength);
            double beamFarX = flight.EndX
                + (flight.Direction * StairSupportWidth);
            double beamBottomElevation = flight.LandingElevation
                - StairSupportDepth;
            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    new Point2(flight.EndX, flight.LandingElevation),
                    new Point2(landingEndX, flight.LandingElevation),
                    new Point2(landingEndX, landingBottomElevation),
                    new Point2(beamFarX, landingBottomElevation),
                    new Point2(beamFarX, beamBottomElevation),
                    new Point2(flight.EndX, beamBottomElevation)
                },
                true,
                StairLayers.Concrete));
        }

        private static void AddVisibleFinishes(
            DrawingModel drawing,
            StairSectionParameters parameters,
            FlightLayout flight)
        {
            for (int treadIndex = 0; treadIndex < flight.TreadCount; treadIndex++)
            {
                double x0 = flight.StartX
                    + (flight.Direction * treadIndex * parameters.TreadDepth);
                double x1 = x0 + (flight.Direction * parameters.TreadDepth);
                double y0 = flight.StartElevation + (treadIndex * parameters.RiserHeight);
                double y1 = y0 + parameters.RiserHeight;
                AddTreadFinish(
                    drawing,
                    x0,
                    x1,
                    y1,
                    parameters.FinishThickness,
                    flight.Direction);
                AddRiserFinish(
                    drawing,
                    x0,
                    y0,
                    y1,
                    parameters.FinishThickness,
                    flight.Direction);
            }

            AddRiserFinish(
                drawing,
                flight.EndX,
                flight.LastTreadElevation,
                flight.LandingElevation,
                parameters.FinishThickness,
                flight.Direction);
            AddHorizontalFinish(
                drawing,
                flight.EndX,
                flight.EndX + (flight.Direction * parameters.LandingLength),
                flight.LandingElevation,
                parameters.FinishThickness);
        }

        private static void AddTreadFinish(
            DrawingModel drawing,
            double riserX,
            double treadEndX,
            double treadElevation,
            double finishThickness,
            double direction)
        {
            double finishedRiserFaceX = riserX - (direction * finishThickness);
            double nosingFrontX = finishedRiserFaceX - (direction * NosingProjection);
            double topElevation = treadElevation + finishThickness;
            double chamfer = Math.Min(NosingChamfer, finishThickness);
            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    new Point2(nosingFrontX, treadElevation),
                    new Point2(nosingFrontX, topElevation - chamfer),
                    new Point2(nosingFrontX + (direction * chamfer), topElevation),
                    new Point2(treadEndX, topElevation),
                    new Point2(treadEndX, treadElevation)
                },
                true,
                StairLayers.Finish));
        }

        private static void AddRiserFinish(
            DrawingModel drawing,
            double riserX,
            double bottomElevation,
            double topElevation,
            double finishThickness,
            double direction)
        {
            double riserHeight = topElevation - bottomElevation;
            double bottomClearance = Math.Min(
                RiserFinishBottomClearance,
                riserHeight * 0.5);
            double shortenedBottomElevation = bottomElevation + bottomClearance;
            double finishedRiserFaceX = riserX - (direction * finishThickness);
            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    new Point2(riserX, shortenedBottomElevation),
                    new Point2(riserX, topElevation),
                    new Point2(finishedRiserFaceX, topElevation),
                    new Point2(finishedRiserFaceX, shortenedBottomElevation)
                },
                true,
                StairLayers.Finish,
                drawOnTop: true));
        }

        private static void AddHorizontalFinish(
            DrawingModel drawing,
            double startX,
            double endX,
            double elevation,
            double finishThickness)
        {
            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    new Point2(startX, elevation),
                    new Point2(endX, elevation),
                    new Point2(endX, elevation + finishThickness),
                    new Point2(startX, elevation + finishThickness)
                },
                true,
                StairLayers.Finish));
        }

        private static void AddRailing(
            DrawingModel drawing,
            StairSectionParameters parameters,
            FlightLayout flight,
            bool includeStartPost,
            bool includeEndPost)
        {
            Point2 firstBase = GetRailingBasePoint(flight, parameters, 0);
            Point2 landingBase = GetRailingBasePoint(flight, parameters, flight.TreadCount);

            if (parameters.RailingType == StairRailingType.Iron)
            {
                AddIronRailing(
                    drawing,
                    parameters,
                    flight,
                    firstBase,
                    includeStartPost,
                    includeEndPost);
                return;
            }

            Point2 verticalOffset = new Point2(0.0, parameters.RailingHeight);
            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    firstBase + verticalOffset,
                    landingBase + verticalOffset
                },
                false,
                StairLayers.Railing,
                drawOnTop: true));
            AddGlassRailing(drawing, parameters, flight);
        }

        private static void MarkRailingPolylinesAsForeground(
            DrawingModel drawing,
            int firstPolylineIndex)
        {
            for (int polylineIndex = firstPolylineIndex;
                polylineIndex < drawing.Polylines.Count;
                polylineIndex++)
            {
                PolylinePrimitive primitive = drawing.Polylines[polylineIndex];
                if (!primitive.DrawOnTop
                    || !primitive.Closed
                    || primitive.Vertices.Count < 3)
                {
                    continue;
                }

                drawing.Wipeouts.Add(new WipeoutPrimitive(
                    primitive.Vertices,
                    primitive.LayerName));
                drawing.Polylines[polylineIndex] = new PolylinePrimitive(
                    primitive.Vertices,
                    primitive.Closed,
                    primitive.LayerName,
                    primitive.GlobalWidth,
                    primitive.LinetypeScale,
                    true,
                    true);
            }
        }

        private static void AddIronRailing(
            DrawingModel drawing,
            StairSectionParameters parameters,
            FlightLayout flight,
            Point2 firstBase,
            bool includeStartPost,
            bool includeEndPost)
        {
            double flightSlope = flight.InclinedRise / flight.Run;
            double startExtension = includeStartPost
                ? IronFirstPostClearance + (IronTerminalPostWidth * 0.5)
                : 0.0;
            double railingStartX = flight.StartX - startExtension;
            AddIronPosts(
                drawing,
                railingStartX,
                flight.EndX,
                progress => progress <= startExtension
                    ? flight.StartElevation + parameters.FinishThickness
                    : GetFlightSurfaceElevation(
                        flight,
                        parameters,
                        progress - startExtension),
                progress => firstBase.Y
                    + (flightSlope * (progress - startExtension)),
                flightSlope,
                parameters.RailingHeight,
                includeStartPost,
                includeEndPost);
        }

        private static void AddIronPosts(
            DrawingModel drawing,
            double startX,
            double endX,
            Func<double, double> getSurfaceElevation,
            Func<double, double> getReferenceElevation,
            double slope,
            double railingHeight,
            bool includeStartPost,
            bool includeEndPost)
        {
            double horizontalLength = Math.Abs(endX - startX);
            if (horizontalLength <= PointTolerance)
            {
                return;
            }

            double direction = Math.Sign(endX - startX);
            double worldSlope = slope * direction;
            int mainPostBayCount = Math.Max(
                1,
                (int)Math.Ceiling(horizontalLength / IronMainPostSpacing));
            double slopeThicknessFactor = Math.Sqrt(1.0 + (slope * slope));
            double handrailVerticalHalfThickness = (IronHandrailDiameter * 0.5)
                * slopeThicknessFactor;
            double memberVerticalHalfThickness = (IronMemberThickness * 0.5)
                * slopeThicknessFactor;
            double upperRailCenterOffset = railingHeight
                - (IronHandrailDiameter * 0.5)
                - IronUpperRailClearance
                - (IronMemberThickness * 0.5);
            double lowerRailCenterOffset = IronLowerRailClearance
                + (IronMemberThickness * 0.5);

            for (int bayIndex = 0; bayIndex < mainPostBayCount; bayIndex++)
            {
                double bayStart = bayIndex * IronMainPostSpacing;
                double bayEnd = Math.Min(
                    horizontalLength,
                    bayStart + IronMainPostSpacing);
                if (bayIndex > 0 || includeStartPost)
                {
                    AddIronMainPost(
                        drawing,
                        startX + (direction * bayStart),
                        getSurfaceElevation(bayStart),
                        getReferenceElevation(bayStart),
                        worldSlope,
                        railingHeight,
                        handrailVerticalHalfThickness,
                        bayIndex == 0 && includeStartPost
                            ? IronTerminalPostWidth
                            : IronMemberThickness);
                }

                double bayLength = bayEnd - bayStart;
                int intermediatePostCount = Math.Min(
                    IronIntermediatePostPatternCount,
                    Math.Max(
                        0,
                        (int)Math.Round(
                            bayLength / IronIntermediatePostSpacing,
                            MidpointRounding.AwayFromZero) - 1));
                double firstIntermediateOffset = intermediatePostCount == 0
                    ? 0.0
                    : (bayLength
                        - ((intermediatePostCount - 1) * IronIntermediatePostSpacing)) * 0.5;
                for (int intermediateIndex = 0;
                    intermediateIndex < intermediatePostCount;
                    intermediateIndex++)
                {
                    double progress = bayStart
                        + firstIntermediateOffset
                        + (intermediateIndex * IronIntermediatePostSpacing);
                    double referenceElevation = getReferenceElevation(progress);
                    AddIronIntermediatePost(
                        drawing,
                        startX + (direction * progress),
                        referenceElevation,
                        worldSlope,
                        lowerRailCenterOffset,
                        upperRailCenterOffset,
                        memberVerticalHalfThickness);
                }

                double startPostWidth = bayIndex == 0 && includeStartPost
                    ? IronTerminalPostWidth
                    : IronMemberThickness;
                double endPostWidth = bayIndex == mainPostBayCount - 1 && includeEndPost
                    ? IronTerminalPostWidth
                    : IronMemberThickness;
                double railStart = bayStart + (startPostWidth * 0.5);
                double railEnd = bayEnd - (endPostWidth * 0.5);
                if (railEnd - railStart > PointTolerance)
                {
                    Point2 upperRailStart = new Point2(
                        startX + (direction * railStart),
                        getReferenceElevation(railStart) + upperRailCenterOffset);
                    Point2 upperRailEnd = new Point2(
                        startX + (direction * railEnd),
                        getReferenceElevation(railEnd) + upperRailCenterOffset);
                    AddIronRailSegmentBetweenPosts(
                        drawing,
                        upperRailStart,
                        upperRailEnd,
                        IronMemberThickness);

                    Point2 lowerRailStart = new Point2(
                        startX + (direction * railStart),
                        getReferenceElevation(railStart) + lowerRailCenterOffset);
                    Point2 lowerRailEnd = new Point2(
                        startX + (direction * railEnd),
                        getReferenceElevation(railEnd) + lowerRailCenterOffset);
                    AddIronRailSegmentBetweenPosts(
                        drawing,
                        lowerRailStart,
                        lowerRailEnd,
                        IronMemberThickness);
                }
            }

            if (includeEndPost)
            {
                AddIronMainPost(
                    drawing,
                    endX,
                    getSurfaceElevation(horizontalLength),
                    getReferenceElevation(horizontalLength),
                    worldSlope,
                    railingHeight,
                    handrailVerticalHalfThickness,
                    IronTerminalPostWidth);
            }
        }

        private static void AddIronMainPost(
            DrawingModel drawing,
            double centerX,
            double bottomElevation,
            double referenceElevation,
            double worldSlope,
            double railingHeight,
            double handrailVerticalHalfThickness,
            double visibleWidth)
        {
            double halfWidth = visibleWidth * 0.5;
            double leftX = centerX - halfWidth;
            double rightX = centerX + halfWidth;
            AddIronBoxPost(
                drawing,
                leftX,
                bottomElevation,
                rightX,
                bottomElevation,
                referenceElevation
                    + (worldSlope * (rightX - centerX))
                    + railingHeight
                    - handrailVerticalHalfThickness,
                referenceElevation
                    + (worldSlope * (leftX - centerX))
                    + railingHeight
                    - handrailVerticalHalfThickness,
                visibleWidth < IronTerminalPostWidth - PointTolerance);
        }

        private static void AddIronIntermediatePost(
            DrawingModel drawing,
            double centerX,
            double referenceElevation,
            double worldSlope,
            double lowerRailCenterOffset,
            double upperRailCenterOffset,
            double memberVerticalHalfThickness)
        {
            double halfWidth = IronMemberThickness * 0.5;
            double leftX = centerX - halfWidth;
            double rightX = centerX + halfWidth;
            double leftReferenceElevation = referenceElevation
                + (worldSlope * (leftX - centerX));
            double rightReferenceElevation = referenceElevation
                + (worldSlope * (rightX - centerX));
            AddIronBoxPost(
                drawing,
                leftX,
                leftReferenceElevation + lowerRailCenterOffset + memberVerticalHalfThickness,
                rightX,
                rightReferenceElevation + lowerRailCenterOffset + memberVerticalHalfThickness,
                rightReferenceElevation + upperRailCenterOffset - memberVerticalHalfThickness,
                leftReferenceElevation + upperRailCenterOffset - memberVerticalHalfThickness,
                true);
        }

        private static void AddIronBoxPost(
            DrawingModel drawing,
            double leftX,
            double bottomLeftElevation,
            double rightX,
            double bottomRightElevation,
            double topRightElevation,
            double topLeftElevation,
            bool closeTop)
        {
            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    new Point2(leftX, topLeftElevation),
                    new Point2(leftX, bottomLeftElevation),
                    new Point2(rightX, bottomRightElevation),
                    new Point2(rightX, topRightElevation)
                },
                closeTop,
                StairLayers.Railing,
                drawOnTop: true));
        }

        private static void AddIronRailSegmentBetweenPosts(
            DrawingModel drawing,
            Point2 startCenter,
            Point2 endCenter,
            double thickness)
        {
            double horizontalLength = endCenter.X - startCenter.X;
            if (Math.Abs(horizontalLength) <= PointTolerance)
            {
                return;
            }

            double slope = (endCenter.Y - startCenter.Y) / horizontalLength;
            double verticalHalfThickness = (thickness * 0.5)
                * Math.Sqrt(1.0 + (slope * slope));
            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    new Point2(startCenter.X, startCenter.Y + verticalHalfThickness),
                    new Point2(endCenter.X, endCenter.Y + verticalHalfThickness),
                    new Point2(endCenter.X, endCenter.Y - verticalHalfThickness),
                    new Point2(startCenter.X, startCenter.Y - verticalHalfThickness)
                },
                true,
                StairLayers.Railing,
                drawOnTop: true));
        }

        private static void AddIronContinuousHandrail(
            DrawingModel drawing,
            StairSectionParameters parameters,
            IReadOnlyList<FlightLayout> flights)
        {
            var centerline = new List<Point2>();
            for (int flightIndex = 0; flightIndex < flights.Count; flightIndex++)
            {
                FlightLayout flight = flights[flightIndex];
                Point2 firstBase = GetRailingBasePoint(flight, parameters, 0);
                Point2 landingBase = GetRailingBasePoint(
                    flight,
                    parameters,
                    flight.TreadCount);
                if (flightIndex == 0)
                {
                    double flightSlope = flight.InclinedRise / flight.Run;
                    double handrailStartExtension = IronFirstPostClearance
                        + IronTerminalPostWidth
                        + (IronHandrailDiameter * 0.5);
                    centerline.Add(new Point2(
                        flight.StartX - handrailStartExtension,
                        firstBase.Y
                            - (flightSlope * handrailStartExtension)
                            + parameters.RailingHeight));
                }
                else
                {
                    centerline.Add(firstBase + new Point2(0.0, parameters.RailingHeight));
                }

                if (flightIndex == flights.Count - 1)
                {
                    double flightSlope = flight.InclinedRise / flight.Run;
                    double handrailEndExtension = (IronTerminalPostWidth * 0.5)
                        + (IronHandrailDiameter * 0.5);
                    centerline.Add(new Point2(
                        flight.EndX + (flight.Direction * handrailEndExtension),
                        landingBase.Y
                            + (flightSlope * handrailEndExtension)
                            + parameters.RailingHeight));
                }
                else
                {
                    centerline.Add(landingBase + new Point2(0.0, parameters.RailingHeight));
                }
            }

            AddContinuousMemberOutline(
                drawing,
                centerline,
                IronHandrailDiameter);
        }

        private static void AddContinuousMemberOutline(
            DrawingModel drawing,
            IReadOnlyList<Point2> centerline,
            double thickness)
        {
            if (centerline == null)
            {
                throw new ArgumentNullException(nameof(centerline));
            }

            if (centerline.Count < 2)
            {
                throw new ArgumentException(
                    "A continuous member requires at least two centerline points.",
                    nameof(centerline));
            }

            double halfThickness = thickness * 0.5;
            var firstBoundary = new List<Point2>(centerline.Count);
            var secondBoundary = new List<Point2>(centerline.Count);
            for (int pointIndex = 0; pointIndex < centerline.Count; pointIndex++)
            {
                if (pointIndex == 0)
                {
                    Point2 direction = (centerline[1] - centerline[0]).Normalize();
                    Point2 normal = new Point2(-direction.Y, direction.X) * halfThickness;
                    firstBoundary.Add(centerline[0] + normal);
                    secondBoundary.Add(centerline[0] - normal);
                    continue;
                }

                if (pointIndex == centerline.Count - 1)
                {
                    Point2 direction = (centerline[pointIndex]
                        - centerline[pointIndex - 1]).Normalize();
                    Point2 normal = new Point2(-direction.Y, direction.X) * halfThickness;
                    firstBoundary.Add(centerline[pointIndex] + normal);
                    secondBoundary.Add(centerline[pointIndex] - normal);
                    continue;
                }

                firstBoundary.Add(GetOffsetJoint(
                    centerline[pointIndex - 1],
                    centerline[pointIndex],
                    centerline[pointIndex + 1],
                    halfThickness));
                secondBoundary.Add(GetOffsetJoint(
                    centerline[pointIndex - 1],
                    centerline[pointIndex],
                    centerline[pointIndex + 1],
                    -halfThickness));
            }

            var outline = new List<Point2>(centerline.Count * 2);
            outline.AddRange(firstBoundary);
            for (int pointIndex = secondBoundary.Count - 1; pointIndex >= 0; pointIndex--)
            {
                AddVertexIfDistinct(outline, secondBoundary[pointIndex]);
            }

            drawing.Polylines.Add(new PolylinePrimitive(
                outline,
                true,
                StairLayers.Railing,
                drawOnTop: true));
        }

        private static Point2 GetOffsetJoint(
            Point2 previousPoint,
            Point2 jointPoint,
            Point2 nextPoint,
            double offset)
        {
            Point2 previousDirection = (jointPoint - previousPoint).Normalize();
            Point2 nextDirection = (nextPoint - jointPoint).Normalize();
            Point2 previousNormal = new Point2(
                -previousDirection.Y,
                previousDirection.X) * offset;
            Point2 nextNormal = new Point2(
                -nextDirection.Y,
                nextDirection.X) * offset;
            Point2 firstOffsetPoint = jointPoint + previousNormal;
            Point2 secondOffsetPoint = jointPoint + nextNormal;
            double cross = Cross(previousDirection, nextDirection);
            if (Math.Abs(cross) <= PointTolerance)
            {
                return jointPoint + ((previousNormal + nextNormal) * 0.5);
            }

            double firstLineParameter = Cross(
                secondOffsetPoint - firstOffsetPoint,
                nextDirection) / cross;
            Point2 intersection = firstOffsetPoint
                + (previousDirection * firstLineParameter);
            double maximumMiterLength = Math.Abs(offset) * 5.0;
            Point2 miter = intersection - jointPoint;
            if (miter.Length <= maximumMiterLength)
            {
                return intersection;
            }

            Point2 averageNormal = (previousNormal + nextNormal) * 0.5;
            return jointPoint + averageNormal;
        }

        private static double Cross(Point2 first, Point2 second)
        {
            return (first.X * second.Y) - (first.Y * second.X);
        }

        private static void AddIronFlightTransition(
            DrawingModel drawing,
            StairSectionParameters parameters,
            FlightLayout lowerFlight,
            FlightLayout upperFlight)
        {
            Point2 upperFirstBase = GetRailingBasePoint(upperFlight, parameters, 0);
            double upperWorldSlope = (upperFlight.InclinedRise / upperFlight.Run)
                * upperFlight.Direction;
            double handrailVerticalHalfThickness = (IronHandrailDiameter * 0.5)
                * Math.Sqrt(1.0 + (upperWorldSlope * upperWorldSlope));
            AddIronMainPost(
                drawing,
                lowerFlight.EndX,
                lowerFlight.LandingElevation + parameters.FinishThickness,
                upperFirstBase.Y,
                upperWorldSlope,
                parameters.RailingHeight,
                handrailVerticalHalfThickness,
                IronMemberThickness);
        }

        private static double GetFlightSurfaceElevation(
            FlightLayout flight,
            StairSectionParameters parameters,
            double progress)
        {
            if (progress >= flight.Run - PointTolerance)
            {
                return flight.LandingElevation + parameters.FinishThickness;
            }

            int treadIndex = Math.Min(
                flight.TreadCount - 1,
                (int)Math.Floor((progress + PointTolerance) / parameters.TreadDepth));
            return flight.StartElevation
                + ((treadIndex + 1) * parameters.RiserHeight)
                + parameters.FinishThickness;
        }

        private static void AddGlassRailing(
            DrawingModel drawing,
            StairSectionParameters parameters,
            FlightLayout flight)
        {
            double panelTopHeight = parameters.RailingHeight - GlassHandrailClearance;
            int panelStartIndex = 0;
            while (panelStartIndex < flight.TreadCount)
            {
                int panelEndIndex = Math.Min(
                    flight.TreadCount,
                    panelStartIndex + GlassPanelTreadSpan);
                Point2 panelStartBase = GetRailingBasePoint(
                    flight,
                    parameters,
                    panelStartIndex);
                Point2 panelEndBase = GetRailingBasePoint(
                    flight,
                    parameters,
                    panelEndIndex);
                AddGlassPanel(
                    drawing,
                    panelStartBase,
                    panelEndBase,
                    panelTopHeight);
                AddRailingPost(drawing, panelStartBase, parameters.RailingHeight);
                panelStartIndex = panelEndIndex;
            }

            Point2 landingBase = GetRailingBasePoint(flight, parameters, flight.TreadCount);
            AddRailingPost(drawing, landingBase, parameters.RailingHeight);
        }

        private static void AddGlassPanel(
            DrawingModel drawing,
            Point2 startBase,
            Point2 endBase,
            double panelTopHeight)
        {
            Point2 bottomOffset = new Point2(0.0, GlassBottomGap);
            Point2 topOffset = new Point2(0.0, panelTopHeight);
            Point2 bottomStart = startBase + bottomOffset;
            Point2 bottomEnd = endBase + bottomOffset;
            Point2 topEnd = endBase + topOffset;
            Point2 topStart = startBase + topOffset;
            drawing.Polylines.Add(new PolylinePrimitive(
                new[] { bottomStart, bottomEnd, topEnd, topStart },
                true,
                StairLayers.RailingGlass,
                drawOnTop: true));
            drawing.Lines.Add(new LinePrimitive(
                bottomStart,
                topEnd,
                StairLayers.RailingGlass));
        }

        private static void AddRailingPost(
            DrawingModel drawing,
            Point2 postBase,
            double railingHeight)
        {
            drawing.Lines.Add(new LinePrimitive(
                postBase,
                postBase + new Point2(0.0, railingHeight),
                StairLayers.Railing));
        }

        private static Point2 GetRailingBasePoint(
            FlightLayout flight,
            StairSectionParameters parameters,
            int treadIndex)
        {
            return new Point2(
                flight.StartX + (flight.Direction * treadIndex * parameters.TreadDepth),
                flight.StartElevation
                    + ((treadIndex + 1) * parameters.RiserHeight)
                    + parameters.FinishThickness);
        }

        private static void AddVertexIfDistinct(IList<Point2> vertices, Point2 point)
        {
            if (vertices.Count == 0)
            {
                vertices.Add(point);
                return;
            }

            Point2 lastPoint = vertices[vertices.Count - 1];
            if ((point - lastPoint).Length > PointTolerance)
            {
                vertices.Add(point);
            }
        }

        private sealed class FlightLayout
        {
            public FlightLayout(
                double startX,
                double startElevation,
                int risers,
                double riserHeight,
                double treadDepth,
                double direction)
            {
                if (risers < 2)
                {
                    throw new ArgumentOutOfRangeException(nameof(risers));
                }

                StartX = startX;
                StartElevation = startElevation;
                Direction = direction;
                TreadCount = risers - 1;
                Run = TreadCount * treadDepth;
                InclinedRise = TreadCount * riserHeight;
                EndX = StartX + (Direction * Run);
                LastTreadElevation = StartElevation + InclinedRise;
                LandingElevation = StartElevation + (risers * riserHeight);
            }

            public double StartX { get; }

            public double StartElevation { get; }

            public double Direction { get; }

            public int TreadCount { get; }

            public double Run { get; }

            public double InclinedRise { get; }

            public double EndX { get; }

            public double LastTreadElevation { get; }

            public double LandingElevation { get; }
        }
    }
}
