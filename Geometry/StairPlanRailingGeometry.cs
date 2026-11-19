using System;
using System.Collections.Generic;
using StairDrawer.Models;

namespace StairDrawer.Geometry
{
    internal static class StairPlanRailingGeometry
    {
        public static void AppendFixings(DrawingModel drawing, StairPlanParameters p, IReadOnlyList<Point2> route)
        {
            if (p.RailingType != StairRailingType.Glass) return;
            for (int segment = 1; segment < route.Count; segment++)
            {
                Point2 a = route[segment - 1], b = route[segment];
                double length = (b - a).Length;
                Point2 axis = (b - a) / length, normal = new Point2(-axis.Y, axis.X);
                int bays = Math.Max(1, (int)Math.Ceiling(length / (p.TreadDepth * 3)));
                for (int bay = 0; bay < bays; bay++)
                {
                    double first = length * bay / bays, last = length * (bay + 1) / bays;
                    double gap = Math.Min(p.GlassOptions.PanelGap * 0.5 + (p.GlassOptions.HasPosts ? 20 : 0), (last - first) * 0.2);
                    Point2 start = a + axis * (first + gap), end = a + axis * (last - gap);
                    AddStrip(drawing, start, end, normal, p.GlassOptions.Thickness, StairLayers.RailingGlass);
                    if (p.GlassOptions.HasPosts)
                    {
                        Point2 at = a + axis * first;
                        AddStrip(drawing, at - axis * 10, at + axis * 100, normal, 50, StairLayers.Railing);
                        AddStrip(drawing, at - axis * 10, at + axis * 10, normal, 20, StairLayers.Railing);
                        drawing.Circles.Add(new CirclePrimitive(at + axis * 35, 4, StairLayers.Railing));
                        drawing.Circles.Add(new CirclePrimitive(at + axis * 80, 4, StairLayers.Railing));
                    }
                    else if (p.GlassOptions.Mount == GlassMountType.RecessedChannel)
                    {
                        AddStrip(drawing, a + axis * first, a + axis * last, normal, 50, StairLayers.Railing);
                    }
                    else
                    {
                        foreach (double t in new[] { 0.25, 0.75 })
                        {
                            Point2 at = start + (end - start) * t;
                            AddStrip(drawing, at - axis * 25, at + axis * 25, normal, 40, StairLayers.Railing);
                            drawing.Circles.Add(new CirclePrimitive(at, 5, StairLayers.Railing));
                            if (p.GlassOptions.Mount == GlassMountType.DoublePad)
                                drawing.Circles.Add(new CirclePrimitive(at + normal * 12, 5, StairLayers.Railing));
                        }
                    }
                }
            }
            if (p.GlassOptions.HasPosts)
            {
                Point2 at = route[route.Count - 1];
                Point2 axis = (at - route[route.Count - 2]).Normalize(), normal = new Point2(-axis.Y, axis.X);
                AddStrip(drawing, at - axis * 10, at + axis * 100, normal, 50, StairLayers.Railing);
                AddStrip(drawing, at - axis * 10, at + axis * 10, normal, 20, StairLayers.Railing);
                drawing.Circles.Add(new CirclePrimitive(at + axis * 35, 4, StairLayers.Railing));
                drawing.Circles.Add(new CirclePrimitive(at + axis * 80, 4, StairLayers.Railing));
            }
        }

        private static void AddStrip(DrawingModel drawing, Point2 a, Point2 b, Point2 normal, double width, string layer)
        {
            Point2 half = normal * (width * 0.5);
            drawing.Polylines.Add(new PolylinePrimitive(new[] { a - half, b - half, b + half, a + half }, true, layer, drawOnTop: true));
        }
    }
}
