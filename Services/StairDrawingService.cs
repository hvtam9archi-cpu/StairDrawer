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
            new LayerDefinition(StairLayers.SectionTrace, 4, LineWeight.LineWeight018, "CENTER"),
            new LayerDefinition(StairLayers.SectionTraceMark, 4, LineWeight.LineWeight025, "Continuous"),
            new LayerDefinition(StairLayers.PlanAnnotation, 3, LineWeight.LineWeight030, "Continuous"),
            new LayerDefinition(StairLayers.PlanText, 7, LineWeight.LineWeight018, "Continuous"),
            new LayerDefinition(StairLayers.Concrete, 1, LineWeight.LineWeight035, "Continuous"),
            new LayerDefinition(StairLayers.Brick, 30, LineWeight.LineWeight018, "Continuous"),
            new LayerDefinition(StairLayers.Finish, 2, LineWeight.LineWeight030, "Continuous"),
            new LayerDefinition(StairLayers.Railing, 3, LineWeight.LineWeight025, "Continuous"),
            new LayerDefinition(StairLayers.RailingGlass, 4, LineWeight.LineWeight018, "Continuous")
        };

        public int Draw(Database database, DrawingModel drawing, StairPlacement placement)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            return DrawInto(database, drawing, placement, database.CurrentSpaceId);
        }

        internal ObjectId DrawAsBlock(Database database, DrawingModel drawing, StairPlacement placement, StairSectionParameters parameters)
        {
            using (Transaction transaction = database.TransactionManager.StartTransaction())
            {
                var table = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForWrite);
                var definition = new BlockTableRecord { Name = "STAIRDRAW_SECTION_" + Guid.NewGuid().ToString("N") };
                table.Add(definition);
                transaction.AddNewlyCreatedDBObject(definition, true);
                DrawInto(database, drawing, new StairPlacement(Point3d.Origin, Vector3d.XAxis), definition.ObjectId);
                var source = new BlockReference(placement.Origin, definition.ObjectId)
                {
                    Rotation = Math.Atan2(placement.XAxis.Y, placement.XAxis.X)
                };
                var space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
                AppendEntity(space, transaction, source);
                source.CreateExtensionDictionary();
                var dictionary = (DBDictionary)transaction.GetObject(source.ExtensionDictionary, OpenMode.ForWrite);
                var record = new Xrecord();
                using (var data = new ResultBuffer(new TypedValue((int)DxfCode.Int16, (short)1),
                    new TypedValue((int)DxfCode.Real, parameters.TreadDepth),
                    new TypedValue((int)DxfCode.Real, parameters.RiserHeight),
                    new TypedValue((int)DxfCode.Real, parameters.FinishThickness),
                    new TypedValue((int)DxfCode.Real, parameters.WaistSlabThickness))) record.Data = data;
                dictionary.SetAt("STAIRDRAW_SECTION_V1", record);
                transaction.AddNewlyCreatedDBObject(record, true);
                ObjectId id = source.ObjectId;
                transaction.Commit();
                return id;
            }
        }

        private int DrawInto(Database database, DrawingModel drawing, StairPlacement placement, ObjectId ownerId)
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
                    ownerId,
                    OpenMode.ForWrite);

                entityCount += AppendPolylines(
                    drawing.Polylines,
                    false,
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
                    false,
                    transform,
                    placement.Origin.Z,
                    currentSpace,
                    transaction);

                entityCount += AppendWipeouts(
                    drawing.Wipeouts,
                    transform,
                    placement.Origin.Z,
                    currentSpace,
                    transaction);

                entityCount += AppendPolylines(
                    drawing.Polylines,
                    true,
                    true,
                    transform,
                    placement.Origin.Z,
                    currentSpace,
                    transaction);

                double baseRotation = transform.Rotation;
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
            bool occludesBackground,
            DrawingTransform transform,
            double elevation,
            BlockTableRecord currentSpace,
            Transaction transaction)
        {
            int appendedCount = 0;
            foreach (PolylinePrimitive primitive in primitives)
            {
                if (primitive.DrawOnTop != drawOnTop
                    || (drawOnTop && primitive.OccludesBackground != occludesBackground))
                {
                    continue;
                }

                Polyline polyline = CreatePolyline(primitive, transform, elevation);
                AppendEntity(currentSpace, transaction, polyline);
                appendedCount++;
                if (primitive.Closed
                    && (primitive.LayerName == StairLayers.Brick || primitive.LayerName == StairLayers.Concrete
                        || primitive.LayerName == StairLayers.RailingGlass))
                {
                    AppendMaterialHatch(currentSpace, transaction, polyline, transform.Rotation);
                    appendedCount++;
                }
            }

            return appendedCount;
        }

        private static void AppendMaterialHatch(
            BlockTableRecord currentSpace, Transaction transaction, Polyline boundary, double rotation)
        {
            using (var hatch = new Hatch())
            {
                hatch.SetDatabaseDefaults(currentSpace.Database);
                hatch.LayerId = boundary.LayerId;
                hatch.Normal = boundary.Normal;
                hatch.Elevation = boundary.Elevation;
                currentSpace.AppendEntity(hatch);
                transaction.AddNewlyCreatedDBObject(hatch, true);

                bool concrete = boundary.Layer == StairLayers.Concrete;
                bool glass = boundary.Layer == StairLayers.RailingGlass;
                string pattern = glass ? "AR-RROOF" : concrete ? "M_BTCT" : "ANSI31";
                HatchPatternType type = concrete ? HatchPatternType.CustomDefined : HatchPatternType.PreDefined;
                double unitScale = currentSpace.Database.Measurement == MeasurementValue.English ? 25.4 : 1.0;
                hatch.SetHatchPattern(type, pattern);
                hatch.PatternScale = glass || concrete ? 10.0 : 4.0 * unitScale;
                hatch.PatternAngle = rotation < 0.0 ? rotation + (Math.PI * 2.0) : rotation;
                if (glass)
                {
                    hatch.Color = Color.FromRgb(34, 211, 238);
                    hatch.Transparency = new Transparency(64);
                }
                hatch.SetHatchPattern(hatch.PatternType, hatch.PatternName);
                hatch.Associative = true;
                var loop = new ObjectIdCollection { boundary.ObjectId };
                hatch.AppendLoop(HatchLoopTypes.Outermost, loop);
                hatch.EvaluateHatch(true);
            }
        }

        private static int AppendWipeouts(
            IEnumerable<WipeoutPrimitive> primitives,
            DrawingTransform transform,
            double elevation,
            BlockTableRecord currentSpace,
            Transaction transaction)
        {
            int appendedCount = 0;
            foreach (WipeoutPrimitive primitive in primitives)
            {
                var boundary = new Point2dCollection();
                foreach (Point2 vertex in primitive.Boundary)
                {
                    Point3d worldPoint = transform.ToWorld(vertex);
                    boundary.Add(new Point2d(worldPoint.X, worldPoint.Y));
                }

                Point3d firstPoint = transform.ToWorld(primitive.Boundary[0]);
                boundary.Add(new Point2d(firstPoint.X, firstPoint.Y));

                var wipeout = new Wipeout
                {
                    Layer = primitive.LayerName
                };
                bool appended = false;
                try
                {
                    wipeout.SetFrom(boundary, Vector3d.ZAxis);
                    if (Math.Abs(elevation) > 1e-9)
                    {
                        wipeout.TransformBy(Matrix3d.Displacement(
                            Vector3d.ZAxis * elevation));
                    }

                    currentSpace.AppendEntity(wipeout);
                    transaction.AddNewlyCreatedDBObject(wipeout, true);
                    appended = true;
                    appendedCount++;
                }
                finally
                {
                    if (!appended)
                    {
                        wipeout.Dispose();
                    }
                }
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

            public double Rotation => Math.Atan2(_xAxis.Y, _xAxis.X);

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
