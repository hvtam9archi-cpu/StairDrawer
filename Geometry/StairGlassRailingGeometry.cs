using System;
using System.Collections.Generic;
using System.Linq;
using StairDrawer.Models;

namespace StairDrawer.Geometry
{
    internal static partial class StairSectionGeometry
    {
        private static void AddConnectedGlassRailings(DrawingModel drawing, StairSectionParameters p, IReadOnlyList<FlightLayout> flights, bool firstFlightCut)
        {
            var metal = new DrawingModel();
            AddFlightHandrail(metal, p, flights);
            for (int flightIndex = 0; flightIndex < flights.Count; flightIndex++)
            {
                FlightLayout flight = flights[flightIndex];
                bool showFixings = !firstFlightCut || flightIndex > 0;
                var posts = new List<Point2>();
                for (int first = 0; first < flight.TreadCount; first += GlassPanelTreadSpan)
                {
                    int last = Math.Min(flight.TreadCount, first + GlassPanelTreadSpan);
                    Point2 start = GetRailingBasePoint(flight, p, first), end = GetRailingBasePoint(flight, p, last);
                    AddDetailedGlassBay(drawing, metal, p, start, end, showFixings);
                    if (p.GlassOptions.HasPosts)
                    {
                        if (posts.Count == 0) posts.Add(start);
                        posts.Add(end);
                    }
                }
                for (int i = 0; i < posts.Count; i++)
                    AddGlassPost(drawing, metal, p, posts[i], p.RiserHeight / p.TreadDepth * flight.Direction,
                        i == 0 && flight.StartElevation == 0 || i == posts.Count - 1 && flight.StartElevation > 0
                            ? StairRailingDimensions.TerminalPostWidth : StairRailingDimensions.TurnPostWidth);
            }
            if (flights.Count > 1)
                AddGlassTurnFixings(drawing, metal, p, flights[0], flights[1], false);
            if (p.PlanType == StairPlanType.LShape && flights.Count == 1 && flights[0].StartElevation == 0 && !p.SectionCut.ReverseView)
            {
                double x = StairPlanGeometry.GetLFlightStartX(flights[0].EndX, p.PlanLandingDepth, p.PlanStairWidth)
                    + GetSectionRailingInset(p);
                AddGlassTransverseEdge(drawing, metal, p, x, flights[0].LandingElevation + p.FinishThickness,
                    p.FloorHeight + p.FinishThickness + p.RailingHeight, false);
            }
            RailingOutlineGeometry.Append(drawing, metal.Polylines.ToArray(), true);
        }

        private static void AddDevelopedGlassRailings(DrawingModel drawing, StairSectionParameters p, IReadOnlyList<Point2> pitchLine)
        {
            var metal = new DrawingModel();
            AddContinuousMemberOutline(metal, pitchLine.Select(a => a + new Point2(0, p.RailingHeight)).ToArray(), IronHandrailDiameter);
            // Keep every pitch change at the landing, but group collinear steps into practical glass bays.
            var breaks = new List<Point2> { pitchLine[0] };
            int count = 0;
            for (int i = 1; i < pitchLine.Count; i++)
            {
                bool corner = i + 1 < pitchLine.Count && Math.Abs(Cross2(pitchLine[i] - pitchLine[i - 1], pitchLine[i + 1] - pitchLine[i])) > 1e-7;
                if (++count >= GlassPanelTreadSpan || corner || i == pitchLine.Count - 1)
                { breaks.Add(pitchLine[i]); count = 0; }
            }
            for (int i = 1; i < breaks.Count; i++) AddDetailedGlassBay(drawing, metal, p, breaks[i - 1], breaks[i], false);
            if (p.GlassOptions.HasPosts)
                for (int i = 0; i < breaks.Count; i++)
                {
                    Point2 direction = i + 1 < breaks.Count ? breaks[i + 1] - breaks[i] : breaks[i] - breaks[i - 1];
                    AddGlassPost(drawing, metal, p, breaks[i], direction.Y / direction.X,
                        i == 0 || i == breaks.Count - 1 ? IronTerminalPostWidth : StairRailingDimensions.TurnPostWidth);
                }
            RailingOutlineGeometry.Append(drawing, metal.Polylines.ToArray(), true);
        }

        private static void AddDetailedGlassBay(DrawingModel drawing, DrawingModel metal, StairSectionParameters p, Point2 start, Point2 end, bool showFixings)
        {
            double length = Math.Abs(end.X - start.X);
            if (length < 40) return;
            double direction = Math.Sign(end.X - start.X), slope = (end.Y - start.Y) / (end.X - start.X);
            double gap = p.GlassOptions.PanelGap * 0.5 + (p.GlassOptions.HasPosts ? 20 : 0);
            gap = Math.Min(gap, length * 0.2);
            Point2 a = start + new Point2(direction * gap, direction * gap * slope);
            Point2 b = end - new Point2(direction * gap, direction * gap * slope);
            Point2 topA = a + new Point2(0, p.RailingHeight - GlassHandrailClearance);
            Point2 topB = b + new Point2(0, p.RailingHeight - GlassHandrailClearance);
            Point2 bottomA, bottomB;
            if (p.GlassOptions.HasPosts)
            {
                bottomA = a + new Point2(0, GlassBottomGap); bottomB = b + new Point2(0, GlassBottomGap);
            }
            else
            {
                var bandA = ConcreteBand(drawing, p, a.X, a.Y);
                var bandB = ConcreteBand(drawing, p, b.X, b.Y);
                bool channel = p.GlassOptions.Mount == GlassMountType.RecessedChannel;
                double recess = Math.Min(80, p.WaistSlabThickness * 0.55);
                bottomA = new Point2(a.X, channel ? bandA.Item2 - recess * 0.55 : bandA.Item1 + 8);
                bottomB = new Point2(b.X, channel ? bandB.Item2 - recess * 0.55 : bandB.Item1 + 8);
                if (channel && showFixings)
                {
                    AddMetal(metal, new[] { new Point2(a.X, bandA.Item2 - recess), new Point2(b.X, bandB.Item2 - recess),
                        new Point2(b.X, bandB.Item2 + 8), new Point2(a.X, bandA.Item2 + 8) });
                }
            }
            drawing.Polylines.Add(new PolylinePrimitive(new[] { bottomA, bottomB, topB, topA }, true, StairLayers.RailingGlass, drawOnTop: true));
            if (p.GlassOptions.HasPosts)
            {
                foreach (double level in new[] { 160.0, p.RailingHeight - 150 })
                {
                    AddGlassClamp(drawing, metal, start.X, a.X, a.Y + level, direction);
                    AddGlassClamp(drawing, metal, end.X, b.X, b.Y + level, -direction);
                }
            }
            else if (showFixings)
            {
                foreach (double t in new[] { 0.25, 0.75 })
                {
                    Point2 point = a + (b - a) * t;
                    AddGlassTopBracket(drawing, metal, p, point, slope);
                    var band = ConcreteBand(drawing, p, point.X, point.Y);
                    if (p.GlassOptions.Mount != GlassMountType.RecessedChannel)
                    {
                        double radius = Math.Min(25, Math.Max(5, (band.Item2 - band.Item1 - 12) * 0.24));
                        double center = (band.Item1 + band.Item2) * 0.5;
                        double spread = p.GlassOptions.Mount == GlassMountType.DoublePad
                            ? Math.Min(80, Math.Max(0, band.Item2 - band.Item1 - radius * 2 - 12)) : 0;
                        AddGlassPad(drawing, point.X, center + spread * 0.5, radius);
                        if (spread > 1) AddGlassPad(drawing, point.X, center - spread * 0.5, radius);
                    }
                }
            }
        }

        private static void AddGlassPost(DrawingModel drawing, DrawingModel metal, StairSectionParameters p, Point2 at, double slope, double width)
        {
            double half = width * 0.5, railHalf = IronHandrailDiameter * 0.5 * Math.Sqrt(1 + slope * slope);
            double forward = slope < 0 ? -1 : 1;
            AddMetalBox(metal, Math.Min(at.X - forward * 10, at.X + forward * 100), at.Y,
                Math.Max(at.X - forward * 10, at.X + forward * 100), at.Y + 10);
            AddIronBoxPost(metal, at.X - half, at.Y + 10, at.X + half, at.Y + 10,
                at.Y + p.RailingHeight + slope * half - railHalf,
                at.Y + p.RailingHeight - slope * half - railHalf, true);
        }

        private static void AddGlassClamp(DrawingModel drawing, DrawingModel metal, double postX, double edgeX, double y, double direction)
        {
            double end = edgeX + direction * 24;
            AddMetalBox(metal, Math.Min(postX, end), y - 15, Math.Max(postX, end), y + 15);
            drawing.Circles.Add(new CirclePrimitive(new Point2(edgeX + direction * 12, y), 4, StairLayers.Railing));
        }

        private static void AddGlassTopBracket(DrawingModel drawing, DrawingModel metal, StairSectionParameters p, Point2 at, double slope)
        {
            double glassTop = at.Y + p.RailingHeight - GlassHandrailClearance;
            double railBottom = at.Y + p.RailingHeight - IronHandrailDiameter * 0.5 * Math.Sqrt(1 + slope * slope);
            AddMetalBox(metal, at.X - 18, glassTop - 18, at.X + 18, glassTop + 4);
            AddMetalBox(metal, at.X - 6, glassTop + 4, at.X + 6, Math.Max(glassTop + 5, railBottom + 1));
            drawing.Circles.Add(new CirclePrimitive(new Point2(at.X, glassTop - 8), 4, StairLayers.Railing));
        }

        private static void AddGlassPad(DrawingModel drawing, double x, double y, double radius)
        {
            var boundary = new List<Point2>();
            for (int i = 0; i < 24; i++) boundary.Add(new Point2(x + radius * Math.Cos(i * Math.PI / 12), y + radius * Math.Sin(i * Math.PI / 12)));
            drawing.Wipeouts.Add(new WipeoutPrimitive(boundary, StairLayers.RailingGlass));
            drawing.Circles.Add(new CirclePrimitive(new Point2(x, y), radius, StairLayers.Railing));
            drawing.Circles.Add(new CirclePrimitive(new Point2(x, y), 4, StairLayers.Railing));
        }

        private static Tuple<double, double> ConcreteBand(DrawingModel drawing, StairSectionParameters p, double x, double surface)
        {
            // A visible upper flight has no cut concrete polygon. Never attach its hardware to the lower flight below it.
            double high = surface - p.RiserHeight - p.FinishThickness - TreadMortarThickness;
            double low = high - p.WaistSlabThickness, best = double.MaxValue;
            double maximumGap = p.RiserHeight + p.FinishThickness + TreadMortarThickness + p.WaistSlabThickness * 0.5;
            foreach (PolylinePrimitive polygon in drawing.Polylines.Where(a => a.LayerName == StairLayers.Concrete && a.Closed))
            {
                var hits = new List<double>();
                for (int i = 0; i < polygon.Vertices.Count; i++)
                {
                    Point2 a = polygon.Vertices[i], b = polygon.Vertices[(i + 1) % polygon.Vertices.Count];
                    if (Math.Abs(b.X - a.X) < 1e-8) { if (Math.Abs(x - a.X) < 1e-6) { hits.Add(a.Y); hits.Add(b.Y); } continue; }
                    double t = (x - a.X) / (b.X - a.X);
                    if (t >= -1e-8 && t <= 1 + 1e-8) hits.Add(a.Y + t * (b.Y - a.Y));
                }
                hits.Sort();
                if (hits.Count < 2) continue;
                double top = hits[hits.Count - 1], bottom = hits[0];
                if (top <= surface + 1e-6 && surface - top <= maximumGap && surface - top < best && top - bottom > 1)
                { best = surface - top; low = bottom; high = top; }
            }
            return Tuple.Create(low, high);
        }

        private static void AddGlassTurnFixings(DrawingModel drawing, DrawingModel metal, StairSectionParameters p, FlightLayout lower, FlightLayout upper, bool showFixings)
        {
            double x = lower.EndX + GetSectionRailingInset(p);
            double bottom = lower.LandingElevation + p.FinishThickness;
            double top = upper.StartElevation + p.RiserHeight + p.FinishThickness + p.RailingHeight;
            AddGlassTransverseEdge(drawing, metal, p, x, bottom, top, false, showFixings);
        }

        private static void AddProjectedTurnConnection(DrawingModel drawing, StairSectionParameters p, FlightLayout lower, FlightLayout upper)
        {
            var metal = new DrawingModel();
            Point2 a = GetRailingBasePoint(lower, p, lower.TreadCount) + new Point2(0, p.RailingHeight);
            Point2 b = GetRailingBasePoint(upper, p, 0) + new Point2(0, p.RailingHeight);
            double x = lower.EndX + GetSectionRailingInset(p);
            AddContinuousMemberOutline(metal, new[] { a, new Point2(x, a.Y), new Point2(x, b.Y), b }, IronHandrailDiameter, true);
            if (p.RailingType == StairRailingType.Glass) AddGlassTurnFixings(drawing, metal, p, lower, upper, false);
            else AddUTurnRailingPosts(metal, p, new[] { lower, upper });
            RailingOutlineGeometry.Append(drawing, metal.Polylines.ToArray(), true);
        }

        private static void AddGlassTransverseEdge(DrawingModel drawing, DrawingModel metal, StairSectionParameters p,
            double x, double bottom, double railTop, bool handrail, bool showFixings = true)
        {
            double half = p.GlassOptions.Thickness * 0.5;
            bool channel = !p.GlassOptions.HasPosts && p.GlassOptions.Mount == GlassMountType.RecessedChannel;
            double start = p.GlassOptions.HasPosts ? bottom + GlassBottomGap : bottom - Math.Min(80, p.WaistSlabThickness * 0.55);
            double top = railTop - GlassHandrailClearance;
            if (top > start) drawing.Polylines.Add(new PolylinePrimitive(new[] {
                new Point2(x - half, start), new Point2(x + half, start), new Point2(x + half, top), new Point2(x - half, top)
            }, true, StairLayers.RailingGlass, drawOnTop: true));
            if (handrail) AddMetalBox(metal, x - 25, bottom + p.RailingHeight, x + 25, railTop + 25);
            if (p.GlassOptions.HasPosts)
            {
                AddMetalBox(metal, x - 10, bottom, x + 10, railTop - 25);
                AddMetalBox(metal, x - 40, bottom, x + 40, bottom + 10);
                foreach (double y in new[] { bottom + 160, railTop - 150 }) AddMetalBox(metal, x - half - 8, y - 15, x + half + 8, y + 15);
            }
            else if (channel && showFixings)
            {
                AddMetalBox(metal, x - 25, start - 10, x + 25, bottom);
            }
            else if (showFixings)
            {
                var band = ConcreteBand(drawing, p, x, bottom);
                double radius = Math.Min(25, Math.Max(5, (band.Item2 - band.Item1 - 12) * 0.24));
                double center = (band.Item1 + band.Item2) * 0.5;
                double spread = p.GlassOptions.Mount == GlassMountType.DoublePad
                    ? Math.Min(80, Math.Max(0, band.Item2 - band.Item1 - radius * 2 - 12)) : 0;
                AddMetalBox(metal, x - 25, center + spread * 0.5 - radius, x + 25, center + spread * 0.5 + radius);
                if (spread > 1) AddMetalBox(metal, x - 25, center - spread * 0.5 - radius, x + 25, center - spread * 0.5 + radius);
            }
        }

        private static void AddMetalBox(DrawingModel drawing, double left, double bottom, double right, double top)
        {
            if (right - left <= 1e-7 || top - bottom <= 1e-7) return;
            AddMetal(drawing, new[] { new Point2(left, bottom), new Point2(right, bottom), new Point2(right, top), new Point2(left, top) });
        }

        private static void AddMetal(DrawingModel drawing, IEnumerable<Point2> points) =>
            drawing.Polylines.Add(new PolylinePrimitive(points, true, StairLayers.Railing, drawOnTop: true));

        private static double Cross2(Point2 a, Point2 b) => a.X * b.Y - a.Y * b.X;

        private static void NormalizeConnectedRailing(DrawingModel drawing)
        {
            var masks = drawing.Wipeouts.Where(a => a.LayerName == StairLayers.Railing).ToArray();
            if (masks.Length == 0) return;
            var members = masks.Select(a => new PolylinePrimitive(a.Boundary, true, StairLayers.Railing)).ToArray();
            var covers = drawing.Polylines.Where(a => a.LayerName == StairLayers.Railing && !a.OccludesBackground && a.Closed).ToArray();
            foreach (PolylinePrimitive outline in drawing.Polylines.Where(a => a.LayerName == StairLayers.Railing && a.OccludesBackground).ToArray()) drawing.Polylines.Remove(outline);
            foreach (WipeoutPrimitive mask in masks) drawing.Wipeouts.Remove(mask);
            RailingOutlineGeometry.Append(drawing, members, true, covers);
        }
    }
}
