using System;
using System.Collections.Generic;

namespace StairDrawer.Models
{
    internal enum TextAnchor
    {
        BaselineLeft,
        MiddleCenter
    }

    internal sealed class LinePrimitive
    {
        public LinePrimitive(Point2 start, Point2 end, string layerName, double linetypeScale = 1.0)
        {
            if ((end - start).Length <= 1e-9)
            {
                throw new ArgumentException("A line requires two distinct points.", nameof(end));
            }

            if (!NumericValidation.IsFinite(linetypeScale) || linetypeScale <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(linetypeScale));
            }

            if (string.IsNullOrWhiteSpace(layerName))
            {
                throw new ArgumentException("A layer name is required.", nameof(layerName));
            }

            Start = start;
            End = end;
            LayerName = layerName;
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
            double linetypeScale = 1.0,
            bool drawOnTop = false)
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

            for (int vertexIndex = 1; vertexIndex < Vertices.Count; vertexIndex++)
            {
                if ((Vertices[vertexIndex] - Vertices[vertexIndex - 1]).Length <= 1e-9)
                {
                    throw new ArgumentException("Consecutive polyline vertices must be distinct.", nameof(vertices));
                }
            }

            if (closed && Vertices.Count < 3)
            {
                throw new ArgumentException("A closed polyline requires at least three vertices.", nameof(vertices));
            }

            if (closed && (Vertices[0] - Vertices[Vertices.Count - 1]).Length <= 1e-9)
            {
                throw new ArgumentException(
                    "Do not repeat the first vertex when creating a closed polyline.",
                    nameof(vertices));
            }

            if (!NumericValidation.IsFinite(globalWidth) || globalWidth < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(globalWidth));
            }

            if (!NumericValidation.IsFinite(linetypeScale) || linetypeScale <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(linetypeScale));
            }

            if (string.IsNullOrWhiteSpace(layerName))
            {
                throw new ArgumentException("A layer name is required.", nameof(layerName));
            }

            Closed = closed;
            LayerName = layerName;
            GlobalWidth = globalWidth;
            LinetypeScale = linetypeScale;
            DrawOnTop = drawOnTop;
        }

        public IReadOnlyList<Point2> Vertices { get; }

        public bool Closed { get; }

        public string LayerName { get; }

        public double GlobalWidth { get; }

        public double LinetypeScale { get; }

        public bool DrawOnTop { get; }
    }

    internal sealed class CirclePrimitive
    {
        public CirclePrimitive(Point2 center, double radius, string layerName)
        {
            if (!NumericValidation.IsFinite(radius) || radius <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }

            if (string.IsNullOrWhiteSpace(layerName))
            {
                throw new ArgumentException("A layer name is required.", nameof(layerName));
            }

            Center = center;
            Radius = radius;
            LayerName = layerName;
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
            string layerName,
            TextAnchor anchor = TextAnchor.BaselineLeft)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Text content is required.", nameof(text));
            }

            if (!NumericValidation.IsFinite(height) || height <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            if (!NumericValidation.IsFinite(rotation))
            {
                throw new ArgumentOutOfRangeException(nameof(rotation));
            }

            if (string.IsNullOrWhiteSpace(layerName))
            {
                throw new ArgumentException("A layer name is required.", nameof(layerName));
            }

            Position = position;
            Text = text;
            Height = height;
            Rotation = rotation;
            LayerName = layerName;
            Anchor = anchor;
        }

        public Point2 Position { get; }

        public string Text { get; }

        public double Height { get; }

        public double Rotation { get; }

        public string LayerName { get; }

        public TextAnchor Anchor { get; }
    }

    internal sealed class DrawingModel
    {
        public IList<LinePrimitive> Lines { get; } = new List<LinePrimitive>();

        public IList<PolylinePrimitive> Polylines { get; } = new List<PolylinePrimitive>();

        public IList<CirclePrimitive> Circles { get; } = new List<CirclePrimitive>();

        public IList<TextPrimitive> Texts { get; } = new List<TextPrimitive>();
    }
}
