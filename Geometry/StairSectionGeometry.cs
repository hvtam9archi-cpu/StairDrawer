using System;
using System.Collections.Generic;
using System.Globalization;
using StairDrawer.Models;

namespace StairDrawer.Geometry
{
    internal static class StairSectionGeometry
    {
        private const double NosingProjection = 20.0;
        private const double PointTolerance = 1e-9;

        public static DrawingModel Create(StairSectionParameters parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            IReadOnlyList<FlightLayout> flights = CreateFlightLayouts(parameters);
            var drawing = new DrawingModel();
            double textHeight = Math.Max(100.0, parameters.TreadDepth * 0.45);

            AddConcreteSlab(drawing, parameters, flights);
            AddBrickStepsAndFinishes(drawing, parameters, flights);
            AddAnnotations(drawing, parameters, flights, textHeight);

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
                    parameters.TreadDepth));
                return flights;
            }

            int firstFlightRisers = parameters.TotalRisers / 2;
            int secondFlightRisers = parameters.TotalRisers - firstFlightRisers;
            var firstFlight = new FlightLayout(
                0.0,
                0.0,
                firstFlightRisers,
                parameters.RiserHeight,
                parameters.TreadDepth);
            flights.Add(firstFlight);
            flights.Add(new FlightLayout(
                firstFlight.EndX + parameters.LandingLength,
                firstFlight.LandingElevation,
                secondFlightRisers,
                parameters.RiserHeight,
                parameters.TreadDepth));

            return flights;
        }

        private static void AddConcreteSlab(
            DrawingModel drawing,
            StairSectionParameters parameters,
            IReadOnlyList<FlightLayout> flights)
        {
            var vertices = new List<Point2>();
            AddVertexIfDistinct(vertices, new Point2(-parameters.LandingLength, 0.0));

            foreach (FlightLayout flight in flights)
            {
                AddVertexIfDistinct(vertices, new Point2(flight.StartX, flight.StartElevation));
                AddVertexIfDistinct(vertices, new Point2(flight.EndX, flight.LastTreadElevation));
                AddVertexIfDistinct(vertices, new Point2(flight.EndX, flight.LandingElevation));
                AddVertexIfDistinct(vertices, new Point2(
                    flight.EndX + parameters.LandingLength,
                    flight.LandingElevation));
            }

            FlightLayout lastFlight = flights[flights.Count - 1];
            AddVertexIfDistinct(vertices, new Point2(
                lastFlight.EndX + parameters.LandingLength,
                lastFlight.LandingElevation - parameters.WaistSlabThickness));

            for (int flightIndex = flights.Count - 1; flightIndex >= 0; flightIndex--)
            {
                FlightLayout flight = flights[flightIndex];
                AddVertexIfDistinct(vertices, new Point2(
                    flight.EndX,
                    flight.LandingElevation - parameters.WaistSlabThickness));

                Point2 downNormal = new Point2(flight.InclinedRise, -flight.Run).Normalize();
                Point2 slabOffset = downNormal * parameters.WaistSlabThickness;
                AddVertexIfDistinct(
                    vertices,
                    new Point2(flight.EndX, flight.LastTreadElevation) + slabOffset);
                AddVertexIfDistinct(
                    vertices,
                    new Point2(flight.StartX, flight.StartElevation) + slabOffset);
                AddVertexIfDistinct(vertices, new Point2(
                    flight.StartX,
                    flight.StartElevation - parameters.WaistSlabThickness));

                if (flightIndex > 0)
                {
                    FlightLayout previousFlight = flights[flightIndex - 1];
                    AddVertexIfDistinct(vertices, new Point2(
                        previousFlight.EndX,
                        flight.StartElevation - parameters.WaistSlabThickness));
                }
            }

            AddVertexIfDistinct(vertices, new Point2(
                -parameters.LandingLength,
                -parameters.WaistSlabThickness));

            drawing.Polylines.Add(new PolylinePrimitive(
                vertices,
                true,
                StairLayers.Concrete));
        }

        private static void AddBrickStepsAndFinishes(
            DrawingModel drawing,
            StairSectionParameters parameters,
            IReadOnlyList<FlightLayout> flights)
        {
            AddHorizontalFinish(
                drawing,
                -parameters.LandingLength,
                0.0,
                0.0,
                parameters.FinishThickness);

            foreach (FlightLayout flight in flights)
            {
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
                        parameters.FinishThickness);
                    AddRiserFinish(
                        drawing,
                        x0,
                        y0,
                        y1,
                        parameters.FinishThickness);
                }

                AddRiserFinish(
                    drawing,
                    flight.EndX,
                    flight.LastTreadElevation,
                    flight.LandingElevation,
                    parameters.FinishThickness);
                AddTreadFinish(
                    drawing,
                    flight.EndX,
                    flight.EndX + parameters.LandingLength,
                    flight.LandingElevation,
                    parameters.FinishThickness);
            }
        }

        private static void AddTreadFinish(
            DrawingModel drawing,
            double riserX,
            double treadEndX,
            double treadElevation,
            double finishThickness)
        {
            double finishedRiserFaceX = riserX - finishThickness;
            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    new Point2(finishedRiserFaceX, treadElevation),
                    new Point2(finishedRiserFaceX - NosingProjection, treadElevation + finishThickness),
                    new Point2(treadEndX, treadElevation + finishThickness),
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
            double finishThickness)
        {
            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    new Point2(riserX, bottomElevation),
                    new Point2(riserX, topElevation),
                    new Point2(riserX - finishThickness, topElevation),
                    new Point2(riserX - finishThickness, bottomElevation)
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

        private static void AddAnnotations(
            DrawingModel drawing,
            StairSectionParameters parameters,
            IReadOnlyList<FlightLayout> flights,
            double textHeight)
        {
            FlightLayout firstFlight = flights[0];
            FlightLayout lastFlight = flights[flights.Count - 1];
            double titleY = -parameters.WaistSlabThickness - (textHeight * 2.2);
            Point2 slopeMidPoint = new Point2(
                firstFlight.StartX + (firstFlight.Run * 0.5),
                firstFlight.StartElevation + (firstFlight.InclinedRise * 0.5));

            drawing.Texts.Add(new TextPrimitive(
                new Point2(-parameters.LandingLength, titleY),
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} - {1} BAC, h={2:0.#}, b={3:0.#}",
                    GetSectionTitle(parameters.PlanType),
                    parameters.TotalRisers,
                    parameters.RiserHeight,
                    parameters.TreadDepth),
                textHeight,
                0.0,
                StairLayers.Annotation));

            AddCallout(
                drawing,
                slopeMidPoint + new Point2(0.0, -parameters.WaistSlabThickness * 0.55),
                slopeMidPoint + new Point2(
                    parameters.TreadDepth * 2.0,
                    -parameters.WaistSlabThickness * 3.0),
                string.Format(CultureInfo.InvariantCulture, "BAN THANG BTCT d={0:0.#}", parameters.WaistSlabThickness),
                textHeight);

            AddCallout(
                drawing,
                slopeMidPoint + new Point2(0.0, parameters.RiserHeight * 0.65),
                slopeMidPoint + new Point2(
                    parameters.TreadDepth * 2.0,
                    parameters.RiserHeight * 3.0),
                "BAC XAY GACH",
                textHeight);

            AddCallout(
                drawing,
                new Point2(
                    firstFlight.StartX + (parameters.TreadDepth * 1.5),
                    firstFlight.StartElevation + (parameters.RiserHeight * 2.0)),
                new Point2(
                    -parameters.LandingLength,
                    firstFlight.StartElevation + (parameters.RiserHeight * 3.0)),
                string.Format(
                    CultureInfo.InvariantCulture,
                    "MAT BAC + CO BAC day {0:0.#}, MUI BAC {1:0.#}",
                    parameters.FinishThickness,
                    NosingProjection),
                textHeight);

            drawing.Texts.Add(new TextPrimitive(
                new Point2(-parameters.LandingLength, titleY - (textHeight * 1.8)),
                string.Format(
                    CultureInfo.InvariantCulture,
                    "LUU Y: MAT CAT {0}; KICH THUOC VA BAN THANG PHAI DUOC KY SU KET CAU KIEM TRA",
                    flights.Count == 1 ? "DOC" : "KHAI TRIEN"),
                textHeight * 0.85,
                0.0,
                StairLayers.Annotation));

            if (flights.Count > 1)
            {
                drawing.Texts.Add(new TextPrimitive(
                    new Point2(
                        lastFlight.EndX + parameters.LandingLength,
                        lastFlight.LandingElevation + (textHeight * 1.5)),
                    parameters.PlanType == StairPlanType.LShape
                        ? "CHIEU DI THANG CHU L"
                        : "CHIEU DI THANG CHU U",
                    textHeight * 0.85,
                    0.0,
                    StairLayers.Annotation));
            }
        }

        private static string GetSectionTitle(StairPlanType planType)
        {
            switch (planType)
            {
                case StairPlanType.LShape:
                    return "MAT CAT KHAI TRIEN THANG CHU L";
                case StairPlanType.UShape:
                    return "MAT CAT KHAI TRIEN THANG CHU U";
                default:
                    return "MAT CAT CAU THANG THANG";
            }
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

        private static void AddCallout(
            DrawingModel drawing,
            Point2 target,
            Point2 textPosition,
            string text,
            double textHeight)
        {
            drawing.Lines.Add(new LinePrimitive(target, textPosition, StairLayers.Annotation));
            drawing.Texts.Add(new TextPrimitive(
                textPosition,
                text,
                textHeight,
                0.0,
                StairLayers.Annotation));
        }

        private sealed class FlightLayout
        {
            public FlightLayout(
                double startX,
                double startElevation,
                int risers,
                double riserHeight,
                double treadDepth)
            {
                if (risers < 2)
                {
                    throw new ArgumentOutOfRangeException(nameof(risers));
                }

                StartX = startX;
                StartElevation = startElevation;
                TreadCount = risers - 1;
                Run = TreadCount * treadDepth;
                InclinedRise = TreadCount * riserHeight;
                EndX = StartX + Run;
                LastTreadElevation = StartElevation + InclinedRise;
                LandingElevation = StartElevation + (risers * riserHeight);
            }

            public double StartX { get; }

            public double StartElevation { get; }

            public int TreadCount { get; }

            public double Run { get; }

            public double InclinedRise { get; }

            public double EndX { get; }

            public double LastTreadElevation { get; }

            public double LandingElevation { get; }
        }
    }
}
