using System;
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
                    MessageForAdding = "\nChọn block hoặc chọn toàn bộ các nét thuộc mặt cắt thang cần trích: ",
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
            var dialog = new StairDetailWindow(StairDetailService.StoneThickness(document.Database, sourceId),
                document.Database.Dimscale >= 5 ? document.Database.Dimscale : 50,
                StairDetailService.NextNumber(document.Database));
            if (Application.ShowModalWindow(dialog) != true) return;
            double elevation;
            using (Transaction tx = document.Database.TransactionManager.StartOpenCloseTransaction())
                elevation = ((BlockReference)tx.GetObject(sourceId, OpenMode.ForRead)).GeometricExtents.MinPoint.Z;
            var center = new StairDetailCircleJig(dialog.Options.Radius, elevation);
            if (document.Editor.Drag(center).Status != PromptStatus.OK) return;
            var destination = new StairDetailCircleJig(dialog.Options.Radius * dialog.Options.Magnification,
                null, center.Position, dialog.Options.Radius);
            if (document.Editor.Drag(destination).Status != PromptStatus.OK) return;
            StairDetailResult result = new StairDetailService().Create(document.Database, sourceId, dialog.Options,
                center.Position, destination.Position);
            document.Editor.WriteMessage("\nĐã trích chi tiết {0}: {1} dim, {2} ghi chú vật liệu{3}.",
                dialog.Options.Number, result.DimensionCount, result.NoteCount, result.UsedCallout ? ", đường nối bằng Callout" : "");
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
                document.Editor.WriteMessage("\nĐã vẽ mặt cắt dạng block; chọn vùng cần phóng chi tiết.");
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
