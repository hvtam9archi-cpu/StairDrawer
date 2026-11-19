using System;
using System.Collections.Generic;
using StairDrawer.Models;

namespace StairDrawer.Geometry
{
    internal static partial class StairSectionGeometry
    {
        private const double NosingProjection = 20.0;
        private const double NosingChamfer = 5.0;
        private const double RiserFinishBottomClearance = 20.0;
        private const double TreadMortarThickness = 20.0;
        private const double RiserMortarThickness = 15.0;
        private const double IronHandrailDiameter = StairRailingDimensions.HandrailWidth;
        private const double IronTerminalPostWidth = StairRailingDimensions.TerminalPostWidth;
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
            DrawingModel drawing = CreateCore(parameters);
            NormalizeConnectedRailing(drawing);
            return drawing;
        }

        private static DrawingModel CreateCore(StairSectionParameters parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            if (parameters.HasStraightLanding)
            {
                DrawingModel straight = CreateStraightLandingSection(parameters);
                return parameters.SectionCut.ReverseView ? ReflectSection(straight) : straight;
            }

            IReadOnlyList<FlightLayout> flights = CreateFlightLayouts(parameters);
            FlightLayout cutFlight = flights[0];
            bool isLShape = parameters.PlanType == StairPlanType.LShape;
            double cutLandingLength = parameters.PlanType == StairPlanType.Straight
                ? parameters.LandingLength : parameters.PlanLandingDepth;
            StairSectionCut cut = StairSectionCut.FromSection(parameters);
            if (cut.Y > cut.StairWidth)
            {
                DrawingModel offset = CreateOffsetSection(parameters, cut, flights);
                return cut.ReverseView ? ReflectSection(offset) : offset;
            }

            var drawing = new DrawingModel();
            var cutWinderStations = new List<double>();
            if (parameters.TurnSteps.HasSteps)
            {
                AddProjectedUTurn(drawing, parameters, cutFlight, cut, cutWinderStations);
            }

            if (parameters.PlanType == StairPlanType.Straight)
            {
                AddStraightConcreteSlab(drawing, parameters, flights);
            }
            else
            {
                AddCutConcreteSlab(drawing, parameters, cutFlight, cutLandingLength);
            }
            double flatFinishLength = cutWinderStations.Count == 0
                ? cutLandingLength
                : cutWinderStations[0] - cutFlight.EndX;
            AddCutBrickStepsAndFinishes(drawing, parameters, cutFlight, flatFinishLength);
            if (cutWinderStations.Count > 0)
            {
                AddCutWinderSteps(
                    drawing, parameters, cutFlight.LandingElevation, cutFlight.EndX + cutLandingLength, cutWinderStations);
            }

            if (isLShape && !cut.ReverseView)
            {
                AddProjectedLFlight(drawing, parameters, cutFlight);
            }

            if (flights.Count > 1 && !cut.ReverseView)
            {
                FlightLayout upperFlight = flights[1];
                double undersideEndElevation = upperFlight.LastTreadElevation
                    - parameters.WaistSlabThickness;
                AddVisibleFlight(drawing, parameters, upperFlight, undersideEndElevation);
                AddCutArrivalFloorAndBeam(drawing, parameters, upperFlight, undersideEndElevation);
                AddVisibleFinishes(drawing, parameters, upperFlight);
            }

            if (parameters.DrawRailing)
            {
                IReadOnlyList<FlightLayout> visibleFlights = cut.ReverseView ? new[] { cutFlight } : flights;
                AddSectionFlightRailings(drawing, parameters, visibleFlights);
            }

            return cut.ReverseView ? ReflectSection(drawing) : drawing;
        }

        private static void AddSectionFlightRailings(
            DrawingModel drawing, StairSectionParameters parameters, IReadOnlyList<FlightLayout> flights, bool firstFlightCut = true)
        {
            if (parameters.RailingType == StairRailingType.Iron)
            {
                AddIronSectionRailings(drawing, parameters, flights);
                return;
            }

            AddConnectedGlassRailings(drawing, parameters, flights, firstFlightCut);
        }

        private static void AddIronSectionRailings(
            DrawingModel drawing, StairSectionParameters parameters, IReadOnlyList<FlightLayout> flights)
        {
            var near = new DrawingModel();
            var far = new DrawingModel();
            AddFlightHandrail(near, parameters, flights);
            PolylinePrimitive upperHandrail = null;
            if (flights.Count > 1)
            {
                // Split at the shared miter so each flight is detailed together with its own handrail.
                IReadOnlyList<Point2> full = near.Polylines[0].Vertices;
                var nearVertices = new List<Point2>();
                for (int index = 0; index <= 4; index++)
                {
                    nearVertices.Add(full[index]);
                }

                for (int index = 7; index < full.Count; index++)
                {
                    nearVertices.Add(full[index]);
                }

                near.Polylines[0] = new PolylinePrimitive(nearVertices, true, StairLayers.Railing, drawOnTop: true);
                upperHandrail = new PolylinePrimitive(
                    new[] { full[4], full[5], full[6], full[7] }, true, StairLayers.Railing, drawOnTop: true);
                far.Polylines.Add(upperHandrail);
            }
            for (int index = 0; index < flights.Count; index++)
            {
                FlightLayout flight = flights[index];
                bool startsAtFloor = parameters.PlanType != StairPlanType.UShape
                    || Math.Abs(flight.StartElevation) <= PointTolerance;
                bool endsAtFloor = parameters.PlanType != StairPlanType.UShape
                    || flight.StartElevation > PointTolerance;
                AddIronRailing(index == 0 ? near : far, parameters, flight,
                    GetRailingBasePoint(flight, parameters, 0), startsAtFloor, endsAtFloor);
            }

            if (parameters.PlanType == StairPlanType.LShape && flights.Count == 1 && !parameters.SectionCut.ReverseView)
            {
                double x = StairPlanGeometry.GetLFlightStartX(flights[0].EndX, parameters.PlanLandingDepth, parameters.PlanStairWidth)
                    + GetSectionRailingInset(parameters);
                AddMetalBox(near, x - 10, flights[0].LandingElevation + parameters.RiserHeight + parameters.FinishThickness,
                    x + 10, parameters.FloorHeight + parameters.FinishThickness + parameters.RailingHeight - 25);
            }

            if (flights.Count > 1)
            {
                double turnX = flights[0].EndX + GetSectionRailingInset(parameters);
                for (int index = 0; index < flights.Count; index++)
                {
                    FlightLayout flight = flights[index];
                    Point2 landing = GetRailingBasePoint(flight, parameters, index == 0 ? flight.TreadCount : 0);
                    foreach (double height in new[]
                    {
                        IronLowerRailClearance + (IronMemberThickness * 0.5),
                        parameters.RailingHeight - (IronHandrailDiameter * 0.5)
                            - IronUpperRailClearance - (IronMemberThickness * 0.5)
                    })
                    {
                        AddIronRailSegmentBetweenPosts(near,
                            new Point2(landing.X, landing.Y + height),
                            new Point2(turnX, landing.Y + height), IronMemberThickness);
                    }
                }

                AddUTurnRailingPosts(near, parameters, flights);
            }

            var nearMembers = new List<PolylinePrimitive>(near.Polylines);
            RailingOutlineGeometry.Append(drawing, new List<PolylinePrimitive>(far.Polylines), false, nearMembers);
            RailingOutlineGeometry.Append(drawing, nearMembers, true,
                upperHandrail == null ? null : new[] { upperHandrail });
        }

        private static DrawingModel ReflectSection(DrawingModel drawing)
        {
            var reflected = new DrawingModel();
            AppendReflectedSection(reflected, drawing, 0.0);
            return reflected;
        }

        private static void AppendReflectedSection(DrawingModel target, DrawingModel source, double axis)
        {
            Point2 Reflect(Point2 point) => new Point2(axis - point.X, point.Y);
            foreach (LinePrimitive line in source.Lines)
            {
                target.Lines.Add(new LinePrimitive(Reflect(line.Start), Reflect(line.End), line.LayerName, line.LinetypeScale));
            }

            foreach (CirclePrimitive circle in source.Circles)
                target.Circles.Add(new CirclePrimitive(Reflect(circle.Center), circle.Radius, circle.LayerName));

            foreach (PolylinePrimitive polyline in source.Polylines)
            {
                var vertices = new List<Point2>(polyline.Vertices.Count);
                foreach (Point2 vertex in polyline.Vertices)
                {
                    vertices.Add(Reflect(vertex));
                }

                target.Polylines.Add(new PolylinePrimitive(
                    vertices, polyline.Closed, polyline.LayerName, polyline.GlobalWidth, polyline.LinetypeScale,
                    polyline.DrawOnTop, polyline.OccludesBackground));
            }

            foreach (WipeoutPrimitive wipeout in source.Wipeouts)
            {
                var boundary = new List<Point2>(wipeout.Boundary.Count);
                foreach (Point2 vertex in wipeout.Boundary)
                {
                    boundary.Add(Reflect(vertex));
                }

                target.Wipeouts.Add(new WipeoutPrimitive(boundary, wipeout.LayerName));
            }
        }

        private static DrawingModel CreateOffsetSection(
            StairSectionParameters parameters, StairSectionCut cut, IReadOnlyList<FlightLayout> flights)
        {
            var drawing = new DrawingModel();
            FlightLayout lower = flights[0];
            if (parameters.PlanType == StairPlanType.LShape)
            {
                AddTransverseFlightSection(
                    drawing, parameters, cut, cut.StartX, cut.EndX, cut.StairWidth,
                    lower.LandingElevation, parameters.SecondFlightRisers);
                if (cut.ReverseView)
                {
                    AddBackgroundFlight(drawing, parameters, lower);
                }

                return drawing;
            }

            FlightLayout upper = flights[1];
            var turn = new UTurnLayout(
                cut.StairWidth, parameters.PlanLandingDepth, parameters.PlanFlightGap, parameters.TreadDepth, parameters.TurnSteps);
            double firstCornerTop = lower.LandingElevation + (turn.Steps.FirstWinderRisers * parameters.RiserHeight);
            double secondCornerBase = upper.StartElevation - (turn.Steps.SecondWinderRisers * parameters.RiserHeight);

            if (cut.Y >= cut.UpperFlightY)
            {
                AddUpperFlightCut(drawing, parameters, cut, lower.EndX, upper, turn, secondCornerBase);
                if (cut.ReverseView)
                {
                    AddBackgroundFlight(drawing, parameters, lower);
                    AddRetainedTreads(
                        drawing, parameters, cut, lower.EndX, lower.EndX + turn.Depth,
                        cut.StairWidth, firstCornerTop, turn.Steps.MiddleFlightRisers);
                }
            }
            else
            {
                AddTransverseFlightSection(
                    drawing, parameters, cut, lower.EndX, lower.EndX + turn.Depth,
                    cut.StairWidth, firstCornerTop, turn.Steps.MiddleFlightRisers);
                AddBackgroundFlight(drawing, parameters, cut.ReverseView ? lower : upper);
            }

            AddRetainedWinderLines(drawing, parameters, cut, turn, lower.EndX, lower.LandingElevation, false);
            AddRetainedWinderLines(drawing, parameters, cut, turn, lower.EndX, secondCornerBase, true);
            if (parameters.DrawRailing) AddProjectedTurnConnection(drawing, parameters, lower, upper);
            return drawing;
        }

        private static void AddUpperFlightCut(
            DrawingModel drawing, StairSectionParameters parameters, StairSectionCut cut, double turnX,
            FlightLayout upper, UTurnLayout turn, double cornerBase)
        {
            var local = new DrawingModel();
            var flight = new FlightLayout(
                0.0, upper.StartElevation, parameters.SecondFlightRisers, parameters.RiserHeight, parameters.TreadDepth, 1.0);
            double offset = Math.Max(0.0001, cut.Y - cut.UpperFlightY);
            var stations = new List<double>();
            for (int index = 1; index <= turn.Steps.SecondWinderRisers; index++)
            {
                Point2 outer = turn.GetOuterPoint(true, index);
                if (offset >= outer.Y - PointTolerance)
                {
                    cornerBase += parameters.RiserHeight;
                }
                else
                {
                    stations.Add(-outer.X * offset / outer.Y);
                }
            }

            AddHorizontalSection(local, -turn.Depth, 0.0, cornerBase - TreadMortarThickness,
                parameters.WaistSlabThickness, StairLayers.Concrete);
            double firstWinder = stations.Count == 0 ? 0.0 : stations[0];
            AddHorizontalFinish(local, -turn.Depth, firstWinder, cornerBase, parameters.FinishThickness);
            if (stations.Count > 0)
            {
                AddCutWinderSteps(local, parameters, cornerBase, 0.0, stations);
            }

            AddCutConcreteSlab(local, parameters, flight, parameters.LandingLength);
            AddCutBrickStepsAndFinishes(local, parameters, flight, parameters.LandingLength);
            if (parameters.DrawRailing)
            {
                AddSectionFlightRailings(local, parameters, new[] { flight });
            }

            AppendReflectedSection(drawing, local, turnX);
        }

        private static void AddBackgroundFlight(DrawingModel drawing, StairSectionParameters parameters, FlightLayout flight)
        {
            AddVisibleFlight(drawing, parameters, flight, flight.LastTreadElevation - parameters.WaistSlabThickness);
            AddVisibleFinishes(drawing, parameters, flight);
            if (parameters.DrawRailing)
            {
                AddSectionFlightRailings(drawing, parameters, new[] { flight }, false);
            }
        }

        private static void AddTransverseFlightSection(
            DrawingModel drawing, StairSectionParameters parameters, StairSectionCut cut,
            double left, double right, double startY, double startElevation, int risers)
        {
            double treadPosition = Math.Max(0.0, (cut.Y - startY) / parameters.TreadDepth);
            double concreteElevation = startElevation;
            double treadElevation = startElevation;
            double thickness = parameters.WaistSlabThickness;
            if (risers > 0)
            {
                int tread = Math.Min(risers, (int)Math.Floor(treadPosition + 1e-9) + 1);
                treadElevation += tread * parameters.RiserHeight;
                if (tread < risers)
                {
                    concreteElevation += treadPosition * parameters.RiserHeight;
                    double slope = parameters.RiserHeight / parameters.TreadDepth;
                    thickness *= Math.Sqrt(1.0 + (slope * slope));
                }
                else
                {
                    concreteElevation = treadElevation;
                }
            }

            AddHorizontalSection(drawing, left, right, concreteElevation - TreadMortarThickness,
                thickness, StairLayers.Concrete);
            if (treadElevation - concreteElevation > PointTolerance)
            {
                double distanceFromRiser = Math.Max(0.0, cut.Y - startY) % parameters.TreadDepth;
                if (risers == 0 || distanceFromRiser >= RiserMortarThickness)
                {
                    AddHorizontalSection(
                        drawing, left, right, treadElevation - TreadMortarThickness,
                        treadElevation - concreteElevation, StairLayers.Brick);
                }
            }

            AddHorizontalFinish(drawing, left, right, treadElevation, parameters.FinishThickness);
            AddRetainedTreads(drawing, parameters, cut, left, right, startY, startElevation, risers,
                concreteElevation - TreadMortarThickness - thickness, treadElevation + parameters.FinishThickness);
            if (parameters.DrawRailing)
            {
                double inset = StairRailingDimensions.GetInset(right - left);
                double top = treadElevation + parameters.FinishThickness + parameters.RailingHeight;
                double half = Math.Min(IronHandrailDiameter * 0.5, inset * 0.5);
                foreach (double x in new[] { left + inset, right - inset })
                {
                    if (parameters.RailingType == StairRailingType.Glass)
                    {
                        var metal = new DrawingModel();
                        AddGlassTransverseEdge(drawing, metal, parameters, x, treadElevation + parameters.FinishThickness,
                            Math.Max(top, startElevation + risers * parameters.RiserHeight + parameters.FinishThickness + parameters.RailingHeight), true, false);
                        RailingOutlineGeometry.Append(drawing, new List<PolylinePrimitive>(metal.Polylines), true);
                    }
                    else AddHorizontalSection(drawing, x - half, x + half, top + half, half * 2.0, StairLayers.Railing);
                }
            }
        }

        private static void AddHorizontalSection(
            DrawingModel drawing, double left, double right, double top, double thickness, string layer)
        {
            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    new Point2(left, top), new Point2(right, top),
                    new Point2(right, top - thickness), new Point2(left, top - thickness)
                },
                true, layer));
        }

        private static void AddRetainedTreads(
            DrawingModel drawing, StairSectionParameters parameters, StairSectionCut cut,
            double left, double right, double startY, double startElevation, int risers,
            double coverBottom = double.PositiveInfinity, double coverTop = double.NegativeInfinity)
        {
            for (int index = 0; index < risers; index++)
            {
                double y = startY + (index * parameters.TreadDepth);
                double height = startElevation + ((index + 1) * parameters.RiserHeight) + parameters.FinishThickness;
                if (IsBehindCut(cut, y) && (height < coverBottom || height > coverTop))
                {
                    AddProjectedTread(drawing, parameters, left, right, height - parameters.FinishThickness,
                        height - parameters.RiserHeight, coverBottom, coverTop);
                }
            }
        }

        private static void AddRetainedWinderLines(
            DrawingModel drawing, StairSectionParameters parameters, StairSectionCut cut, UTurnLayout turn,
            double turnX, double baseElevation, bool upper)
        {
            double pivotY = upper ? cut.UpperFlightY : cut.StairWidth;
            for (int index = 1; index <= turn.GetCornerRiserCount(upper); index++)
            {
                Point2 edge = turn.GetOuterPoint(upper, index);
                bool pivotBehind = IsBehindCut(cut, pivotY);
                bool endBehind = IsBehindCut(cut, pivotY + edge.Y);
                if (!pivotBehind && !endBehind)
                {
                    continue;
                }

                double start = 0.0;
                double end = edge.X;
                if (pivotBehind != endBehind)
                {
                    double intersection = edge.X * (cut.Y - pivotY) / edge.Y;
                    start = pivotBehind ? 0.0 : intersection;
                    end = endBehind ? edge.X : intersection;
                }

                if (end - start > PointTolerance)
                {
                    double elevation = baseElevation + (index * parameters.RiserHeight);
                    AddProjectedTread(drawing, parameters, turnX + start, turnX + end, elevation,
                        elevation - parameters.RiserHeight + parameters.FinishThickness);
                }
            }
        }

        private static void AddProjectedTread(
            DrawingModel drawing, StairSectionParameters parameters, double left, double right,
            double elevation, double previousFinishedElevation,
            double coverBottom = double.PositiveInfinity, double coverTop = double.NegativeInfinity)
        {
            if (right - left <= PointTolerance)
            {
                return;
            }

            AddHorizontalFinish(drawing, left, right, elevation, parameters.FinishThickness);
            double bottom = Math.Min(previousFinishedElevation, elevation);
            double top = Math.Max(previousFinishedElevation, elevation);
            if (coverBottom > coverTop)
            {
                AddProjectedRiserEdge(drawing, right, bottom, top);
            }
            else
            {
                AddProjectedRiserEdge(drawing, right, bottom, Math.Min(top, coverBottom));
                AddProjectedRiserEdge(drawing, right, Math.Max(bottom, coverTop), top);
            }
        }

        private static void AddProjectedRiserEdge(DrawingModel drawing, double x, double bottom, double top)
        {
            if (top - bottom > PointTolerance)
            {
                drawing.Lines.Add(new LinePrimitive(
                    new Point2(x, bottom), new Point2(x, top), StairLayers.SectionVisible));
            }
        }

        private static bool IsBehindCut(StairSectionCut cut, double y)
        {
            return cut.ReverseView ? y < cut.Y - PointTolerance : y > cut.Y + PointTolerance;
        }

        private static DrawingModel CreateStraightLandingSection(StairSectionParameters parameters)
        {
            var stations = new List<double>(parameters.TotalRisers);
            var landingTreads = new HashSet<int>();
            AddDevelopedFlightStations(stations, landingTreads, 0.0, parameters.FirstFlightRisers, parameters.TreadDepth);
            double landingStart = stations[stations.Count - 1];
            double upperFlightStart = landingStart + parameters.PlanLandingDepth;
            AddDevelopedFlightStations(
                stations, landingTreads, upperFlightStart, parameters.SecondFlightRisers, parameters.TreadDepth);

            var drawing = new DrawingModel();
            var flights = new[]
            {
                new FlightLayout(
                    0.0, 0.0, parameters.FirstFlightRisers, parameters.RiserHeight, parameters.TreadDepth, 1.0),
                new FlightLayout(
                    upperFlightStart, parameters.FirstFlightRisers * parameters.RiserHeight,
                    parameters.SecondFlightRisers, parameters.RiserHeight, parameters.TreadDepth, 1.0)
            };
            AddStraightConcreteSlab(drawing, parameters, flights);
            double finish = parameters.FinishThickness;
            double arrivalEnd = stations[stations.Count - 1] + parameters.LandingLength;
            for (int index = 0; index < stations.Count; index++)
            {
                double start = stations[index];
                double end = index + 1 < stations.Count ? stations[index + 1] : arrivalEnd;
                double bottom = index * parameters.RiserHeight;
                double top = (index + 1) * parameters.RiserHeight;
                if (landingTreads.Contains(index))
                {
                    AddTreadFinish(drawing, start, end, top, finish, 1.0);
                }
                else
                {
                    AddBrickStep(drawing, start, end, bottom, top, 1.0);
                    AddTreadFinish(drawing, start, end, top, finish, 1.0);
                }

                AddRiserFinish(drawing, start, bottom, top, finish, 1.0);
            }
            if (parameters.DrawRailing)
            {
                AddDevelopedRailing(drawing, parameters, stations, landingTreads);
            }

            return drawing;
        }

        private static void AddStraightConcreteSlab(
            DrawingModel drawing, StairSectionParameters parameters, IReadOnlyList<FlightLayout> flights)
        {
            double thickness = parameters.WaistSlabThickness;
            double slope = parameters.RiserHeight / parameters.TreadDepth;
            double slopeFactor = Math.Sqrt(1.0 + (slope * slope));
            double verticalDrop = thickness * slopeFactor;
            double jointOffset = thickness * slope / (slopeFactor + 1.0);
            double floorStart = flights[0].StartX;
            FlightLayout arrival = flights[flights.Count - 1];
            double floorEnd = arrival.EndX + parameters.LandingLength;
            var outline = new List<Point2> { new Point2(floorStart, 0.0) };
            var soffit = new List<Point2> { new Point2(floorStart, -thickness) };
            for (int index = 0; index < flights.Count; index++)
            {
                FlightLayout flight = flights[index];
                AddVertexIfDistinct(outline, new Point2(flight.StartX, flight.StartElevation));
                outline.Add(new Point2(flight.EndX, flight.LastTreadElevation));
                outline.Add(new Point2(flight.EndX + RiserMortarThickness, flight.LastTreadElevation));
                outline.Add(new Point2(flight.EndX + RiserMortarThickness, flight.LandingElevation));

                // The underside is offset by the normal slab thickness and trimmed to the horizontal soffits.
                double nextJoint = index + 1 < flights.Count ? flights[index + 1].StartX + jointOffset : floorEnd;
                double startJoint = Math.Min(flight.StartX + jointOffset, floorEnd);
                double endJoint = Math.Min(flight.EndX + parameters.TreadDepth + jointOffset, Math.Min(nextJoint, floorEnd));
                AddVertexIfDistinct(soffit, new Point2(startJoint, flight.StartElevation - thickness));
                AddVertexIfDistinct(soffit, new Point2(
                    endJoint,
                    flight.StartElevation + ((endJoint - flight.StartX) * slope) - verticalDrop));
            }

            outline.Add(new Point2(floorEnd, arrival.LandingElevation));
            AddVertexIfDistinct(soffit, new Point2(floorEnd, soffit[soffit.Count - 1].Y));
            for (int index = soffit.Count - 1; index >= 0; index--)
            {
                outline.Add(soffit[index]);
            }

            drawing.Polylines.Add(new PolylinePrimitive(
                ClipSlabAboveFloor(LowerStructure(outline), flights[0].StartElevation), true, StairLayers.Concrete));
        }

        private static void AddDevelopedFlightStations(
            IList<double> stations, ISet<int> landingTreads, double start, int risers, double treadDepth)
        {
            for (int index = 0; index < risers; index++)
            {
                stations.Add(start + (index * treadDepth));
            }

            if (risers > 0)
            {
                landingTreads.Add(stations.Count - 1);
            }
        }

        private static void AddDevelopedRailing(
            DrawingModel drawing, StairSectionParameters parameters, IReadOnlyList<double> stations, ISet<int> landingTreads)
        {
            var pitchLine = new List<Point2>();
            for (int index = 0; index < stations.Count; index++)
            {
                double elevation = ((index + 1) * parameters.RiserHeight) + parameters.FinishThickness;
                pitchLine.Add(new Point2(stations[index], elevation));
                if (index + 1 < stations.Count && landingTreads.Contains(index))
                {
                    double flatLength = stations[index + 1] - stations[index] - parameters.TreadDepth;
                    if (flatLength > PointTolerance)
                    {
                        pitchLine.Add(new Point2(stations[index] + flatLength, elevation));
                    }
                }
            }

            var handrail = new List<Point2>(pitchLine.Count);
            var railOffset = new Point2(0.0, parameters.RailingHeight);
            for (int index = 0; index < pitchLine.Count; index++)
            {
                handrail.Add(pitchLine[index] + railOffset);
            }

            if (parameters.RailingType == StairRailingType.Iron)
            {
                AddContinuousMemberOutline(drawing, handrail, IronHandrailDiameter);
                for (int index = 1; index < pitchLine.Count; index++)
                {
                    Point2 start = pitchLine[index - 1];
                    Point2 end = pitchLine[index];
                    double length = end.X - start.X;
                    double slope = (end.Y - start.Y) / length;
                    AddIronPosts(
                        drawing, start.X, end.X, progress => progress < length ? start.Y : end.Y,
                        progress => start.Y + (slope * progress), slope, parameters.RailingHeight,
                        true, index == pitchLine.Count - 1);
                }

                return;
            }

            AddDevelopedGlassRailings(drawing, parameters, pitchLine);
        }

        private static IReadOnlyList<FlightLayout> CreateFlightLayouts(StairSectionParameters parameters)
        {
            var firstFlight = new FlightLayout(
                0.0,
                0.0,
                parameters.FirstFlightRisers,
                parameters.RiserHeight,
                parameters.TreadDepth,
                1.0);
            var flights = new List<FlightLayout> { firstFlight };
            if (parameters.PlanType == StairPlanType.UShape)
            {
                flights.Add(new FlightLayout(
                    firstFlight.EndX,
                    (parameters.TotalRisers - parameters.SecondFlightRisers) * parameters.RiserHeight,
                    parameters.SecondFlightRisers,
                    parameters.RiserHeight,
                    parameters.TreadDepth,
                    -1.0));
            }

            return flights;
        }

        private static void AddProjectedUTurn(
            DrawingModel drawing, StairSectionParameters parameters, FlightLayout lowerFlight,
            StairSectionCut cut, IList<double> cutWinderStations)
        {
            UTurnSteps steps = parameters.TurnSteps;
            var turn = new UTurnLayout(
                parameters.PlanStairWidth, parameters.PlanLandingDepth, 0.0, parameters.TreadDepth, steps);
            var treadWidths = new List<double>((int)steps.TotalRisers);
            for (int index = 1; index <= steps.FirstWinderRisers; index++)
            {
                Point2 outerPoint = turn.GetOuterPoint(false, index);
                if (cut.IntersectLowerWinder(outerPoint, out double visibleWidth))
                {
                    cutWinderStations.Add(lowerFlight.EndX + visibleWidth);
                }

                treadWidths.Add(visibleWidth);
            }

            if (cut.ReverseView)
            {
                return;
            }

            for (int index = 0; index < steps.MiddleFlightRisers; index++)
            {
                treadWidths.Add(turn.Depth);
            }

            for (int index = 1; index <= steps.SecondWinderRisers; index++)
            {
                treadWidths.Add(turn.GetOuterPoint(true, index).X);
            }

            // With a flat second corner, the upper landing extends across the full width of the turn.
            if (steps.SecondWinderRisers == 0 && cutWinderStations.Count != treadWidths.Count)
            {
                treadWidths[treadWidths.Count - 1] = turn.Depth;
            }

            double x = lowerFlight.EndX;
            double bottom = lowerFlight.LandingElevation + parameters.FinishThickness;
            for (int index = 0; index < treadWidths.Count; index++)
            {
                double elevation = lowerFlight.LandingElevation + ((index + 1) * parameters.RiserHeight);
                double width = treadWidths[index];
                AddProjectedTread(drawing, parameters, x, x + width, elevation,
                    elevation - parameters.RiserHeight + parameters.FinishThickness);
            }

            double top = bottom + (treadWidths.Count * parameters.RiserHeight);
            drawing.Lines.Add(new LinePrimitive(new Point2(x, bottom), new Point2(x, top), StairLayers.SectionVisible));
        }

        private static void AddCutWinderSteps(
            DrawingModel drawing, StairSectionParameters parameters, double landingElevation, double landingEnd,
            IReadOnlyList<double> cutWinderStations)
        {
            var brick = new List<Point2>
            {
                new Point2(Math.Min(landingEnd, cutWinderStations[0] + RiserMortarThickness),
                    landingElevation - TreadMortarThickness)
            };
            for (int index = 0; index < cutWinderStations.Count; index++)
            {
                double start = cutWinderStations[index];
                double end = index + 1 < cutWinderStations.Count ? cutWinderStations[index + 1] : landingEnd;
                double bottom = landingElevation + (index * parameters.RiserHeight);
                double top = bottom + parameters.RiserHeight;
                if (start + RiserMortarThickness < landingEnd - PointTolerance)
                {
                    AddVertexIfDistinct(brick, new Point2(start + RiserMortarThickness, top - TreadMortarThickness));
                    AddVertexIfDistinct(brick, new Point2(
                        index + 1 < cutWinderStations.Count ? Math.Min(landingEnd, end + RiserMortarThickness) : end,
                        top - TreadMortarThickness));
                }
                AddTreadFinish(drawing, start, end, top, parameters.FinishThickness, 1.0);
                AddRiserFinish(drawing, start, bottom, top, parameters.FinishThickness, 1.0);
            }

            AddVertexIfDistinct(brick, new Point2(landingEnd, landingElevation - TreadMortarThickness));
            AppendMaterialPolygon(drawing, brick, StairLayers.Brick);
        }

        private static void AddCutConcreteSlab(
            DrawingModel drawing,
            StairSectionParameters parameters,
            FlightLayout flight,
            double endLandingLength)
        {
            var vertices = new List<Point2>();
            AddVertexIfDistinct(vertices, new Point2(flight.StartX, flight.StartElevation));
            AddVertexIfDistinct(vertices, new Point2(flight.EndX, flight.LastTreadElevation));
            AddVertexIfDistinct(vertices, new Point2(flight.EndX + RiserMortarThickness, flight.LastTreadElevation));
            AddVertexIfDistinct(vertices, new Point2(flight.EndX + RiserMortarThickness, flight.LandingElevation));
            double landingEndX = flight.EndX + endLandingLength;
            double landingBottomElevation = flight.LandingElevation
                - parameters.WaistSlabThickness;
            AddVertexIfDistinct(vertices, new Point2(landingEndX, flight.LandingElevation));
            AddVertexIfDistinct(vertices, new Point2(landingEndX, landingBottomElevation));

            Point2 downNormal = new Point2(flight.InclinedRise, -flight.Run).Normalize();
            Point2 slabOffset = downNormal * parameters.WaistSlabThickness;
            Point2 inclinedSlabEnd = new Point2(flight.EndX, flight.LastTreadElevation) + slabOffset;
            double inclinedSlope = flight.InclinedRise / flight.Run;
            double intersectionRun = (landingBottomElevation - inclinedSlabEnd.Y) * flight.Run / flight.InclinedRise;
            double landingIntersectionX = Math.Min(landingEndX, inclinedSlabEnd.X + intersectionRun);
            AddVertexIfDistinct(vertices, new Point2(landingIntersectionX, landingBottomElevation));
            AddVertexIfDistinct(vertices, inclinedSlabEnd);

            double slopeFactor = Math.Sqrt(1.0 + (inclinedSlope * inclinedSlope));
            double footJointX = flight.StartX + (parameters.WaistSlabThickness * inclinedSlope / (slopeFactor + 1.0));
            AddVertexIfDistinct(vertices, new Point2(footJointX, flight.StartElevation - parameters.WaistSlabThickness));
            AddVertexIfDistinct(vertices, new Point2(
                flight.StartX,
                flight.StartElevation - parameters.WaistSlabThickness));

            vertices = LowerStructure(vertices);
            drawing.Polylines.Add(new PolylinePrimitive(
                Math.Abs(flight.StartElevation) <= PointTolerance
                    ? ClipSlabAboveFloor(vertices, flight.StartElevation) : vertices,
                true,
                StairLayers.Concrete));
        }

        private static List<Point2> LowerStructure(IReadOnlyList<Point2> boundary)
        {
            var lowered = new List<Point2>(boundary.Count);
            foreach (Point2 point in boundary)
            {
                lowered.Add(new Point2(point.X, point.Y - TreadMortarThickness));
            }

            return lowered;
        }

        private static List<Point2> ClipSlabAboveFloor(IReadOnlyList<Point2> boundary, double floorElevation)
        {
            var clipped = new List<Point2>();
            Point2 previous = boundary[boundary.Count - 1];
            bool previousInside = previous.Y >= floorElevation;
            foreach (Point2 current in boundary)
            {
                bool currentInside = current.Y >= floorElevation;
                if (previousInside != currentInside)
                {
                    double ratio = (floorElevation - previous.Y) / (current.Y - previous.Y);
                    AddVertexIfDistinct(clipped,
                        new Point2(previous.X + ((current.X - previous.X) * ratio), floorElevation));
                }

                if (currentInside)
                {
                    AddVertexIfDistinct(clipped, current);
                }

                previous = current;
                previousInside = currentInside;
            }

            if (clipped.Count > 1 && (clipped[0] - clipped[clipped.Count - 1]).Length <= PointTolerance)
            {
                clipped.RemoveAt(clipped.Count - 1);
            }

            return clipped;
        }

        private static void AddCutBrickStepsAndFinishes(
            DrawingModel drawing,
            StairSectionParameters parameters,
            FlightLayout flight,
            double endLandingLength)
        {
            for (int treadIndex = 0; treadIndex < flight.TreadCount; treadIndex++)
            {
                double x0 = flight.StartX + (treadIndex * parameters.TreadDepth);
                double x1 = x0 + parameters.TreadDepth;
                double y0 = flight.StartElevation + (treadIndex * parameters.RiserHeight);
                double y1 = y0 + parameters.RiserHeight;

                AddBrickStep(drawing, x0, x1, y0, y1, 1.0);

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
                flight.EndX + endLandingLength,
                flight.LandingElevation,
                parameters.FinishThickness,
                1.0);
        }

        private static void AddProjectedLFlight(DrawingModel drawing, StairSectionParameters parameters, FlightLayout cutFlight)
        {
            // In the section plane the upper flight runs into the page, so only its width projects onto X.
            double width = parameters.PlanStairWidth;
            double left = StairPlanGeometry.GetLFlightStartX(cutFlight.EndX, parameters.PlanLandingDepth, width);
            double right = left + width;
            double bottom = cutFlight.LandingElevation + parameters.FinishThickness;
            double top = parameters.FloorHeight + parameters.FinishThickness;
            drawing.Polylines.Add(new PolylinePrimitive(
                new[] { new Point2(left, bottom), new Point2(left, top), new Point2(right, top), new Point2(right, bottom) },
                false, StairLayers.SectionVisible));

            for (int index = 1; index <= parameters.SecondFlightRisers; index++)
            {
                double elevation = cutFlight.LandingElevation + (index * parameters.RiserHeight);
                AddProjectedTread(drawing, parameters, left, right, elevation,
                    elevation - parameters.RiserHeight + parameters.FinishThickness);
            }

            if (parameters.DrawRailing)
            {
                double inset = StairRailingDimensions.GetInset(width);
                double railingBottom = bottom + parameters.RiserHeight;
                double railingTop = top + parameters.RailingHeight;
                foreach (double railX in new[] { left + inset, right - inset })
                {
                    // The near edge is part of the continuous lower-flight/landing/upper-flight assembly.
                    if (Math.Abs(railX - left - inset) < PointTolerance) continue;
                    if (parameters.RailingType == StairRailingType.Iron)
                    {
                        double halfWidth = IronHandrailDiameter * 0.5;
                        AddIronBoxPost(
                            drawing, railX - halfWidth, railingBottom, railX + halfWidth, railingBottom,
                            railingTop, railingTop, true);
                    }
                    else
                    {
                        var metal = new DrawingModel();
                        AddGlassTransverseEdge(drawing, metal, parameters, railX, railingBottom, railingTop, true);
                        RailingOutlineGeometry.Append(drawing, new List<PolylinePrimitive>(metal.Polylines), true);
                    }
                }
            }
        }

        private static void AddVisibleFlight(
            DrawingModel drawing,
            StairSectionParameters parameters,
            FlightLayout flight,
            double undersideEndElevation)
        {
            // An uncut flight shows its outside edges, without the backing profile behind the stone.
            double lowerStartElevation = flight.StartElevation - parameters.WaistSlabThickness - TreadMortarThickness;
            drawing.Lines.Add(new LinePrimitive(
                new Point2(flight.StartX, lowerStartElevation),
                new Point2(flight.EndX, undersideEndElevation - TreadMortarThickness),
                StairLayers.SectionVisible));
            drawing.Lines.Add(new LinePrimitive(
                new Point2(flight.StartX, flight.StartElevation + parameters.FinishThickness),
                new Point2(flight.StartX, lowerStartElevation),
                StairLayers.SectionVisible));
            drawing.Lines.Add(new LinePrimitive(
                new Point2(flight.EndX, undersideEndElevation - TreadMortarThickness),
                new Point2(flight.EndX + (flight.Direction * RiserMortarThickness), undersideEndElevation - TreadMortarThickness),
                StairLayers.SectionVisible));
        }

        private static void AddCutArrivalFloorAndBeam(
            DrawingModel drawing,
            StairSectionParameters parameters,
            FlightLayout flight,
            double undersideEndElevation)
        {
            double landingBottomElevation = flight.LandingElevation
                - parameters.WaistSlabThickness;
            double landingEndX = flight.EndX
                + (flight.Direction * parameters.LandingLength);
            double beamFarX = flight.EndX
                + (flight.Direction * StairSupportWidth);
            double beamBottomElevation = Math.Min(
                flight.LandingElevation - StairSupportDepth,
                undersideEndElevation);
            double faceX = flight.EndX + (flight.Direction * RiserMortarThickness);
            drawing.Polylines.Add(new PolylinePrimitive(
                LowerStructure(new[]
                {
                    new Point2(faceX, flight.LandingElevation),
                    new Point2(landingEndX, flight.LandingElevation),
                    new Point2(landingEndX, landingBottomElevation),
                    new Point2(beamFarX, landingBottomElevation),
                    new Point2(beamFarX, beamBottomElevation),
                    new Point2(faceX, beamBottomElevation)
                }),
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
            AddTreadFinish(
                drawing,
                flight.EndX,
                flight.EndX + (flight.Direction * parameters.LandingLength),
                flight.LandingElevation,
                parameters.FinishThickness,
                flight.Direction);
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
            var vertices = new List<Point2>(5)
            {
                new Point2(nosingFrontX, treadElevation)
            };
            AddVertexIfDistinct(vertices, new Point2(nosingFrontX, topElevation - chamfer));
            vertices.Add(new Point2(nosingFrontX + (direction * chamfer), topElevation));
            vertices.Add(new Point2(treadEndX, topElevation));
            vertices.Add(new Point2(treadEndX, treadElevation));
            drawing.Polylines.Add(new PolylinePrimitive(
                vertices,
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

        private static void AddBrickStep(
            DrawingModel drawing, double startX, double endX, double bottom, double top, double direction)
        {
            double face = startX + (direction * RiserMortarThickness);
            double baseAtFace = bottom - TreadMortarThickness
                + ((top - bottom) * RiserMortarThickness / Math.Abs(endX - startX));
            AppendMaterialPolygon(drawing, new List<Point2>
            {
                new Point2(face, baseAtFace), new Point2(face, top - TreadMortarThickness),
                new Point2(endX, top - TreadMortarThickness)
            }, StairLayers.Brick);
        }

        private static void AppendMaterialPolygon(DrawingModel drawing, IReadOnlyList<Point2> boundary, string layer)
        {
            List<Point2> visible = ClipSlabAboveFloor(boundary, 0.0);
            if (visible.Count < 3)
            {
                return;
            }

            double twiceArea = 0.0;
            for (int index = 0; index < visible.Count; index++)
            {
                Point2 a = visible[index];
                Point2 b = visible[(index + 1) % visible.Count];
                twiceArea += (a.X * b.Y) - (a.Y * b.X);
            }

            if (Math.Abs(twiceArea) > PointTolerance)
            {
                drawing.Polylines.Add(new PolylinePrimitive(visible, true, layer));
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
            AddIronPosts(
                drawing,
                flight.StartX,
                flight.EndX,
                progress => GetFlightSurfaceElevation(flight, parameters, progress),
                progress => firstBase.Y
                    + (flightSlope * progress),
                flightSlope,
                parameters.RailingHeight,
                true,
                true,
                includeStartPost ? IronTerminalPostWidth : StairRailingDimensions.TurnPostWidth,
                includeEndPost ? IronTerminalPostWidth : StairRailingDimensions.TurnPostWidth);
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
            bool includeEndPost,
            double firstPostWidth = IronTerminalPostWidth,
            double lastPostWidth = IronTerminalPostWidth)
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
                double bayStart = horizontalLength * bayIndex / mainPostBayCount;
                double bayEnd = horizontalLength * (bayIndex + 1) / mainPostBayCount;
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
                            ? firstPostWidth
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

                double startPostWidth = bayIndex == 0
                    ? includeStartPost ? firstPostWidth : 0.0
                    : IronMemberThickness;
                double endPostWidth = bayIndex == mainPostBayCount - 1
                    ? includeEndPost ? lastPostWidth : 0.0
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
                    lastPostWidth);
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

        private static void AddFlightHandrail(
            DrawingModel drawing, StairSectionParameters parameters, IReadOnlyList<FlightLayout> flights)
        {
            var centerline = new List<Point2>();
            var height = new Point2(0.0, parameters.RailingHeight);
            for (int index = 0; index < flights.Count; index++)
            {
                FlightLayout flight = flights[index];
                Point2 first = GetRailingBasePoint(flight, parameters, 0) + height;
                Point2 landing = GetRailingBasePoint(flight, parameters, flight.TreadCount) + height;
                double slope = parameters.RiserHeight / parameters.TreadDepth;
                if (index == 0)
                {
                    double extension = IronTerminalPostWidth * 0.5;
                    first -= new Point2(flight.Direction * extension, slope * extension);
                }

                if (index == flights.Count - 1 && parameters.PlanType != StairPlanType.LShape)
                {
                    double extension = IronTerminalPostWidth * 0.5;
                    landing += new Point2(flight.Direction * extension, slope * extension);
                }
                AddVertexIfDistinct(centerline, first);
                AddVertexIfDistinct(centerline, landing);
                if (index + 1 < flights.Count)
                {
                    double turnX = flight.EndX + GetSectionRailingInset(parameters);
                    Point2 next = GetRailingBasePoint(flights[index + 1], parameters, 0) + height;
                    AddVertexIfDistinct(centerline, new Point2(turnX, landing.Y));
                    AddVertexIfDistinct(centerline, new Point2(turnX, next.Y));
                }
            }

            if (parameters.PlanType == StairPlanType.LShape && flights.Count == 1 && flights[0].StartElevation == 0 && !parameters.SectionCut.ReverseView)
            {
                double x = StairPlanGeometry.GetLFlightStartX(flights[0].EndX, parameters.PlanLandingDepth, parameters.PlanStairWidth)
                    + GetSectionRailingInset(parameters);
                AddVertexIfDistinct(centerline, new Point2(x, centerline[centerline.Count - 1].Y));
                AddVertexIfDistinct(centerline, new Point2(x, parameters.FloorHeight + parameters.FinishThickness + parameters.RailingHeight));
            }

            AddContinuousMemberOutline(drawing, centerline, GetSectionHandrailWidth(parameters, flights), true);
        }

        private static double GetSectionRailingInset(StairSectionParameters parameters)
        {
            double width = parameters.PlanStairWidth > 0.0 ? parameters.PlanStairWidth : parameters.LandingLength;
            return StairRailingDimensions.GetInset(width);
        }

        private static double GetSectionHandrailWidth(StairSectionParameters parameters, IReadOnlyList<FlightLayout> flights)
        {
            double width = IronHandrailDiameter;
            if (flights.Count > 1)
            {
                double rise = (parameters.TurnSteps.TotalRisers + 1) * parameters.RiserHeight;
                width = Math.Min(width, rise * 0.8);
            }

            return width;
        }

        private static void AddUTurnRailingPosts(
            DrawingModel drawing, StairSectionParameters parameters, IReadOnlyList<FlightLayout> flights)
        {
            FlightLayout lower = flights[0];
            FlightLayout upper = flights[1];
            double x = lower.EndX + GetSectionRailingInset(parameters);
            double lowerBase = lower.LandingElevation + parameters.FinishThickness;
            double upperBase = upper.StartElevation + parameters.FinishThickness;
            double handrailHalf = GetSectionHandrailWidth(parameters, flights) * 0.5;
            double firstCorner = lowerBase + (parameters.TurnSteps.FirstWinderRisers * parameters.RiserHeight);
            double secondCorner = firstCorner + (parameters.TurnSteps.MiddleFlightRisers * parameters.RiserHeight);
            var supports = new[] { lowerBase, firstCorner, secondCorner, upperBase };
            double intervalStart = supports[0];
            double intervalEnd = intervalStart + parameters.RailingHeight - handrailHalf;
            for (int index = 1; index < supports.Length; index++)
            {
                double top = supports[index] + parameters.RailingHeight - handrailHalf;
                if (index == supports.Length - 1)
                {
                    top += parameters.RiserHeight;
                }

                if (supports[index] > intervalEnd + PointTolerance)
                {
                    AddTurnPostInterval(drawing, parameters, x, intervalStart, intervalEnd);
                    intervalStart = supports[index];
                }

                intervalEnd = Math.Max(intervalEnd, top);
            }

            AddTurnPostInterval(drawing, parameters, x, intervalStart, intervalEnd);
        }

        private static void AddTurnPostInterval(
            DrawingModel drawing, StairSectionParameters parameters, double centerX, double bottom, double top)
        {
            double halfWidth = StairRailingDimensions.TurnPostWidth * 0.5;
            AddIronBoxPost(drawing, centerX - halfWidth, bottom, centerX + halfWidth, bottom, top, top, true);
        }
        private static void AddContinuousMemberOutline(
            DrawingModel drawing,
            IReadOnlyList<Point2> centerline,
            double thickness,
            bool verticalEndCaps = false)
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
                    Point2 normal = verticalEndCaps && Math.Abs(direction.X) > PointTolerance
                        ? new Point2(0.0, halfThickness / direction.X)
                        : new Point2(-direction.Y, direction.X) * halfThickness;
                    firstBoundary.Add(centerline[0] + normal);
                    secondBoundary.Add(centerline[0] - normal);
                    continue;
                }

                if (pointIndex == centerline.Count - 1)
                {
                    Point2 direction = (centerline[pointIndex]
                        - centerline[pointIndex - 1]).Normalize();
                    Point2 normal = verticalEndCaps && Math.Abs(direction.X) > PointTolerance
                        ? new Point2(0.0, halfThickness / direction.X)
                        : new Point2(-direction.Y, direction.X) * halfThickness;
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
