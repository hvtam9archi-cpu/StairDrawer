using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StairDrawer.Models;

namespace StairDrawer.Geometry
{
    internal sealed class StairDetailTread
    {
        public Point2 TopFront { get; private set; }
        public Point2 TopBack { get; private set; }
        public Point2 BottomFront { get; private set; }
        public Point2 Up { get; private set; }
        public Point2 Along { get; private set; }
        public double Thickness { get; private set; }
        public double Width => (TopBack - TopFront).Length;
        public Point2 Nosing => BottomFront + Up * Thickness;
        public double FrameAngle => Math.Atan2(-Up.X, Up.Y);

        public static bool TryCreate(IReadOnlyList<Point2> points, out StairDetailTread tread)
        {
            tread = null;
            if (points.Count != 4 && points.Count != 5) return false;
            int topIndex = points.Count - 3;
            Point2 front = points[topIndex], back = points[topIndex + 1], bottomBack = points[topIndex + 2];
            double thickness = (back - bottomBack).Length, width = (back - front).Length;
            if (thickness < 0.01 || width < thickness * 1.5) return false;
            Point2 up = (back - bottomBack) / thickness, along = (back - front) / width;
            if (Math.Abs(Dot(up, along)) > 1e-5) return false;
            Point2 bevel = front - points[0];
            double bevelWidth = Dot(bevel, along);
            if (Math.Abs(Dot(bevel, up) - thickness) > thickness * 1e-5
                || bevelWidth < thickness * 1e-6 || bevelWidth > thickness * 1.01) return false;
            tread = new StairDetailTread { TopFront = front, TopBack = back, BottomFront = points[0],
                Up = up, Along = along, Thickness = thickness };
            return true;
        }

        internal static double Dot(Point2 a, Point2 b) => a.X * b.X + a.Y * b.Y;
    }

    internal sealed class StairAutomaticDetailSelection
    {
        public IReadOnlyList<StairDetailTread> Treads { get; set; }
        public Point2 TreadCenter { get; set; }
        public double TreadRadius { get; set; }
        public Point2 NosingCenter { get; set; }
        public double NosingRadius { get; set; }
        public double FrameAngle { get; set; }
        public double StoneThickness { get; set; }
        public double Pitch { get; set; }
        public double Rise { get; set; }
    }

    internal static class StairAutomaticDetailGeometry
    {
        public static bool HasCutMaterial(StairDetailTread tread, IEnumerable<IReadOnlyList<Point2>> bricks)
        {
            // Visible flights have the same beveled stone outlines, but no nearby cut brick below them.
            double level = StairDetailTread.Dot(tread.BottomFront, tread.Up);
            foreach (IReadOnlyList<Point2> brick in bricks)
            foreach (double fraction in new[] { .35, .65 })
            {
                double station = StairDetailTread.Dot(tread.BottomFront + tread.Along * (tread.Width * fraction), tread.Along);
                var heights = new List<double>();
                for (int i = 0; i < brick.Count; i++)
                {
                    Point2 a = brick[i], b = brick[(i + 1) % brick.Count];
                    double ax = StairDetailTread.Dot(a, tread.Along), bx = StairDetailTread.Dot(b, tread.Along);
                    if (Math.Abs(ax - bx) < 1e-8) continue;
                    double t = (station - ax) / (bx - ax);
                    if (t >= 0 && t <= 1) heights.Add(StairDetailTread.Dot(a + (b - a) * t, tread.Up));
                }
                if (heights.Count >= 2 && level > heights.Max() && level - heights.Max() < tread.Width * .5) return true;
            }
            return false;
        }

        public static StairAutomaticDetailSelection Select(IReadOnlyList<StairDetailTread> treads, double slabThickness)
        {
            var runs = new List<List<StairDetailTread>>();
            foreach (var family in treads.GroupBy(t => Key(t.Up) + "/" + Key(t.Along)))
            {
                Point2 along = family.First().Along, up = family.First().Up;
                var ordered = family.OrderBy(t => StairDetailTread.Dot(t.Nosing, along)).ToList();
                if (ordered.Count < 2) continue;
                var pairs = ordered.Zip(ordered.Skip(1), (a, b) => b.Nosing - a.Nosing)
                    .Where(d => StairDetailTread.Dot(d, along) > .01 && StairDetailTread.Dot(d, up) > .01).ToArray();
                if (pairs.Length == 0) continue;
                double pitch = Median(pairs.Select(d => StairDetailTread.Dot(d, along)));
                double rise = Median(pairs.Select(d => StairDetailTread.Dot(d, up)));
                var current = new List<StairDetailTread>();
                foreach (StairDetailTread tread in ordered)
                {
                    if (current.Count > 0)
                    {
                        Point2 delta = tread.Nosing - current[current.Count - 1].Nosing;
                        if (Math.Abs(StairDetailTread.Dot(delta, along) - pitch) > Math.Max(.01, pitch * .15)
                            || Math.Abs(StairDetailTread.Dot(delta, up) - rise) > Math.Max(.01, rise * .15))
                        { if (current.Count >= 2) runs.Add(current); current = new List<StairDetailTread>(); }
                    }
                    current.Add(tread);
                }
                if (current.Count >= 2) runs.Add(current);
            }
            var run = runs.OrderByDescending(r => r.Count).ThenBy(r => r.Min(t => t.Nosing.Y)).FirstOrDefault();
            if (run == null) throw new ArgumentException("Mặt cắt cần có ít nhất hai bậc cắt dọc có cấu tạo đá để tự trích chi tiết.");
            int count = Math.Min(3, run.Count), first = (run.Count - count) / 2;
            var chosen = run.Skip(first).Take(count).ToArray();
            StairDetailTread middle = chosen[chosen.Length / 2];
            Point2 deltaStep = chosen[1].Nosing - chosen[0].Nosing;
            double treadPitch = StairDetailTread.Dot(deltaStep, middle.Along), treadRise = StairDetailTread.Dot(deltaStep, middle.Up);
            double stone = Median(chosen.Select(t => t.Thickness));
            Point2 center = (chosen[0].Nosing + chosen[chosen.Length - 1].Nosing) * .5
                - middle.Up * ((treadRise + slabThickness) * .2);
            double radius = chosen.Max(t => (t.Nosing - center).Length) / .85 + stone * 1.5;
            Point2 concreteBottom = middle.Nosing - middle.Up * (treadRise + slabThickness + stone * 2);
            radius = Math.Max(radius, (concreteBottom - center).Length + stone);
            radius = Math.Max(20, Math.Ceiling(radius / 10) * 10);
            double noseRadius = Math.Max(20, Math.Ceiling(Math.Max(stone * 2.5, Math.Min(treadPitch, treadRise) * .55) / 10) * 10);
            if (radius > 5000 || noseRadius > 5000) throw new ArgumentException("Kích thước hình học vượt phạm vi trích chi tiết tự động theo milimét.");
            // Prefer a separate, interior nosing so the two source callouts do not cover one another.
            var noses = run.Count > 3 ? run.Skip(1).Take(run.Count - 2).ToList() : run;
            StairDetailTread nose = noses.OrderByDescending(t => (t.BottomFront - center).Length).First();
            return new StairAutomaticDetailSelection { Treads = chosen, TreadCenter = center, TreadRadius = radius,
                NosingCenter = nose.BottomFront, NosingRadius = noseRadius, FrameAngle = middle.FrameAngle,
                StoneThickness = stone, Pitch = treadPitch, Rise = treadRise };
        }

        private static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(x => x).ToArray();
            return sorted[sorted.Length / 2];
        }
        private static string Key(Point2 point) => Math.Round(point.X, 5).ToString(CultureInfo.InvariantCulture)
            + "," + Math.Round(point.Y, 5).ToString(CultureInfo.InvariantCulture);
    }
}
