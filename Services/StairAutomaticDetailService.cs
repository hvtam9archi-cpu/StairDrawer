using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.Geometry;
using StairDrawer.Geometry;
using StairDrawer.Models;

namespace StairDrawer.Services
{
    internal sealed class StairAutomaticDetailResult
    {
        public IReadOnlyList<StairDetailResult> Details { get; set; }
        public Extents3d Bounds { get; set; }
    }

    internal sealed partial class StairDetailService
    {
        public StairAutomaticDetailResult CreateAutomatic(Database database, ObjectId sourceId)
        {
            // Both details, their source symbols and their groups commit as a single operation.
            using (Transaction tx = database.TransactionManager.StartTransaction())
            {
                var source = tx.GetObject(sourceId, OpenMode.ForRead) as BlockReference;
                if (source == null) throw new ArgumentException("Chọn mặt cắt thang dạng block hoặc toàn bộ nét của mặt cắt.");
                if (!source.ExtensionDictionary.IsNull
                    && ((DBDictionary)tx.GetObject(source.ExtensionDictionary, OpenMode.ForRead)).Contains(MetadataKey))
                    throw new ArgumentException("Chọn mặt cắt gốc để tự trích chi tiết.");
                var definition = (BlockTableRecord)tx.GetObject(source.BlockTableRecord, OpenMode.ForRead);
                var pieces = new List<Piece>();
                foreach (ObjectId id in definition)
                    CollectPieces(tx, (Entity)tx.GetObject(id, OpenMode.ForRead), source.BlockTransform, pieces, 0);
                var bricks = pieces.Where(p => p.Closed && p.Layer == StairLayers.Brick)
                    .Select(p => (IReadOnlyList<Point2>)p.Points.Select(v => new Point2(v.X, v.Y)).ToArray()).ToArray();
                var treads = new List<StairDetailTread>();
                foreach (Piece piece in pieces.Where(p => p.Layer == StairLayers.Finish && p.Closed))
                    if (TryTread(piece, out StairDetailTread tread) && StairAutomaticDetailGeometry.HasCutMaterial(tread, bricks)) treads.Add(tread);
                if (treads.Count < 2) throw new ArgumentException("Không tìm thấy hai bậc có cấu tạo cắt dọc để tự trích. Hãy chọn mặt cắt cắt dọc vế thang.");
                double slab = AutomaticSlabThickness(tx, source, pieces, treads[treads.Count / 2]);
                StairAutomaticDetailSelection selection = StairAutomaticDetailGeometry.Select(treads, slab);
                double elevation = pieces.First(p => p.Layer == StairLayers.Finish && p.Closed && TryTread(p, out StairDetailTread tread)).Points[0].Z;
                double paper = NumericValidation.IsFinite(database.Dimscale) && database.Dimscale >= 10 && database.Dimscale <= 1000
                    ? database.Dimscale : 50;
                Extents3d sourceBounds = source.GeometricExtents;
                var space = (BlockTableRecord)tx.GetObject(source.OwnerId, OpenMode.ForRead);
                var occupied = new List<Extents3d>();
                foreach (ObjectId id in space)
                    if (tx.GetObject(id, OpenMode.ForRead) is Entity entity && entity.Visible
                        && AutomaticEntityBounds(tx, entity, out Extents3d bounds)) occupied.Add(bounds);

                int number = int.Parse(NextNumber(database), CultureInfo.InvariantCulture);
                string thickness = selection.StoneThickness.ToString("0.##", CultureInfo.InvariantCulture);
                string treadNote = "ĐÁ ỐP MẶT BẬC DÀY " + thickness + " MM";
                string riserNote = "ĐÁ ỐP CỔ BẬC DÀY " + thickness + " MM";
                var treadOptions = new StairDetailOptions(selection.TreadRadius, paper, 10, number.ToString("00", CultureInfo.InvariantCulture),
                    "CHI TIẾT BẬC THANG", true, true, true, true, false, "", treadNote, riserNote)
                    .WithGeometry(selection.FrameAngle, selection.StoneThickness);
                var noseOptions = new StairDetailOptions(selection.NosingRadius, paper, 5, (number + 1).ToString("00", CultureInfo.InvariantCulture),
                    "CHI TIẾT MŨI BẬC", false, true, true, true, false, "", treadNote, riserNote, "", "")
                    .WithGeometry(selection.FrameAngle, selection.StoneThickness);
                double column = sourceBounds.MaxPoint.X + paper * 30;
                Point3d center = new Point3d(selection.TreadCenter.X, selection.TreadCenter.Y, elevation);
                Point3d noseCenter = new Point3d(selection.NosingCenter.X, selection.NosingCenter.Y, elevation);
                double radius = treadOptions.Radius * treadOptions.Magnification;
                var treadDetail = Create(database, sourceId, treadOptions, center,
                    new Point3d(column + radius, sourceBounds.MaxPoint.Y - radius - paper * 8, elevation));
                if (treadDetail.DimensionCount < 2) throw new ArgumentException("Chưa nhận diện đủ các mặt bậc để tự ghi kích thước thực.");
                var noseDetail = Create(database, sourceId, noseOptions, noseCenter,
                    new Point3d(column + noseOptions.Radius * noseOptions.Magnification, sourceBounds.MinPoint.Y, elevation));
                Extents3d treadBounds = AutomaticDetailBounds(tx, treadDetail);
                Extents3d noseBounds = AutomaticDetailBounds(tx, noseDetail);
                Vector3d treadMove = new Vector3d(column - treadBounds.MinPoint.X, sourceBounds.MaxPoint.Y - treadBounds.MaxPoint.Y, 0);
                treadBounds.TransformBy(Matrix3d.Displacement(treadMove));
                Vector3d noseMove = new Vector3d(column - noseBounds.MinPoint.X, treadBounds.MinPoint.Y - paper * 8 - noseBounds.MaxPoint.Y, 0);
                noseBounds.TransformBy(Matrix3d.Displacement(noseMove));
                var batch = treadBounds; batch.AddExtents(noseBounds);
                double shift = 0;
                // Advance the entire column past existing geometry; preserve the stacked layout.
                for (int attempt = 0; attempt <= occupied.Count; attempt++)
                {
                    var hits = occupied.Where(b => AutomaticOverlaps(batch, b, paper * 4)).ToArray();
                    if (hits.Length == 0) break;
                    double move = hits.Max(b => b.MaxPoint.X) + paper * 12 - batch.MinPoint.X;
                    if (move <= 0) throw new InvalidOperationException("Không tìm được khoảng trống để bố trí chi tiết.");
                    batch.TransformBy(Matrix3d.Displacement(Vector3d.XAxis * move)); shift += move;
                }
                MoveAutomaticDetail(tx, treadDetail, treadMove + Vector3d.XAxis * shift);
                MoveAutomaticDetail(tx, noseDetail, noseMove + Vector3d.XAxis * shift);
                AddAutomaticGroup(database, tx, treadDetail, "Chi tiết bậc thang " + treadOptions.Number);
                AddAutomaticGroup(database, tx, noseDetail, "Chi tiết mũi bậc " + noseOptions.Number);
                Extents3d overall = batch; overall.AddExtents(sourceBounds);
                foreach (ObjectId id in treadDetail.SourceEntityIds.Concat(noseDetail.SourceEntityIds))
                    if (AutomaticEntityBounds(tx, (Entity)tx.GetObject(id, OpenMode.ForRead), out Extents3d bounds)) overall.AddExtents(bounds);
                var result = new StairAutomaticDetailResult { Details = new[] { treadDetail, noseDetail }, Bounds = overall };
                tx.Commit();
                return result;
            }
        }

        private static double AutomaticSlabThickness(Transaction tx, BlockReference source, IReadOnlyList<Piece> pieces, StairDetailTread tread)
        {
            if (!source.ExtensionDictionary.IsNull)
            {
                var dict = (DBDictionary)tx.GetObject(source.ExtensionDictionary, OpenMode.ForRead);
                if (dict.Contains("STAIRDRAW_SECTION_V1"))
                    using (ResultBuffer data = ((Xrecord)tx.GetObject(dict.GetAt("STAIRDRAW_SECTION_V1"), OpenMode.ForRead)).Data)
                    {
                        var values = data?.AsArray();
                        if (values != null && values.Length > 4 && values[4].Value is double thickness && thickness > 0)
                            return thickness * Vector3d.YAxis.TransformBy(source.BlockTransform).Length;
                    }
            }
            double x = StairDetailTread.Dot(tread.Nosing, tread.Along), y = StairDetailTread.Dot(tread.Nosing, tread.Up);
            double best = double.MaxValue, depth = 120;
            foreach (Piece piece in pieces.Where(p => p.Closed && p.Layer == StairLayers.Concrete))
            {
                var hits = new List<double>();
                for (int i = 0; i < piece.Points.Count; i++)
                {
                    Point3d a = piece.Points[i], b = piece.Points[(i + 1) % piece.Points.Count];
                    double ax = a.X * tread.Along.X + a.Y * tread.Along.Y, bx = b.X * tread.Along.X + b.Y * tread.Along.Y;
                    if (Math.Abs(ax - bx) < 1e-8) continue;
                    double t = (x - ax) / (bx - ax);
                    if (t < 0 || t > 1) continue;
                    hits.Add((a.X + (b.X - a.X) * t) * tread.Up.X + (a.Y + (b.Y - a.Y) * t) * tread.Up.Y);
                }
                if (hits.Count < 2) continue;
                double top = hits.Max(), bottom = hits.Min();
                if (top <= y && y - top < best && top - bottom > 0)
                { best = y - top; depth = top - bottom; }
            }
            return depth;
        }

        private static Extents3d AutomaticDetailBounds(Transaction tx, StairDetailResult result)
        {
            Extents3d? bounds = null;
            foreach (ObjectId id in result.DetailEntityIds.Where(id => id != result.BlockId))
            {
                var entity = (Entity)tx.GetObject(id, OpenMode.ForRead);
                if (entity is Dimension dim) { if (!dim.IsWriteEnabled) dim.UpgradeOpen(); dim.RecomputeDimensionBlock(true); }
                if (!AutomaticEntityBounds(tx, entity, out Extents3d part)) continue;
                if (bounds.HasValue) { var combined = bounds.Value; combined.AddExtents(part); bounds = combined; }
                else bounds = part;
            }
            if (!bounds.HasValue) throw new InvalidOperationException("Không xác định được khung bố trí chi tiết.");
            return bounds.Value;
        }

        private static bool AutomaticEntityBounds(Transaction tx, Entity entity, out Extents3d bounds)
        {
            try
            {
                if (entity is BlockReference block && !block.ExtensionDictionary.IsNull)
                {
                    var extension = (DBDictionary)tx.GetObject(block.ExtensionDictionary, OpenMode.ForRead);
                    if (extension.Contains(MetadataKey) && extension.Contains("ACAD_FILTER"))
                    {
                        var filters = (DBDictionary)tx.GetObject(extension.GetAt("ACAD_FILTER"), OpenMode.ForRead);
                        if (filters.Contains("SPATIAL"))
                        {
                            var crop = (SpatialFilter)tx.GetObject(filters.GetAt("SPATIAL"), OpenMode.ForRead);
                            Point3d[] points = crop.Definition.GetPoints().Cast<Point2d>()
                                .Select(p => new Point3d(p.X, p.Y, 0).TransformBy(block.BlockTransform)).ToArray();
                            bounds = new Extents3d(points[0], points[0]);
                            foreach (Point3d point in points) bounds.AddPoint(point);
                            return true;
                        }
                    }
                }
                bounds = entity.GeometricExtents;
                return true;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception) { bounds = default; return false; }
        }

        private static bool AutomaticOverlaps(Extents3d a, Extents3d b, double gap) =>
            a.MinPoint.X < b.MaxPoint.X + gap && a.MaxPoint.X > b.MinPoint.X - gap
                && a.MinPoint.Y < b.MaxPoint.Y + gap && a.MaxPoint.Y > b.MinPoint.Y - gap;

        private static void MoveAutomaticDetail(Transaction tx, StairDetailResult result, Vector3d move)
        {
            Matrix3d matrix = Matrix3d.Displacement(move);
            foreach (ObjectId id in result.DetailEntityIds)
            {
                var entity = (Entity)tx.GetObject(id, OpenMode.ForWrite); entity.TransformBy(matrix);
                if (entity is Dimension dimension) dimension.RecomputeDimensionBlock(true);
            }
        }

        private static void AddAutomaticGroup(Database database, Transaction tx, StairDetailResult result, string description)
        {
            var groups = (DBDictionary)tx.GetObject(database.GroupDictionaryId, OpenMode.ForWrite);
            var group = new Group(description, true);
            groups.SetAt("STAIR_DETAIL_SET_" + Guid.NewGuid().ToString("N"), group); tx.AddNewlyCreatedDBObject(group, true);
            group.Append(new ObjectIdCollection(result.DetailEntityIds.ToArray()));
        }
    }
}
