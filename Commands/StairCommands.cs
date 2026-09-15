using System;
using System.Diagnostics;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using StairDrawer.Geometry;
using StairDrawer.Interaction;
using StairDrawer.Models;
using StairDrawer.Services;
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
            ExecuteSafely(
                CommandNames.Draw,
                document =>
                {
                    if (!StairInteraction.TryGetDrawingMode(document.Editor, out StairDrawingMode mode))
                    {
                        return;
                    }

                    if (mode == StairDrawingMode.Plan)
                    {
                        DrawPlanCore(document);
                    }
                    else
                    {
                        DrawSectionCore(document);
                    }
                });
        }

        [CommandMethod(CommandNames.DrawPlan, CommandFlags.Modal)]
        public void DrawPlan()
        {
            ExecuteSafely(CommandNames.DrawPlan, DrawPlanCore);
        }

        [CommandMethod(CommandNames.DrawSection, CommandFlags.Modal)]
        public void DrawSection()
        {
            ExecuteSafely(CommandNames.DrawSection, DrawSectionCore);
        }

        private void DrawPlanCore(Document document)
        {
            Editor editor = document.Editor;
            if (!StairInteraction.TryGetPlanParameters(editor, out StairPlanParameters parameters)
                || !StairInteraction.TryGetPlacement(editor, out StairPlacement placement))
            {
                return;
            }

            DrawingModel drawing = StairPlanGeometry.Create(parameters);
            int count = _drawingService.Draw(document.Database, drawing, placement);
            editor.WriteMessage("\nDa ve mat bang cau thang ({0} doi tuong).", count);
        }

        private void DrawSectionCore(Document document)
        {
            Editor editor = document.Editor;
            if (!StairInteraction.TryGetSectionParameters(editor, out StairSectionParameters parameters)
                || !StairInteraction.TryGetPlacement(editor, out StairPlacement placement))
            {
                return;
            }

            DrawingModel drawing = StairSectionGeometry.Create(parameters);
            int count = _drawingService.Draw(document.Database, drawing, placement);
            editor.WriteMessage("\nDa ve mat cat cau thang ({0} doi tuong).", count);
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
