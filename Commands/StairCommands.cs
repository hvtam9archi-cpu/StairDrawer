using System;
using System.Linq;
using Trace = System.Diagnostics.Trace;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using StairDrawer.Geometry;
using StairDrawer.Interaction;
using StairDrawer.Models;
using StairDrawer.Services;
using StairDrawer.UI;
using AutoCADException = Autodesk.AutoCAD.Runtime.Exception;

namespace StairDrawer.Commands
{
    internal static class CommandNames
    {
        public const string Draw = "STAIRDRAW";
        public const string Detail = "STAIRDETAIL";
    }

    public sealed class StairCommands
    {
        private readonly StairDrawingService _drawingService = new StairDrawingService();
        private StairDrawingConfiguration _previousConfiguration;

        [CommandMethod(CommandNames.Draw, CommandFlags.Modal)]
        public void DrawStair()
        {
            ExecuteSafely(CommandNames.Draw, document => ShowConfigurationWindow(
                document,
                StairDrawingMode.Plan,
                true));
        }

        [CommandMethod(CommandNames.Detail, CommandFlags.Modal)]
        public void ExtractStairDetail()
        {
            ExecuteSafely(CommandNames.Detail, document =>
            {
                var prompt = new PromptSelectionOptions
                {
                    MessageForAdding = "\nChọn mặt cắt thang để tự trích bậc thang và mũi bậc (block hoặc các nét thuộc mặt cắt): ",
                    AllowDuplicates = false
                };
                PromptSelectionResult selection = document.Editor.GetSelection(prompt);
                if (selection.Status != PromptStatus.OK) return;
                ObjectId[] selected = selection.Value.GetObjectIds();
                bool isBlock;
                using (Transaction tx = document.Database.TransactionManager.StartOpenCloseTransaction())
                    isBlock = selected.Length == 1 && tx.GetObject(selected[0], OpenMode.ForRead) is BlockReference;
                ObjectId sourceId = isBlock ? selected[0]
                    : StairDetailService.CreateTemporarySource(document.Database, new ObjectIdCollection(selected));
                try { ExtractDetail(document, sourceId); }
                finally { if (!isBlock) StairDetailService.RemoveTemporarySource(document.Database, sourceId); }
            });
        }

        private static void ExtractDetail(Document document, ObjectId sourceId)
        {
            StairAutomaticDetailResult result = new StairDetailService().CreateAutomatic(document.Database, sourceId);
            document.Editor.WriteMessage("\nĐã tự trích chi tiết bậc thang 1:10 và mũi bậc 1:5: {0} dim, {1} ghi chú. Các hình được nhóm và đặt ở khoảng trống bên phải mặt cắt.",
                result.Details.Sum(d => d.DimensionCount), result.Details.Sum(d => d.NoteCount));
            try
            {
                using (ViewTableRecord view = document.Editor.GetCurrentView())
                {
                    Matrix3d display = Matrix3d.PlaneToWorld(view.ViewDirection);
                    display = Matrix3d.Displacement(view.Target - Point3d.Origin) * display;
                    display = Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target) * display;
                    Extents3d bounds = result.Bounds; bounds.TransformBy(display.Inverse());
                    double ratio = view.Height > 0 ? view.Width / view.Height : 1.5;
                    double width = Math.Max(1, bounds.MaxPoint.X - bounds.MinPoint.X) * 1.12;
                    double height = Math.Max(1, bounds.MaxPoint.Y - bounds.MinPoint.Y) * 1.12;
                    if (width / height > ratio) height = width / ratio; else width = height * ratio;
                    view.Width = width; view.Height = height;
                    view.CenterPoint = new Point2d((bounds.MinPoint.X + bounds.MaxPoint.X) * .5, (bounds.MinPoint.Y + bounds.MaxPoint.Y) * .5);
                    document.Editor.SetCurrentView(view);
                }
            }
            catch (System.Exception error) { Trace.TraceWarning("Cannot focus automatic stair details: {0}", error.Message); }
        }

        private void ShowConfigurationWindow(
            Document document,
            StairDrawingMode initialDrawingMode,
            bool allowDrawingModeSelection)
        {
            var window = new StairConfigurationWindow(initialDrawingMode, allowDrawingModeSelection, _previousConfiguration);
            bool? dialogResult = Application.ShowModalWindow(window);
            StairDrawingConfiguration configuration = window.Configuration;
            if (dialogResult != true
                || configuration == null
                || !StairInteraction.TryGetPlacement(document.Editor, out StairPlacement placement))
            {
                return;
            }

            DrawingModel drawing;
            string drawingLabel;
            double riserHeight;
            if (configuration.DrawingMode == StairDrawingMode.Plan)
            {
                drawing = StairPlanGeometry.Create(configuration.PlanParameters);
                drawingLabel = "mat bang";
                riserHeight = configuration.PlanParameters.RiserHeight;
            }
            else
            {
                drawing = StairSectionGeometry.Create(configuration.SectionParameters);
                drawingLabel = "mat cat";
                riserHeight = configuration.SectionParameters.RiserHeight;
            }

            if (window.ExtractDetail && configuration.DrawingMode == StairDrawingMode.Section)
            {
                ObjectId sourceId = _drawingService.DrawAsBlock(document.Database, drawing, placement, configuration.SectionParameters);
                _previousConfiguration = configuration;
                document.Editor.WriteMessage("\nĐã vẽ mặt cắt dạng block; đang tự trích chi tiết bậc thang và mũi bậc.");
                ExtractDetail(document, sourceId);
                return;
            }

            int count = _drawingService.Draw(document.Database, drawing, placement);
            _previousConfiguration = configuration;
            document.Editor.WriteMessage(
                "\nDa ve {0} cau thang ({1} doi tuong).",
                drawingLabel,
                count);

            if (riserHeight < 120.0 || riserHeight > 200.0)
            {
                document.Editor.WriteMessage(
                    "\nCanh bao: chieu cao co bac {0:0.#} nam ngoai khoang tham khao 120-200.",
                    riserHeight);
            }
        }

        private static void ExecuteSafely(string commandName, Action<Document> action)
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                Trace.TraceWarning("{0}: no active AutoCAD document.", commandName);
                return;
            }

            try
            {
                action(document);
            }
            catch (AutoCADException exception)
            {
                Trace.TraceError(
                    "{0} failed in drawing '{1}': {2}",
                    commandName,
                    document.Name,
                    exception);
                document.Editor.WriteMessage(
                    "\n{0} loi AutoCAD: {1}",
                    commandName,
                    exception.Message);
            }
            catch (System.Exception exception)
            {
                Trace.TraceError(
                    "{0} failed in drawing '{1}': {2}",
                    commandName,
                    document.Name,
                    exception);
                document.Editor.WriteMessage(
                    "\n{0} khong the hoan tat: {1}",
                    commandName,
                    exception.Message);
            }
        }
    }
}
