using System;
using System.Collections.Generic;

namespace StairDrawer.Models
{
    internal sealed class LinePrimitive
    {
        public LinePrimitive(Point2 start, Point2 end, string layerName, double linetypeScale = 1.0)
        {
            if (linetypeScale <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(linetypeScale));
            }

            Start = start;
            End = end;
            LayerName = layerName ?? throw new ArgumentNullException(nameof(layerName));
            LinetypeScale = linetypeScale;
        }

        public Point2 Start { get; }

        public Point2 End { get; }

        public string LayerName { get; }

        public double LinetypeScale { get; }
    }

    internal sealed class PolylinePrimitive
    {
        public PolylinePrimitive(
            IEnumerable<Point2> vertices,
            bool closed,
            string layerName,
            double globalWidth = 0.0,
            double linetypeScale = 1.0)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            Vertices = new List<Point2>(vertices);
            if (Vertices.Count < 2)
            {
                throw new ArgumentException("A polyline requires at least two vertices.", nameof(vertices));
            }

            if (globalWidth < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(globalWidth));
            }

            if (linetypeScale <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(linetypeScale));
            }

            Closed = closed;
            LayerName = layerName ?? throw new ArgumentNullException(nameof(layerName));
            GlobalWidth = globalWidth;
            LinetypeScale = linetypeScale;
        }

        public IReadOnlyList<Point2> Vertices { get; }

        public bool Closed { get; }

        public string LayerName { get; }

        public double GlobalWidth { get; }

        public double LinetypeScale { get; }
    }

    internal sealed class CirclePrimitive
    {
        public CirclePrimitive(Point2 center, double radius, string layerName)
        {
            if (radius <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            Center = center;
            Radius = radius;
            LayerName = layerName ?? throw new ArgumentNullException(nameof(layerName));
        }

        public Point2 Center { get; }

        public double Radius { get; }

        public string LayerName { get; }
    }

    internal sealed class TextPrimitive
    {
        public TextPrimitive(
            Point2 position,
            string text,
            double height,
            double rotation,
            string layerName)
        {
            Position = position;
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Height = height > 0.0 ? height : throw new ArgumentOutOfRangeException(nameof(height));
            Rotation = rotation;
            LayerName = layerName ?? throw new ArgumentNullException(nameof(layerName));
        }

        public Point2 Position { get; }

        public string Text { get; }

        public double Height { get; }

        public double Rotation { get; }

        public string LayerName { get; }
    }

    internal sealed class DrawingModel
    {
        public IList<LinePrimitive> Lines { get; } = new List<LinePrimitive>();

        public IList<PolylinePrimitive> Polylines { get; } = new List<PolylinePrimitive>();

        public IList<CirclePrimitive> Circles { get; } = new List<CirclePrimitive>();

        public IList<TextPrimitive> Texts { get; } = new List<TextPrimitive>();
    }
}
