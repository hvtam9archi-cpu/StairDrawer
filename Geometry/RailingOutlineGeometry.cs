using System;
using System.Collections.Generic;
using System.Globalization;
using StairDrawer.Models;

namespace StairDrawer.Geometry
{
    // Draw the outside of joined members, removing seams at welds and lines hidden by nearer members.
    internal static class RailingOutlineGeometry
    {
        private const double Tolerance = 1e-7;

        public static void Append(
            DrawingModel drawing, IReadOnlyList<PolylinePrimitive> members, bool foreground,
            IReadOnlyList<PolylinePrimitive> covers = null)
        {
            var edges = new List<Edge>();
            var unique = new HashSet<string>();
            for (int polygonIndex = 0; polygonIndex < members.Count; polygonIndex++)
            {
                IReadOnlyList<Point2> polygon = members[polygonIndex].Vertices;
                for (int index = 0; index < polygon.Count; index++)
                {
                    Point2 start = polygon[index];
                    Point2 end = polygon[(index + 1) % polygon.Count];
                    Point2 direction = end - start;
                    double length = direction.Length;
                    if (length <= Tolerance)
                    {
                        continue;
                    }

                    var splits = new List<double> { 0.0, 1.0 };
                    AddIntersections(splits, start, end, members);
                    if (covers != null)
                    {
                        AddIntersections(splits, start, end, covers);
                    }

                    splits.Sort();
                    Point2 normal = new Point2(-direction.Y, direction.X) / length;
                    for (int split = 1; split < splits.Count; split++)
                    {
                        double first = splits[split - 1];
                        double last = splits[split];
                        if ((last - first) * length <= Tolerance)
                        {
                            continue;
                        }

                        Point2 middle = start + (direction * ((first + last) * 0.5));
                        if (covers != null && ContainsAny(covers, middle, true))
                        {
                            continue;
                        }

                        double probe = Math.Min(0.0001, (last - first) * length * 0.01);
                        bool leftInside = ContainsAny(members, middle + (normal * probe), false);
                        bool rightInside = ContainsAny(members, middle - (normal * probe), false);
                        if (leftInside == rightInside)
                        {
                            continue;
                        }

                        Point2 a = start + (direction * first);
                        Point2 b = start + (direction * last);
                        var edge = leftInside ? new Edge(a, b) : new Edge(b, a);
                        string key = string.CompareOrdinal(edge.StartKey, edge.EndKey) < 0
                            ? edge.StartKey + "/" + edge.EndKey : edge.EndKey + "/" + edge.StartKey;
                        if (unique.Add(key))
                        {
                            edges.Add(edge);
                        }
                    }
                }
            }

            if (foreground)
            {
                foreach (PolylinePrimitive member in members)
                {
                    drawing.Wipeouts.Add(new WipeoutPrimitive(member.Vertices, StairLayers.Railing));
                }
            }

            AppendChains(drawing, edges, foreground);
        }

        private static void AddIntersections(
            IList<double> splits, Point2 start, Point2 end, IReadOnlyList<PolylinePrimitive> polygons)
        {
            Point2 direction = end - start;
            double lengthSquared = Dot(direction, direction);
            double length = Math.Sqrt(lengthSquared);
            foreach (PolylinePrimitive polygon in polygons)
            {
                for (int index = 0; index < polygon.Vertices.Count; index++)
                {
                    Point2 a = polygon.Vertices[index];
                    Point2 b = polygon.Vertices[(index + 1) % polygon.Vertices.Count];
                    Point2 other = b - a;
                    double cross = Cross(direction, other);
                    if (Math.Abs(cross) > 1e-10 * length * other.Length)
                    {
                        double t = Cross(a - start, other) / cross;
                        double u = Cross(a - start, direction) / cross;
                        if (u >= -1e-10 && u <= 1.0 + 1e-10)
                        {
                            AddSplit(splits, t);
                        }
                    }
                    else if (Math.Abs(Cross(a - start, direction)) <= Tolerance * length)
                    {
                        AddSplit(splits, Dot(a - start, direction) / lengthSquared);
                        AddSplit(splits, Dot(b - start, direction) / lengthSquared);
                    }
                }
            }
        }

        private static void AddSplit(IList<double> splits, double value)
        {
            if (value >= -1e-10 && value <= 1.0 + 1e-10)
            {
                splits.Add(Math.Max(0.0, Math.Min(1.0, value)));
            }
        }

        private static bool ContainsAny(IReadOnlyList<PolylinePrimitive> polygons, Point2 point, bool includeBoundary)
        {
            foreach (PolylinePrimitive polygon in polygons)
            {
                bool inside = false;
                for (int index = 0; index < polygon.Vertices.Count; index++)
                {
                    Point2 a = polygon.Vertices[index];
                    Point2 b = polygon.Vertices[(index + 1) % polygon.Vertices.Count];
                    Point2 direction = b - a;
                    if (includeBoundary && Math.Abs(Cross(point - a, direction)) <= Tolerance * direction.Length
                        && Dot(point - a, point - b) <= Tolerance)
                    {
                        return true;
                    }

                    if ((a.Y > point.Y) != (b.Y > point.Y)
                        && point.X < a.X + ((point.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y)))
                    {
                        inside = !inside;
                    }
                }

                if (inside)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendChains(DrawingModel drawing, IList<Edge> edges, bool foreground)
        {
            var outgoing = new Dictionary<string, List<Edge>>();
            foreach (Edge edge in edges)
            {
                if (!outgoing.TryGetValue(edge.StartKey, out List<Edge> connected))
                {
                    connected = new List<Edge>();
                    outgoing.Add(edge.StartKey, connected);
                }

                connected.Add(edge);
            }

            foreach (Edge first in edges)
            {
                if (first.Used)
                {
                    continue;
                }

                var points = new List<Point2> { first.Start };
                Edge current = first;
                bool closed = false;
                while (current != null)
                {
                    current.Used = true;
                    if (current.EndKey == first.StartKey)
                    {
                        closed = true;
                        break;
                    }

                    points.Add(current.End);
                    Edge next = null;
                    double bestTurn = double.NegativeInfinity;
                    if (outgoing.TryGetValue(current.EndKey, out List<Edge> choices))
                    {
                        Point2 incoming = current.End - current.Start;
                        foreach (Edge choice in choices)
                        {
                            if (choice.Used)
                            {
                                continue;
                            }

                            Point2 direction = choice.End - choice.Start;
                            double turn = Math.Atan2(Cross(incoming, direction), Dot(incoming, direction));
                            if (turn > bestTurn)
                            {
                                next = choice;
                                bestTurn = turn;
                            }
                        }
                    }

                    current = next;
                }

                if (points.Count >= (closed ? 3 : 2))
                {
                    drawing.Polylines.Add(new PolylinePrimitive(
                        points, closed, StairLayers.Railing, drawOnTop: true, occludesBackground: foreground));
                }
            }
        }

        private static double Cross(Point2 a, Point2 b) => (a.X * b.Y) - (a.Y * b.X);
        private static double Dot(Point2 a, Point2 b) => (a.X * b.X) + (a.Y * b.Y);
        private static string Key(Point2 point) => Math.Round(point.X, 6).ToString("F6", CultureInfo.InvariantCulture)
            + ":" + Math.Round(point.Y, 6).ToString("F6", CultureInfo.InvariantCulture);

        private sealed class Edge
        {
            public Edge(Point2 start, Point2 end)
            {
                Start = start;
                End = end;
                StartKey = Key(start);
                EndKey = Key(end);
            }

            public Point2 Start { get; }
            public Point2 End { get; }
            public string StartKey { get; }
            public string EndKey { get; }
            public bool Used { get; set; }
        }
    }
}
