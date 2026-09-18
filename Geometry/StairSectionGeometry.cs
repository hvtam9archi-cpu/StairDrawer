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
            AddCutConcreteSlab(drawing, parameters, cutFlight);
            AddCutBrickStepsAndFinishes(drawing, parameters, cutFlight);
            if (flights.Count > 1)
            {
                AddVisibleFlight(drawing, parameters, flights[1]);
                AddVisibleFinishes(drawing, parameters, flights[1]);
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
            FlightLayout flight)
        {
            var vertices = new List<Point2>();
            AddVertexIfDistinct(vertices, new Point2(
                flight.StartX - parameters.LandingLength,
                flight.StartElevation));
            AddVertexIfDistinct(vertices, new Point2(flight.StartX, flight.StartElevation));
            AddVertexIfDistinct(vertices, new Point2(flight.EndX, flight.LastTreadElevation));
            AddVertexIfDistinct(vertices, new Point2(flight.EndX, flight.LandingElevation));
            AddVertexIfDistinct(vertices, new Point2(
                flight.EndX + parameters.LandingLength,
                flight.LandingElevation));
            AddVertexIfDistinct(vertices, new Point2(
                flight.EndX + parameters.LandingLength,
                flight.LandingElevation - parameters.WaistSlabThickness));

            Point2 downNormal = new Point2(flight.InclinedRise, -flight.Run).Normalize();
            Point2 slabOffset = downNormal * parameters.WaistSlabThickness;
            Point2 inclinedSlabEnd = new Point2(flight.EndX, flight.LastTreadElevation) + slabOffset;
            double landingBottomElevation = flight.LandingElevation - parameters.WaistSlabThickness;
            double intersectionRun = (landingBottomElevation - inclinedSlabEnd.Y)
                * flight.Run
                / flight.InclinedRise;
            double landingIntersectionX = Math.Min(
                flight.EndX + parameters.LandingLength,
                inclinedSlabEnd.X + intersectionRun);
            Point2 landingIntersection = new Point2(
                landingIntersectionX,
                landingBottomElevation);
            AddVertexIfDistinct(vertices, landingIntersection);
            AddVertexIfDistinct(
                vertices,
                inclinedSlabEnd);
            AddVertexIfDistinct(
                vertices,
                new Point2(flight.StartX, flight.StartElevation) + slabOffset);
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
            stepVertices.Add(new Point2(
                flight.EndX + (flight.Direction * parameters.LandingLength),
                flight.LandingElevation));
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

            Point2 upperLandingEnd = new Point2(
                flight.EndX + (flight.Direction * parameters.LandingLength),
                flight.LandingElevation);
            Point2 upperLandingBottomEnd = new Point2(
                upperLandingEnd.X,
                upperLandingEnd.Y - parameters.WaistSlabThickness);
            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    new Point2(flight.EndX, lowerEndElevation),
                    new Point2(flight.EndX, flight.LandingElevation - parameters.WaistSlabThickness),
                    upperLandingBottomEnd,
                    upperLandingEnd
                },
                false,
                StairLayers.SectionVisible));
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
