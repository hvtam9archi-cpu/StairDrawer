using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.Geometry;
using StairDrawer.Models;

namespace StairDrawer.Services
{
    internal sealed class StairDetailResult
    {
        public ObjectId BlockId { get; set; }
        public int DimensionCount { get; set; }
        public int NoteCount { get; set; }
        public bool UsedCallout { get; set; }
    }

    internal sealed class StairDetailService
    {
        private const string BoundaryLayer = "STAIR-DETAIL-BOUNDARY";
        private const string AnnotationLayer = "STAIR-DETAIL-NOTE";
        private const string DimensionLayer = "STAIR-DETAIL-DIM";
        private const string ReferenceLayer = "STAIR-DETAIL-REFERENCE";
        private const string MetadataKey = "STAIRDRAW_DETAIL_V1";

        public static ObjectId CreateTemporarySource(Database database, ObjectIdCollection entities)
        {
            using (Transaction tx = database.TransactionManager.StartTransaction())
            {
                var table = (BlockTable)tx.GetObject(database.BlockTableId, OpenMode.ForWrite);
                var definition = new BlockTableRecord { Name = "STAIR_TEMP_SOURCE_" + Guid.NewGuid().ToString("N") };
                table.Add(definition); tx.AddNewlyCreatedDBObject(definition, true);
                using (var map = new IdMapping()) database.DeepCloneObjects(entities, definition.ObjectId, map, false);
                var source = new BlockReference(Point3d.Origin, definition.ObjectId) { Visible = false };
                var space = (BlockTableRecord)tx.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
                Append(space, tx, source);
                source.CreateExtensionDictionary();
                var dictionary = (DBDictionary)tx.GetObject(source.ExtensionDictionary, OpenMode.ForWrite);
                var stone = definition.Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<Polyline>()
                    .FirstOrDefault(p => p.Layer == StairLayers.Finish && p.Closed && p.NumberOfVertices == 5);
                if (stone != null)
                {
                    Vector3d axis = stone.GetPoint3dAt(3) - stone.GetPoint3dAt(2);
                    double angle = Math.Atan2(axis.Y, axis.X);
                    while (angle > Math.PI * 0.5) angle -= Math.PI;
                    while (angle < -Math.PI * 0.5) angle += Math.PI;
                    var record = new Xrecord();
                    using (var values = new ResultBuffer(new TypedValue((int)DxfCode.Real, angle),
                        new TypedValue((int)DxfCode.Real, stone.GetPoint3dAt(3).DistanceTo(stone.GetPoint3dAt(4))))) record.Data = values;
                    dictionary.SetAt("STAIRDRAW_SOURCE_FRAME", record); tx.AddNewlyCreatedDBObject(record, true);
                }
                ObjectId sourceId = source.ObjectId; tx.Commit(); return sourceId;
            }
        }

        public static void RemoveTemporarySource(Database database, ObjectId id)
        {
            using (Transaction tx = database.TransactionManager.StartTransaction())
            {
                var source = (BlockReference)tx.GetObject(id, OpenMode.ForWrite);
                ObjectId definition = source.BlockTableRecord;
                source.Erase();
                ((BlockTableRecord)tx.GetObject(definition, OpenMode.ForWrite)).Erase();
                tx.Commit();
            }
        }

        public static string NextNumber(Database database)
        {
            using (Transaction tx = database.TransactionManager.StartOpenCloseTransaction())
            {
                var space = (BlockTableRecord)tx.GetObject(database.CurrentSpaceId, OpenMode.ForRead);
                int maximum = 0;
                foreach (ObjectId id in space)
                {
                    if (!(tx.GetObject(id, OpenMode.ForRead) is BlockReference block) || block.ExtensionDictionary.IsNull) continue;
                    var dictionary = (DBDictionary)tx.GetObject(block.ExtensionDictionary, OpenMode.ForRead);
                    if (!dictionary.Contains(MetadataKey)) continue;
                    using (var data = ((Xrecord)tx.GetObject(dictionary.GetAt(MetadataKey), OpenMode.ForRead)).Data)
                    {
                        TypedValue[] values = data?.AsArray();
                        if (values != null && values.Length > 1 && int.TryParse(values[1].Value as string,
                            NumberStyles.None, CultureInfo.InvariantCulture, out int number)) maximum = Math.Max(maximum, number);
                    }
                }
                return (maximum + 1).ToString("00", CultureInfo.InvariantCulture);
            }
        }

        public static double StoneThickness(Database database, ObjectId sourceId)
        {
            using (Transaction tx = database.TransactionManager.StartOpenCloseTransaction())
            {
                var source = (BlockReference)tx.GetObject(sourceId, OpenMode.ForRead);
                if (!source.ExtensionDictionary.IsNull)
                {
                    var dict = (DBDictionary)tx.GetObject(source.ExtensionDictionary, OpenMode.ForRead);
                    if (dict.Contains("STAIRDRAW_SECTION_V1"))
                    {
                        using (var data = ((Xrecord)tx.GetObject(dict.GetAt("STAIRDRAW_SECTION_V1"), OpenMode.ForRead)).Data)
                        {
                            TypedValue[] values = data?.AsArray();
                            if (values != null && values.Length >= 4 && values[3].Value is double value)
                                return value * Math.Abs(source.ScaleFactors.Y);
                        }
                    }
                    if (dict.Contains("STAIRDRAW_SOURCE_FRAME"))
                    {
                        using (var data = ((Xrecord)tx.GetObject(dict.GetAt("STAIRDRAW_SOURCE_FRAME"), OpenMode.ForRead)).Data)
                            if (data?.AsArray().Length == 2) return (double)data.AsArray()[1].Value;
                    }
                }
                return 20.0;
            }
        }

        public StairDetailResult Create(Database database, ObjectId sourceId, StairDetailOptions options,
            Point3d center, Point3d destination)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            using (Transaction tx = database.TransactionManager.StartTransaction())
            {
                var source = tx.GetObject(sourceId, OpenMode.ForRead) as BlockReference;
                if (source == null || source.IsErased) throw new ArgumentException("Chọn block mặt cắt thang hợp lệ.");
                double sx = Vector3d.XAxis.TransformBy(source.BlockTransform).Length;
                double sy = Vector3d.YAxis.TransformBy(source.BlockTransform).Length;
                if (Math.Abs(sx - sy) > Math.Max(sx, sy) * 1e-8)
                    throw new ArgumentException("Block nguồn cần có tỷ lệ X và Y bằng nhau để trích chi tiết đúng kích thước.");
                if (Math.Abs(source.Normal.DotProduct(Vector3d.ZAxis)) < 1.0 - 1e-8)
                    throw new ArgumentException("Chọn mặt cắt nằm trên mặt phẳng WCS XY.");

                var sourceRecord = (BlockTableRecord)tx.GetObject(source.BlockTableRecord, OpenMode.ForRead);
                var sourceIds = new ObjectIdCollection();
                foreach (ObjectId id in sourceRecord)
                {
                    var entity = tx.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity != null && (!options.HideRailing || !IsRailing(entity.Layer))) sourceIds.Add(id);
                }
                if (sourceIds.Count == 0) throw new ArgumentException("Block nguồn không có hình học để trích.");

                var table = (BlockTable)tx.GetObject(database.BlockTableId, OpenMode.ForWrite);
                var snapshot = new BlockTableRecord { Name = "STAIR_DETAIL_" + Guid.NewGuid().ToString("N") };
                table.Add(snapshot); tx.AddNewlyCreatedDBObject(snapshot, true);
                var pieces = new List<Piece>();
                using (var mapping = new IdMapping())
                {
                    database.DeepCloneObjects(sourceIds, snapshot.ObjectId, mapping, false);
                    foreach (ObjectId id in sourceIds)
                    {
                        IdPair pair = mapping.Lookup(id);
                        if (!pair.IsCloned) continue;
                        var clone = (Entity)tx.GetObject(pair.Value, OpenMode.ForWrite);
                        clone.TransformBy(source.BlockTransform);
                    }
                }
                var definitions = new Dictionary<ObjectId, ObjectId>();
                foreach (ObjectId id in snapshot)
                {
                    if (tx.GetObject(id, OpenMode.ForRead) is BlockReference nested)
                    {
                        ObjectId definition = CloneDefinition(database, tx, nested.BlockTableRecord, definitions, options.HideRailing);
                        nested.UpgradeOpen(); nested.BlockTableRecord = definition;
                    }
                }
                foreach (ObjectId id in snapshot)
                {
                    var entity = (Entity)tx.GetObject(id, OpenMode.ForRead);
                    if (entity is Hatch hatch) { hatch.UpgradeOpen(); hatch.EvaluateHatch(true); }
                    CollectPieces(tx, entity, Matrix3d.Identity, pieces, 0);
                }
                if (!pieces.Any(p => p.Intersects(center, options.Radius)))
                    throw new ArgumentException("Vùng trích không chạm hình học của thang. Chọn lại tâm trên bậc cần trích.");

                EnsureLayer(database, tx, BoundaryLayer, 8, "DASHED", LineWeight.LineWeight018);
                EnsureLayer(database, tx, AnnotationLayer, 7, "Continuous", LineWeight.LineWeight018);
                EnsureLayer(database, tx, DimensionLayer, 8, "Continuous", LineWeight.LineWeight013);
                EnsureLayer(database, tx, ReferenceLayer, 8, "CENTER", LineWeight.LineWeight013);
                ObjectId textStyle = EnsureTextStyle(database, tx);
                ObjectId dimStyle = EnsureDimStyle(database, tx, textStyle, options.SourceScale);
                var space = (BlockTableRecord)tx.GetObject(source.OwnerId, OpenMode.ForWrite);

                Vector3d sourceX = Vector3d.XAxis.TransformBy(source.BlockTransform).GetNormal();
                double angle = Math.Atan2(sourceX.Y, sourceX.X);
                if (!source.ExtensionDictionary.IsNull)
                {
                    var dictionary = (DBDictionary)tx.GetObject(source.ExtensionDictionary, OpenMode.ForRead);
                    if (dictionary.Contains("STAIRDRAW_SOURCE_FRAME"))
                        using (var frame = ((Xrecord)tx.GetObject(dictionary.GetAt("STAIRDRAW_SOURCE_FRAME"), OpenMode.ForRead)).Data)
                            angle = (double)frame.AsArray()[0].Value;
                }
                double rotation = options.Horizontal ? -angle : 0.0;
                Matrix3d transform = Matrix3d.Displacement(destination - center)
                    * Matrix3d.Rotation(rotation, Vector3d.ZAxis, center) * Matrix3d.Scaling(options.Magnification, center);
                var detail = new BlockReference(Point3d.Origin, snapshot.ObjectId);
                Append(space, tx, detail);
                ApplyCrop(tx, detail, center, options.Radius);
                detail.TransformBy(transform);
                Record(tx, detail, options, source.Visible ? sourceId : ObjectId.Null);

                double paper = options.SourceScale;
                var sourceCircle = new Circle(center, Vector3d.ZAxis, options.Radius)
                    { Layer = BoundaryLayer, LinetypeScale = paper * 0.5 };
                var detailCircle = new Circle(destination, Vector3d.ZAxis, options.Radius * options.Magnification)
                    { Layer = BoundaryLayer, LinetypeScale = paper * 0.5 };
                Append(space, tx, sourceCircle); Append(space, tx, detailCircle);
                AddSourceBubble(space, tx, database, textStyle, options, center, source.GeometricExtents.MaxPoint.X);

                var result = new StairDetailResult { BlockId = detail.ObjectId };
                var connectors = new List<Entity>();
                if (options.ConnectingLeader)
                {
                    List<Point3d> boundary = CirclePoints(center, options.Radius, 64);
                    if (StairCalloutBridge.TryCreateLeader(boundary, transform, paper * 1.2, database, out List<Entity> leader))
                    {
                        result.UsedCallout = true;
                        foreach (Entity entity in leader) { entity.Layer = AnnotationLayer; Append(space, tx, entity); connectors.Add(entity); }
                    }
                    else AddConnector(space, tx, center, options.Radius, destination, options.Radius * options.Magnification);
                }

                var treads = FindTreads(pieces, center, options.Radius, angle);
                if (options.Dimensions) result.DimensionCount = AddDimensions(space, tx, dimStyle, options, treads, transform, destination, angle + rotation);
                if (options.Notes) result.NoteCount = AddNotes(space, tx, textStyle, options, pieces, center, destination, transform, angle);
                AddTitle(space, tx, textStyle, options, destination, connectors);
                tx.Commit();
                return result;
            }
        }

        private static bool IsRailing(string layer) => layer == StairLayers.Railing || layer == StairLayers.RailingGlass;

        private static ObjectId CloneDefinition(Database database, Transaction tx, ObjectId original,
            IDictionary<ObjectId, ObjectId> cache, bool hideRailing)
        {
            if (cache.TryGetValue(original, out ObjectId existing)) return existing;
            var table = (BlockTable)tx.GetObject(database.BlockTableId, OpenMode.ForWrite);
            var definition = new BlockTableRecord { Name = "STAIR_COMPONENT_" + Guid.NewGuid().ToString("N") };
            table.Add(definition); tx.AddNewlyCreatedDBObject(definition, true); cache.Add(original, definition.ObjectId);
            var source = (BlockTableRecord)tx.GetObject(original, OpenMode.ForRead);
            var entities = new ObjectIdCollection(source.Cast<ObjectId>()
                .Where(id => !hideRailing || !IsRailing(((Entity)tx.GetObject(id, OpenMode.ForRead)).Layer)).ToArray());
            using (var map = new IdMapping()) database.DeepCloneObjects(entities, definition.ObjectId, map, false);
            foreach (ObjectId id in definition)
            {
                if (tx.GetObject(id, OpenMode.ForRead) is BlockReference nested)
                {
                    ObjectId nestedId = CloneDefinition(database, tx, nested.BlockTableRecord, cache, hideRailing);
                    nested.UpgradeOpen(); nested.BlockTableRecord = nestedId;
                }
            }
            return definition.ObjectId;
        }

        private static void CollectPieces(Transaction tx, Entity entity, Matrix3d matrix, IList<Piece> pieces, int depth)
        {
            if (entity is Polyline polyline)
            {
                var vertices = new List<Point3d>();
                for (int i = 0; i < polyline.NumberOfVertices; i++) vertices.Add(polyline.GetPoint3dAt(i).TransformBy(matrix));
                if (vertices.Count > 1) pieces.Add(new Piece(vertices, polyline.Closed, polyline.Layer));
            }
            else if (entity is BlockReference block && depth < 12)
            {
                var record = (BlockTableRecord)tx.GetObject(block.BlockTableRecord, OpenMode.ForRead);
                foreach (ObjectId id in record) CollectPieces(tx, (Entity)tx.GetObject(id, OpenMode.ForRead), matrix * block.BlockTransform, pieces, depth + 1);
            }
            else if (entity is Line line)
                pieces.Add(new Piece(new List<Point3d> { line.StartPoint.TransformBy(matrix), line.EndPoint.TransformBy(matrix) }, false, line.Layer));
        }

        private static List<Point3d> FindTreads(IEnumerable<Piece> pieces, Point3d center, double radius, double angle)
        {
            Vector3d x = new Vector3d(Math.Cos(angle), Math.Sin(angle), 0);
            Vector3d y = Vector3d.ZAxis.CrossProduct(x);
            var points = new List<Tuple<Point3d, double>>();
            foreach (Piece piece in pieces.Where(p => p.Layer == StairLayers.Finish && p.Points.Count == 5 && p.Closed))
            {
                Point3d a = piece.Points[2], b = piece.Points[3];
                if (Math.Abs((b - a).DotProduct(y)) > 1e-5) continue;
                double direction = Math.Sign((b - a).DotProduct(x));
                if (direction == 0) continue;
                // The common 20 mm nosing is removed from the witness position; no nominal pitch is assumed.
                Point3d point = a + x * ((direction * 20.0) - (a - piece.Points[0]).DotProduct(x));
                if (Distance2d(point, center) < radius * 0.90 && !points.Any(p => Distance2d(p.Item1, point) < 1e-5))
                    points.Add(Tuple.Create(point, direction));
            }
            if (points.Count == 0) return new List<Point3d>();
            double chosenDirection = points.OrderBy(p => Distance2d(p.Item1, center)).First().Item2;
            return points.Where(p => p.Item2 == chosenDirection).OrderBy(p => Distance2d(p.Item1, center)).Take(3)
                .Select(p => p.Item1).OrderBy(p => (p - center).DotProduct(x)).ToList();
        }

        private static int AddDimensions(BlockTableRecord space, Transaction tx, ObjectId style, StairDetailOptions options,
            IList<Point3d> treads, Matrix3d transform, Point3d center, double angle)
        {
            if (treads.Count < 2) return 0;
            var points = treads.Select(p => p.TransformBy(transform)).ToList();
            Vector3d x = new Vector3d(Math.Cos(angle), Math.Sin(angle), 0);
            Vector3d y = Vector3d.ZAxis.CrossProduct(x);
            double paper = options.SourceScale, radius = options.Radius * options.Magnification;
            double top = Math.Max(radius + paper * 3, points.Max(p => (p - center).DotProduct(y)) + paper * 4);
            double right = radius + paper * 3;
            int count = 0;
            for (int i = 1; i < points.Count; i++)
            {
                Point3d a = points[i - 1], b = points[i];
                double width = Math.Abs((b - a).DotProduct(x)), rise = Math.Abs((b - a).DotProduct(y));
                if (width > 1e-5)
                {
                    var dim = new RotatedDimension(angle, a, b,
                        center + x * (((a - center).DotProduct(x) + (b - center).DotProduct(x)) * 0.5) + y * top,
                        "", style) { Layer = DimensionLayer, Dimlfac = 1.0 / options.Magnification };
                    Append(space, tx, dim); count++;
                }
                if (rise > 1e-5)
                {
                    var dim = new RotatedDimension(angle + Math.PI * 0.5, a, b,
                        center + x * right + y * (((a - center).DotProduct(y) + (b - center).DotProduct(y)) * 0.5),
                        "", style) { Layer = DimensionLayer, Dimlfac = 1.0 / options.Magnification };
                    Append(space, tx, dim); count++;
                }
            }
            foreach (Point3d p in points)
            {
                double level = (p - center).DotProduct(y);
                var axis = new Line(center - x * (radius + paper * 2) + y * level,
                    center + x * (radius + paper * 2) + y * level)
                    { Layer = ReferenceLayer, LinetypeScale = paper * 0.5 };
                Append(space, tx, axis);
            }
            return count;
        }

        private static int AddNotes(BlockTableRecord space, Transaction tx, ObjectId style, StairDetailOptions options,
            IList<Piece> pieces, Point3d originalCenter, Point3d center, Matrix3d transform, double sourceAngle)
        {
            var notes = new List<Tuple<Point3d, string>>();
            var finishes = pieces.Where(p => p.Layer == StairLayers.Finish).ToList();
            double dotClearance = options.SourceScale * 0.35 / options.Magnification;
            AddNoteTarget(notes, finishes.Where(p => p.Points.Count == 5), options.TreadNote, originalCenter, options.Radius, dotClearance);
            Vector3d sourceX = new Vector3d(Math.Cos(sourceAngle), Math.Sin(sourceAngle), 0);
            Vector3d sourceY = Vector3d.ZAxis.CrossProduct(sourceX);
            AddNoteTarget(notes, finishes.Where(p => p.Points.Count == 4
                && Math.Abs((p.Points[1] - p.Points[0]).DotProduct(sourceY))
                    > Math.Abs((p.Points[1] - p.Points[0]).DotProduct(sourceX))), options.RiserNote, originalCenter, options.Radius, dotClearance);
            AddNoteTarget(notes, pieces.Where(p => p.Layer == StairLayers.Brick), options.BrickNote, originalCenter, options.Radius, dotClearance);
            var concrete = new List<Tuple<Point3d, string>>();
            AddNoteTarget(concrete, pieces.Where(p => p.Layer == StairLayers.Concrete), options.ConcreteNote, originalCenter, options.Radius, dotClearance);
            double paper = options.SourceScale, radius = options.Radius * options.Magnification;
            var sorted = notes.OrderByDescending(n => n.Item1.TransformBy(transform).Y).ToList();
            double rowY = center.Y + radius + paper * 8;
            double textX = center.X + radius + paper * 10;
            foreach (var note in sorted)
            {
                Point3d target = note.Item1.TransformBy(transform);
                var label = AddText(space, tx, style, note.Item2, new Point3d(textX, rowY, center.Z), paper * 2.5, paper * 60);
                double baseline = rowY - label.ActualHeight - paper * 1.0;
                AddNoteLeader(space, tx, target, new Point3d(textX - paper * 3, baseline, center.Z),
                    new Point3d(textX + label.ActualWidth, baseline, center.Z), paper);
                rowY = baseline - paper * 4;
            }
            foreach (var note in concrete)
            {
                Point3d target = note.Item1.TransformBy(transform);
                double noteY = center.Y - radius - paper * 2;
                var label = AddText(space, tx, style, note.Item2,
                    new Point3d(center.X + radius * 0.40, noteY, center.Z), paper * 2.5, paper * 60);
                double baseline = noteY - label.ActualHeight - paper;
                AddNoteLeader(space, tx, target, new Point3d(center.X + radius * 0.32, baseline, center.Z),
                    new Point3d(label.Location.X + label.ActualWidth, baseline, center.Z), paper);
            }
            return notes.Count + concrete.Count;
        }

        private static void AddNoteTarget(IList<Tuple<Point3d, string>> targets, IEnumerable<Piece> candidates,
            string text, Point3d center, double radius, double clearance)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            Point3d? best = null; double score = double.MaxValue;
            foreach (Piece piece in candidates.Where(p => p.Closed && p.Intersects(center, radius)))
            {
                var probes = new List<Point3d>
                {
                    new Point3d(piece.Points.Average(p => p.X), piece.Points.Average(p => p.Y), center.Z)
                };
                double minX = Math.Max(center.X - radius, piece.Points.Min(p => p.X));
                double maxX = Math.Min(center.X + radius, piece.Points.Max(p => p.X));
                double minY = Math.Max(center.Y - radius, piece.Points.Min(p => p.Y));
                double maxY = Math.Min(center.Y + radius, piece.Points.Max(p => p.Y));
                for (int i = 0; i <= 20; i++) for (int j = 0; j <= 20; j++)
                    probes.Add(new Point3d(minX + (maxX - minX) * i / 20, minY + (maxY - minY) * j / 20, center.Z));
                foreach (Point3d p in probes)
                {
                    double distance = Distance2d(p, center);
                    if (distance < radius * 0.87 && distance < score && piece.Contains(p) && piece.Clearance(p) >= clearance)
                    { best = p; score = distance; }
                }
            }
            if (best.HasValue) targets.Add(Tuple.Create(best.Value, text));
        }

        private static void AddNoteLeader(BlockTableRecord space, Transaction tx, Point3d start, Point3d knee, Point3d end, double paper)
        {
            var line = new Polyline(3) { Layer = AnnotationLayer, Elevation = start.Z };
            line.AddVertexAt(0, new Point2d(start.X, start.Y), 0, 0, 0);
            line.AddVertexAt(1, new Point2d(knee.X, knee.Y), 0, 0, 0);
            line.AddVertexAt(2, new Point2d(end.X, end.Y), 0, 0, 0);
            Append(space, tx, line);
            var dot = new Polyline(2) { Closed = true, Layer = AnnotationLayer, Elevation = start.Z, ConstantWidth = paper * 0.6 };
            dot.AddVertexAt(0, new Point2d(start.X - paper * 0.3, start.Y), 1, 0, 0);
            dot.AddVertexAt(1, new Point2d(start.X + paper * 0.3, start.Y), 1, 0, 0);
            Append(space, tx, dot);
        }

        private static void AddSourceBubble(BlockTableRecord space, Transaction tx, Database database, ObjectId style,
            StairDetailOptions options, Point3d center, double sourceRight)
        {
            double paper = options.SourceScale, radius = paper * 5;
            Point3d bubble = new Point3d(Math.Max(center.X + options.Radius + paper * 10, sourceRight + paper * 10),
                center.Y + options.Radius * 0.65, center.Z);
            var bubbles = space.Cast<ObjectId>().Select(id => tx.GetObject(id, OpenMode.ForRead)).OfType<Circle>()
                .Where(c => c.Layer == AnnotationLayer).ToList();
            while (bubbles.Any(c => Distance2d(c.Center, bubble) < c.Radius + radius + paper * 2))
                bubble += Vector3d.YAxis * (radius * 2 + paper * 2);
            Point3d border = center + (bubble - center).GetNormal() * options.Radius;
            Append(space, tx, new Line(border, bubble - Vector3d.XAxis * radius) { Layer = AnnotationLayer });
            Append(space, tx, new Circle(bubble, Vector3d.ZAxis, radius) { Layer = AnnotationLayer });
            if (string.IsNullOrWhiteSpace(options.Sheet)) CenterText(space, tx, database, style, options.Number, bubble, paper * 3.5, paper * 8);
            else
            {
                Append(space, tx, new Line(bubble - Vector3d.XAxis * radius, bubble + Vector3d.XAxis * radius) { Layer = AnnotationLayer });
                CenterText(space, tx, database, style, options.Number, bubble + Vector3d.YAxis * (paper * 2.2), paper * 3.2, paper * 8);
                CenterText(space, tx, database, style, options.Sheet, bubble - Vector3d.YAxis * (paper * 2.2), paper * 2.0, paper * 8);
            }
        }

        private static void AddTitle(BlockTableRecord space, Transaction tx, ObjectId style, StairDetailOptions options,
            Point3d center, IEnumerable<Entity> connectors)
        {
            double paper = options.SourceScale, radius = options.Radius * options.Magnification;
            double titleY = center.Y - radius - paper * 15;
            Point3d bubble = new Point3d(center.X - radius + paper * 5, titleY - paper * 2.5, center.Z);
            // Keep the number bubble beside an optional routed Callout connector.
            foreach (Polyline connector in connectors.OfType<Polyline>())
            {
                for (int i = 1; i < connector.NumberOfVertices; i++)
                {
                    Point3d a = connector.GetPoint3dAt(i - 1), b = connector.GetPoint3dAt(i);
                    double clearance = paper * 7;
                    if (Math.Abs(a.X - b.X) < 1e-7 && Math.Abs(bubble.X - a.X) < clearance
                        && bubble.Y > Math.Min(a.Y, b.Y) - clearance && bubble.Y < Math.Max(a.Y, b.Y) + clearance)
                        bubble = new Point3d(a.X + clearance, bubble.Y, bubble.Z);
                }
            }
            Append(space, tx, new Circle(bubble, Vector3d.ZAxis, paper * 5) { Layer = AnnotationLayer });
            CenterText(space, tx, space.Database, style, options.Number, bubble, paper * 3.5, paper * 8);
            double textX = bubble.X + paper * 7;
            MText title = AddText(space, tx, style, options.Title.ToUpperInvariant(),
                new Point3d(textX, titleY, center.Z), paper * 3.5, Math.Max(radius * 2, paper * 65));
            double underline = titleY - title.ActualHeight - paper * 0.8;
            double right = Math.Max(center.X + radius, textX + title.ActualWidth);
            Append(space, tx, new Line(new Point3d(textX, underline, center.Z), new Point3d(right, underline, center.Z)) { Layer = AnnotationLayer });
            var ratio = AddText(space, tx, style, "1:" + options.DetailScale.ToString("0.##", CultureInfo.InvariantCulture),
                new Point3d(right, underline - paper * 1.5, center.Z), paper * 2.5, paper * 20);
            ratio.Attachment = AttachmentPoint.TopRight;
        }

        private static MText AddText(BlockTableRecord space, Transaction tx, ObjectId style, string text, Point3d point, double height, double width)
        {
            var label = new MText { Layer = AnnotationLayer, TextStyleId = style, TextHeight = height, Width = width,
                Attachment = AttachmentPoint.TopLeft, Location = point,
                Contents = text.Replace("\\", "\\\\").Replace("{", "\\{").Replace("}", "\\}").Replace("\r", "").Replace("\n", "\\P") };
            Append(space, tx, label);
            return label;
        }

        private static void CenterText(BlockTableRecord space, Transaction tx, Database database, ObjectId style, string value,
            Point3d center, double height, double maxWidth)
        {
            var text = new DBText { TextStyleId = style, Layer = AnnotationLayer, TextString = value, Height = height,
                HorizontalMode = TextHorizontalMode.TextCenter, VerticalMode = TextVerticalMode.TextVerticalMid,
                Position = center, AlignmentPoint = center };
            Append(space, tx, text); text.AdjustAlignment(database);
            double width = text.GeometricExtents.MaxPoint.X - text.GeometricExtents.MinPoint.X;
            if (width > maxWidth)
            {
                text.Height *= maxWidth / width;
                text.AdjustAlignment(database);
            }
        }

        private static void AddConnector(BlockTableRecord space, Transaction tx, Point3d source, double sourceRadius, Point3d detail, double detailRadius)
        {
            Vector3d direction = detail - source;
            if (direction.Length <= sourceRadius + detailRadius) return;
            direction = direction.GetNormal();
            Append(space, tx, new Line(source + direction * sourceRadius, detail - direction * detailRadius) { Layer = AnnotationLayer });
        }

        private static void ApplyCrop(Transaction tx, BlockReference block, Point3d center, double radius)
        {
            // Same ACAD_FILTER/SPATIAL and hard-pointer storage contract used by the local Callout repository.
            block.CreateExtensionDictionary();
            var extension = (DBDictionary)tx.GetObject(block.ExtensionDictionary, OpenMode.ForWrite);
            extension.TreatElementsAsHard = true;
            var filters = new DBDictionary { TreatElementsAsHard = true };
            extension.SetAt("ACAD_FILTER", filters); tx.AddNewlyCreatedDBObject(filters, true);
            var points = new Point2dCollection();
            foreach (Point3d p in CirclePoints(center, radius, 128)) points.Add(new Point2d(p.X, p.Y));
            var filter = new SpatialFilter { Definition = new SpatialFilterDefinition(points, Vector3d.ZAxis, center.Z, 0, 0, true) };
            filters.SetAt("SPATIAL", filter); tx.AddNewlyCreatedDBObject(filter, true);
            var references = new Xrecord { XlateReferences = true };
            using (var values = new ResultBuffer(new TypedValue((int)DxfCode.HardPointerId, filters.ObjectId),
                new TypedValue((int)DxfCode.HardPointerId, filter.ObjectId))) references.Data = values;
            extension.SetAt("STAIRDRAW_CROP_REFS", references); tx.AddNewlyCreatedDBObject(references, true);
        }

        private static void Record(Transaction tx, BlockReference block, StairDetailOptions options, ObjectId source)
        {
            var dict = (DBDictionary)tx.GetObject(block.ExtensionDictionary, OpenMode.ForWrite);
            var record = new Xrecord { XlateReferences = true };
            var values = new List<TypedValue> { new TypedValue((int)DxfCode.Int16, (short)1),
                new TypedValue((int)DxfCode.Text, options.Number), new TypedValue((int)DxfCode.Text, options.Title),
                new TypedValue((int)DxfCode.Real, options.Magnification) };
            if (!source.IsNull) values.Add(new TypedValue((int)DxfCode.SoftPointerId, source));
            using (var data = new ResultBuffer(values.ToArray())) record.Data = data;
            dict.SetAt(MetadataKey, record); tx.AddNewlyCreatedDBObject(record, true);
        }

        private static List<Point3d> CirclePoints(Point3d center, double radius, int count)
        {
            var points = new List<Point3d>(count);
            for (int i = 0; i < count; i++) points.Add(center + new Vector3d(Math.Cos(2 * Math.PI * i / count), Math.Sin(2 * Math.PI * i / count), 0) * radius);
            return points;
        }

        private static ObjectId EnsureTextStyle(Database database, Transaction tx)
        {
            var styles = (TextStyleTable)tx.GetObject(database.TextStyleTableId, OpenMode.ForRead);
            const string name = "STAIR-DETAIL-ARIAL";
            if (styles.Has(name)) return styles[name];
            styles.UpgradeOpen();
            var style = new TextStyleTableRecord { Name = name, FileName = "arial.ttf" };
            styles.Add(style); tx.AddNewlyCreatedDBObject(style, true); return style.ObjectId;
        }

        private static ObjectId EnsureDimStyle(Database database, Transaction tx, ObjectId font, double scale)
        {
            var table = (DimStyleTable)tx.GetObject(database.DimStyleTableId, OpenMode.ForRead);
            string name = "STAIR-DETAIL-" + scale.ToString("0.##", CultureInfo.InvariantCulture);
            if (table.Has(name)) return table[name];
            table.UpgradeOpen();
            var style = new DimStyleTableRecord { Name = name, Dimtxsty = font, Dimscale = scale, Dimtxt = 2.5,
                Dimasz = 1.2, Dimexo = 1, Dimexe = 1.5, Dimgap = 0.8, Dimtad = 1, Dimdec = 2, Dimzin = 8,
                Dimclrd = Color.FromColorIndex(ColorMethod.ByAci, 8), Dimclre = Color.FromColorIndex(ColorMethod.ByAci, 8),
                Dimclrt = Color.FromColorIndex(ColorMethod.ByAci, 7) };
            table.Add(style); tx.AddNewlyCreatedDBObject(style, true); return style.ObjectId;
        }

        private static void EnsureLayer(Database database, Transaction tx, string name, short color, string linetype, LineWeight weight)
        {
            var table = (LayerTable)tx.GetObject(database.LayerTableId, OpenMode.ForRead);
            if (table.Has(name)) return;
            var types = (LinetypeTable)tx.GetObject(database.LinetypeTableId, OpenMode.ForRead);
            if (!types.Has(linetype))
            {
                try { database.LoadLineTypeFile(linetype, "acadiso.lin"); }
                catch (Autodesk.AutoCAD.Runtime.Exception) { database.LoadLineTypeFile(linetype, "acad.lin"); }
            }
            table.UpgradeOpen();
            var layer = new LayerTableRecord { Name = name, Color = Color.FromColorIndex(ColorMethod.ByAci, color),
                LineWeight = weight, LinetypeObjectId = types[linetype] };
            table.Add(layer); tx.AddNewlyCreatedDBObject(layer, true);
        }

        private static void Append(BlockTableRecord space, Transaction tx, Entity entity)
        {
            bool added = false;
            try { space.AppendEntity(entity); tx.AddNewlyCreatedDBObject(entity, true); added = true; }
            finally { if (!added) entity.Dispose(); }
        }

        private static double Distance2d(Point3d a, Point3d b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private sealed class Piece
        {
            public Piece(List<Point3d> points, bool closed, string layer) { Points = points; Closed = closed; Layer = layer; }
            public List<Point3d> Points { get; }
            public bool Closed { get; }
            public string Layer { get; }
            public bool Contains(Point3d point)
            {
                bool inside = false;
                for (int i = 0, j = Points.Count - 1; i < Points.Count; j = i++)
                {
                    Point3d a = Points[i], b = Points[j];
                    if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < a.X + (point.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y)) inside = !inside;
                }
                return inside;
            }
            public bool Intersects(Point3d center, double radius)
            {
                if (Closed && Contains(center)) return true;
                for (int i = 0; i < Points.Count - (Closed ? 0 : 1); i++)
                {
                    Point3d a = Points[i], b = Points[(i + 1) % Points.Count];
                    Vector3d ab = b - a; double length = ab.DotProduct(ab);
                    double t = length < 1e-12 ? 0 : Math.Max(0, Math.Min(1, (center - a).DotProduct(ab) / length));
                    if (Distance2d(center, a + ab * t) <= radius) return true;
                }
                return false;
            }
            public double Clearance(Point3d point)
            {
                double nearest = double.MaxValue;
                for (int i = 0; i < Points.Count; i++)
                {
                    Point3d a = Points[i], b = Points[(i + 1) % Points.Count];
                    Vector3d ab = b - a; double length = ab.DotProduct(ab);
                    double t = length < 1e-12 ? 0 : Math.Max(0, Math.Min(1, (point - a).DotProduct(ab) / length));
                    nearest = Math.Min(nearest, Distance2d(point, a + ab * t));
                }
                return nearest;
            }
        }
    }
}
