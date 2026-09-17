using System;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
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
        public const string DrawPlan = "STAIRPLAN";
        public const string DrawSection = "STAIRSECTION";
    }

    public sealed class StairCommands
    {
        private readonly StairDrawingService _drawingService = new StairDrawingService();

        [CommandMethod(CommandNames.Draw, CommandFlags.Modal)]
        public void DrawStair()
        {
            ExecuteSafely(CommandNames.Draw, document => ShowConfigurationWindow(
                document,
                StairDrawingMode.Plan,
                true));
        }

        [CommandMethod(CommandNames.DrawPlan, CommandFlags.Modal)]
        public void DrawPlan()
        {
            ExecuteSafely(CommandNames.DrawPlan, document => ShowConfigurationWindow(
                document,
                StairDrawingMode.Plan,
                false));
        }

        [CommandMethod(CommandNames.DrawSection, CommandFlags.Modal)]
        public void DrawSection()
        {
            ExecuteSafely(CommandNames.DrawSection, document => ShowConfigurationWindow(
                document,
                StairDrawingMode.Section,
                false));
        }

        private void ShowConfigurationWindow(
            Document document,
            StairDrawingMode initialDrawingMode,
            bool allowDrawingModeSelection)
        {
            var window = new StairConfigurationWindow(initialDrawingMode, allowDrawingModeSelection);
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

            int count = _drawingService.Draw(document.Database, drawing, placement);
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
