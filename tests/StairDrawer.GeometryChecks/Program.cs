using System;
using System.Linq;
using StairDrawer.Geometry;
using StairDrawer.Models;

internal static class Program
{
    private static void Main()
    {
        var options = new[] { new GlassRailingOptions(),
            new GlassRailingOptions(GlassRailingType.Frameless, GlassMountType.SinglePad),
            new GlassRailingOptions(GlassRailingType.Frameless, GlassMountType.DoublePad),
            new GlassRailingOptions(GlassRailingType.Frameless, GlassMountType.RecessedChannel) };
        int count = 0;
        foreach (StairPlanType type in Enum.GetValues(typeof(StairPlanType)))
        foreach (bool landing in type == StairPlanType.Straight ? new[] { false, true } : new[] { false })
        foreach (UTurnSteps turn in type == StairPlanType.UShape ? new[] { default(UTurnSteps), new UTurnSteps(3, 0, 0), new UTurnSteps(2, 3, 2) } : new[] { default(UTurnSteps) })
        foreach (double cut in new[] { .12, .5, .84 })
        foreach (bool reverse in new[] { false, true })
        foreach (GlassRailingOptions glass in options)
        {
            int first = type == StairPlanType.Straight && !landing ? 20 : 10;
            var p = new StairSectionParameters(type, 3600, 20 + (int)turn.TotalRisers, first, 250, 1000, 120, 20,
                true, StairRailingType.Glass, 900, turn, 1000, 1400, landing, 100, new StairCutSelection(cut, reverse)).WithGlassOptions(glass);
            DrawingModel model = StairSectionGeometry.Create(p);
            string context = type + "/" + turn.TotalRisers + "/" + cut + "/" + reverse + "/" + glass.Type + "/" + glass.Mount;
            Require(!model.Lines.Any(a => a.LayerName == StairLayers.RailingGlass), "Obsolete glass diagonal: " + context);
            Require(model.Lines.Where(a => a.LayerName == StairLayers.Railing).All(a => (a.End - a.Start).Length <= p.RiserHeight + p.WaistSlabThickness + p.FinishThickness + 80),
                "A fixing crosses the gap into another flight: " + context);
            foreach (PolylinePrimitive polyline in model.Polylines)
            {
                Require(polyline.Vertices.All(a => NumericValidation.IsFinite(a.X) && NumericValidation.IsFinite(a.Y)), "Non-finite geometry: " + context);
                if (polyline.Closed) Require(Math.Abs(Area(polyline)) > 1e-5 && !SelfCrosses(polyline), "Invalid closed boundary: " + context);
                if (polyline.LayerName == StairLayers.RailingGlass && polyline.Closed && polyline.Vertices.Count == 4
                    && polyline.Vertices.Max(a => a.X) - polyline.Vertices.Min(a => a.X) > 40)
                    Require(Math.Abs(polyline.Vertices[0].Y - polyline.Vertices[3].Y) <= p.RailingHeight + p.RiserHeight + p.WaistSlabThickness + 100,
                        "Upper glass extends down to another flight: " + context);
            }
            if (type == StairPlanType.UShape && !reverse) CheckUJoin(model, p);
            var plan = new StairPlanParameters(type, 3600, p.TotalRisers, first, 250, 1000, 1400, 100, 1200,
                turn, landing, true, p.SectionCut).WithRailingOptions(StairRailingType.Glass, glass);
            DrawingModel planModel = StairPlanGeometry.Create(plan);
            Require(planModel.Polylines.Any(a => a.LayerName == StairLayers.RailingGlass), "No glass in plan: " + context);
            Require(plan.GlassOptions == p.GlassOptions, "Plan/section construction differs");
            count++;
        }
        foreach (StairRailingType rail in Enum.GetValues(typeof(StairRailingType)))
        {
            var p = new StairSectionParameters(StairPlanType.LShape, 3600, 20, 10, 250, 1000, 120, 20, true, rail, 900, default, 1000, 1400);
            DrawingModel model = StairSectionGeometry.Create(p);
            for (int i = 0; i <= 10; i++)
            {
                double t = i / 10.0;
                Require(InMetal(model, new Point2(2250 + 275 * t, 2720)), "L landing rail is disconnected: " + rail);
                Require(InMetal(model, new Point2(2525, 2720 + 1800 * t)), "L upper rail is disconnected: " + rail);
            }
        }
        var original = new StairSectionParameters(StairPlanType.Straight, 3600, 20, 20, 250, 1000, 120, 20, true, StairRailingType.Glass, 900);
        DrawingModel single = StairSectionGeometry.Create(original.WithGlassOptions(options[1]));
        DrawingModel dual = StairSectionGeometry.Create(original.WithGlassOptions(options[2]));
        Require(dual.Circles.Count(a => a.Radius > 8) == 2 * single.Circles.Count(a => a.Radius > 8), "Double-pad hardware is not doubled");
        Require(original.GlassOptions.HasPosts, "Changing glass options mutated an existing configuration");
        Console.WriteLine("PASS: " + count + " glass plan/section cases; U/L joins, local anchors, pad multiplicity and immutable configurations.");
    }

    private static void CheckUJoin(DrawingModel model, StairSectionParameters p)
    {
        double end = (p.FirstFlightRisers - 1) * p.TreadDepth, x = end + 75;
        double a = p.FirstFlightRisers * p.RiserHeight + p.FinishThickness + p.RailingHeight;
        double b = (p.FirstFlightRisers + p.TurnSteps.TotalRisers + 1) * p.RiserHeight + p.FinishThickness + p.RailingHeight;
        for (int i = 0; i <= 10; i++)
        {
            double t = i / 10.0;
            Require(InMetal(model, new Point2(end + 75 * t, a)), "Lower U rail is disconnected");
            Require(InMetal(model, new Point2(x, a + (b - a) * t)), "U turn rail is disconnected");
            Require(InMetal(model, new Point2(x - 75 * t, b)), "Upper U rail is disconnected");
        }
    }

    private static bool InMetal(DrawingModel model, Point2 point) => model.Wipeouts.Where(a => a.LayerName == StairLayers.Railing).Any(a =>
    {
        bool inside = false;
        for (int i = 0, j = a.Boundary.Count - 1; i < a.Boundary.Count; j = i++)
        {
            Point2 x = a.Boundary[i], y = a.Boundary[j], d = y - x;
            double t = Math.Max(0, Math.Min(1, Dot(point - x, d) / Dot(d, d)));
            if ((point - x - d * t).Length < 1e-6) return true;
            if ((x.Y > point.Y) != (y.Y > point.Y) && point.X < x.X + (point.Y - x.Y) * (y.X - x.X) / (y.Y - x.Y)) inside = !inside;
        }
        return inside;
    });

    private static double Area(PolylinePrimitive p)
    {
        double area = 0;
        for (int i = 0; i < p.Vertices.Count; i++) area += Cross(p.Vertices[i], p.Vertices[(i + 1) % p.Vertices.Count]);
        return area * 0.5;
    }

    private static bool SelfCrosses(PolylinePrimitive p)
    {
        int n = p.Vertices.Count;
        for (int i = 0; i < n; i++) for (int j = i + 2; j < n; j++)
        {
            if ((j + 1) % n == i) continue;
            Point2 a = p.Vertices[i], b = p.Vertices[(i + 1) % n], c = p.Vertices[j], d = p.Vertices[(j + 1) % n];
            double denominator = Cross(b - a, d - c);
            if (Math.Abs(denominator) < 1e-8) continue;
            double t = Cross(c - a, d - c) / denominator, u = Cross(c - a, b - a) / denominator;
            if (t > 1e-7 && t < 1 - 1e-7 && u > 1e-7 && u < 1 - 1e-7) return true;
        }
        return false;
    }

    private static double Dot(Point2 a, Point2 b) => a.X * b.X + a.Y * b.Y;
    private static double Cross(Point2 a, Point2 b) => a.X * b.Y - a.Y * b.X;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
