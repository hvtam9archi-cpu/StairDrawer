using System;
using System.Collections.Generic;
using System.Linq;
using StairDrawer.Geometry;
using StairDrawer.Models;

internal static class AutomaticDetailChecks
{
    public static void Run()
    {
        int cases = 0;
        foreach (StairPlanType shape in Enum.GetValues(typeof(StairPlanType)))
        foreach (double finish in new[] { 5.0, 20, 30, 100 })
        foreach (double pitch in new[] { 250.0, 300 })
        foreach (double angle in new[] { 0.0, .47, Math.PI, -1.2 })
        foreach (double scale in new[] { 1.0, 2 })
        foreach (bool mirror in new[] { false, true })
        {
            var parameters = new StairSectionParameters(shape, 3600, 20, shape == StairPlanType.Straight ? 20 : 10,
                pitch, 1000, 120, finish, false, StairRailingType.Iron, 900, default, 1000, 1400);
            DrawingModel drawing = StairSectionGeometry.Create(parameters);
            Point2 Transform(Point2 p)
            {
                double x = p.X * scale, y = p.Y * scale * (mirror ? -1 : 1);
                return new Point2(15000 + Math.Cos(angle) * x - Math.Sin(angle) * y, 7000 + Math.Sin(angle) * x + Math.Cos(angle) * y);
            }
            var bricks = drawing.Polylines.Where(p => p.Closed && p.LayerName == StairLayers.Brick)
                .Select(p => (IReadOnlyList<Point2>)p.Vertices.Select(Transform).ToArray()).ToArray();
            var candidates = new List<StairDetailTread>();
            foreach (PolylinePrimitive polyline in drawing.Polylines.Where(p => p.Closed && p.LayerName == StairLayers.Finish))
                if (StairDetailTread.TryCreate(polyline.Vertices.Select(Transform).ToArray(), out StairDetailTread tread)
                    && StairAutomaticDetailGeometry.HasCutMaterial(tread, bricks)) candidates.Add(tread);
            var selection = StairAutomaticDetailGeometry.Select(candidates, 120 * scale);
            Need(selection.Treads.Count == 3, "Automatic detail does not select three adjacent treads");
            Need(selection.Treads.All(t => StairAutomaticDetailGeometry.HasCutMaterial(t, bricks)), "Automatic detail selected an uncut flight");
            Need(Math.Abs(selection.Pitch - pitch * scale) < 1e-5, "Automatic pitch differs from geometry");
            Need(Math.Abs(selection.Rise - 180 * scale) < 1e-5, "Automatic rise differs from geometry");
            Need(Math.Abs(selection.StoneThickness - finish * scale) < 1e-5, "Automatic stone thickness differs from geometry");
            Need(selection.Treads.All(t => (t.Nosing - selection.TreadCenter).Length < selection.TreadRadius * .9), "Selected tread falls outside its dimension area");
            Need(candidates.Any(t => (t.BottomFront - selection.NosingCenter).Length < 1e-5), "Nosing circle is not attached to an actual nosing");
            Need(selection.NosingRadius < selection.TreadRadius, "Nosing detail is not a tighter crop");
            cases++;
        }
        bool rejected = false;
        try { StairAutomaticDetailGeometry.Select(new StairDetailTread[0], 120); } catch (ArgumentException) { rejected = true; }
        Need(rejected, "Unsupported source must not generate invented details");
        Console.WriteLine("PASS: " + cases + " automatic selections across shapes, thicknesses, rotations, scales and mirrored sections.");
    }

    private static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
