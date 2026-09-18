using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using StairDrawer.Interaction;
using StairDrawer.Models;
using AutoCADException = Autodesk.AutoCAD.Runtime.Exception;
using SystemTrace = System.Diagnostics.Trace;

namespace StairDrawer.Services
{
    internal sealed class StairDrawingService
    {
        private static readonly LayerDefinition[] LayerDefinitions =
        {
            new LayerDefinition(StairLayers.PlanVisible, 7, LineWeight.LineWeight030, "Continuous"),
            new LayerDefinition(StairLayers.PlanHidden, 8, LineWeight.LineWeight015, "HIDDEN"),
            new LayerDefinition(StairLayers.PlanCut, 1, LineWeight.LineWeight040, "Continuous"),
            new LayerDefinition(StairLayers.PlanAnnotation, 3, LineWeight.LineWeight030, "Continuous"),
            new LayerDefinition(StairLayers.PlanText, 7, LineWeight.LineWeight018, "Continuous"),
            new LayerDefinition(StairLayers.Concrete, 1, LineWeight.LineWeight035, "Continuous"),
            new LayerDefinition(StairLayers.Brick, 30, LineWeight.LineWeight018, "Continuous"),
            new LayerDefinition(StairLayers.Finish, 2, LineWeight.LineWeight030, "Continuous")
        };

        public int Draw(Database database, DrawingModel drawing, StairPlacement placement)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            if (drawing == null)
            {
                throw new ArgumentNullException(nameof(drawing));
            }

            if (placement == null)
            {
                throw new ArgumentNullException(nameof(placement));
            }

            Vector3d xAxis = new Vector3d(placement.XAxis.X, placement.XAxis.Y, 0.0);
            if (xAxis.Length <= 1e-9)
            {
                throw new ArgumentException("Placement X axis must be non-zero on WCS XY.", nameof(placement));
            }

            xAxis = xAxis.GetNormal();
            Vector3d yAxis = Vector3d.ZAxis.CrossProduct(xAxis).GetNormal();
            var transform = new DrawingTransform(placement.Origin, xAxis, yAxis);
            int entityCount = 0;

            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                EnsureLayers(database, transaction);
                var currentSpace = (BlockTableRecord)transaction.GetObject(
                    database.CurrentSpaceId,
                    OpenMode.ForWrite);

                entityCount += AppendPolylines(
                    drawing.Polylines,
                    false,
                    transform,
                    placement.Origin.Z,
                    currentSpace,
                    transaction);

                foreach (LinePrimitive primitive in drawing.Lines)
                {
                    var line = new Line(
                        transform.ToWorld(primitive.Start),
                        transform.ToWorld(primitive.End))
                    {
                        Layer = primitive.LayerName,
                        LinetypeScale = primitive.LinetypeScale
                    };

                    AppendEntity(currentSpace, transaction, line);
                    entityCount++;
                }

                foreach (CirclePrimitive primitive in drawing.Circles)
                {
                    var circle = new Circle(
                        transform.ToWorld(primitive.Center),
                        Vector3d.ZAxis,
                        primitive.Radius)
                    {
                        Layer = primitive.LayerName
                    };

                    AppendEntity(currentSpace, transaction, circle);
                    entityCount++;
                }

                entityCount += AppendPolylines(
                    drawing.Polylines,
                    true,
                    transform,
                    placement.Origin.Z,
                    currentSpace,
                    transaction);

                double baseRotation = Math.Atan2(xAxis.Y, xAxis.X);
                foreach (TextPrimitive primitive in drawing.Texts)
                {
                    Point3d textPoint = transform.ToWorld(primitive.Position);
                    var text = new DBText
                    {
                        Position = textPoint,
                        Height = primitive.Height,
                        Rotation = baseRotation + primitive.Rotation,
                        TextString = primitive.Text,
                        Layer = primitive.LayerName,
                        TextStyleId = database.Textstyle
                    };

                    if (primitive.Anchor == TextAnchor.MiddleCenter)
                    {
                        text.HorizontalMode = TextHorizontalMode.TextCenter;
                        text.VerticalMode = TextVerticalMode.TextVerticalMid;
                        text.AlignmentPoint = textPoint;
                    }

                    AppendEntity(currentSpace, transaction, text);
                    if (primitive.Anchor == TextAnchor.MiddleCenter)
                    {
                        text.AdjustAlignment(database);
                    }

                    entityCount++;
                }

                transaction.Commit();
            }

            return entityCount;
        }

        private static int AppendPolylines(
            IEnumerable<PolylinePrimitive> primitives,
            bool drawOnTop,
            DrawingTransform transform,
            double elevation,
            BlockTableRecord currentSpace,
            Transaction transaction)
        {
            int appendedCount = 0;
            foreach (PolylinePrimitive primitive in primitives)
            {
                if (primitive.DrawOnTop != drawOnTop)
                {
                    continue;
                }

                Polyline polyline = CreatePolyline(primitive, transform, elevation);
                AppendEntity(currentSpace, transaction, polyline);
                appendedCount++;
            }

            return appendedCount;
        }

        private static Polyline CreatePolyline(
            PolylinePrimitive primitive,
            DrawingTransform transform,
            double elevation)
        {
            var polyline = new Polyline(primitive.Vertices.Count);
            try
            {
                polyline.Closed = primitive.Closed;
                polyline.Elevation = elevation;
                polyline.Layer = primitive.LayerName;
                polyline.LinetypeScale = primitive.LinetypeScale;
                polyline.Normal = Vector3d.ZAxis;

                for (int vertexIndex = 0; vertexIndex < primitive.Vertices.Count; vertexIndex++)
                {
                    Point3d point = transform.ToWorld(primitive.Vertices[vertexIndex]);
                    polyline.AddVertexAt(vertexIndex, new Point2d(point.X, point.Y), 0.0, 0.0, 0.0);
                }

                polyline.ConstantWidth = primitive.GlobalWidth;
                return polyline;
            }
            catch
            {
                polyline.Dispose();
                throw;
            }
        }

        private static void EnsureLayers(Database database, Transaction transaction)
        {
            var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
            foreach (LayerDefinition definition in LayerDefinitions)
            {
                if (layerTable.Has(definition.Name))
                {
                    continue;
                }

                if (!layerTable.IsWriteEnabled)
                {
                    layerTable.UpgradeOpen();
                }

                var layerRecord = new LayerTableRecord
                {
                    Name = definition.Name,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, definition.ColorIndex),
                    LineWeight = definition.LineWeight,
                    LinetypeObjectId = GetLinetypeId(database, transaction, definition.LinetypeName)
                };

                bool added = false;
                try
                {
                    layerTable.Add(layerRecord);
                    transaction.AddNewlyCreatedDBObject(layerRecord, true);
                    added = true;
                }
                finally
                {
                    if (!added)
                    {
                        layerRecord.Dispose();
                    }
                }
            }
        }

        private static ObjectId GetLinetypeId(
            Database database,
            Transaction transaction,
            string linetypeName)
        {
            var linetypeTable = (LinetypeTable)transaction.GetObject(
                database.LinetypeTableId,
                OpenMode.ForRead);

            if (!linetypeTable.Has(linetypeName))
            {
                try
                {
                    database.LoadLineTypeFile(linetypeName, "acad.lin");
                }
                catch (AutoCADException exception)
                {
                    SystemTrace.TraceWarning(
                        "Could not load linetype '{0}' from acad.lin: {1}",
                        linetypeName,
                        exception.Message);
                }
            }

            return linetypeTable.Has(linetypeName)
                ? linetypeTable[linetypeName]
                : linetypeTable["Continuous"];
        }

        private static void AppendEntity(
            BlockTableRecord currentSpace,
            Transaction transaction,
            Entity entity)
        {
            bool appended = false;
            try
            {
                currentSpace.AppendEntity(entity);
                transaction.AddNewlyCreatedDBObject(entity, true);
                appended = true;
            }
            finally
            {
                if (!appended)
                {
                    entity.Dispose();
                }
            }
        }

        private sealed class DrawingTransform
        {
            private readonly Point3d _origin;
            private readonly Vector3d _xAxis;
            private readonly Vector3d _yAxis;

            public DrawingTransform(Point3d origin, Vector3d xAxis, Vector3d yAxis)
            {
                _origin = origin;
                _xAxis = xAxis;
                _yAxis = yAxis;
            }

            public Point3d ToWorld(Point2 point)
            {
                return _origin + (_xAxis * point.X) + (_yAxis * point.Y);
            }
        }

        private sealed class LayerDefinition
        {
            public LayerDefinition(
                string name,
                short colorIndex,
                LineWeight lineWeight,
                string linetypeName)
            {
                Name = name;
                ColorIndex = colorIndex;
                LineWeight = lineWeight;
                LinetypeName = linetypeName;
            }

            public string Name { get; }

            public short ColorIndex { get; }

            public LineWeight LineWeight { get; }

            public string LinetypeName { get; }
        }
    }
}
