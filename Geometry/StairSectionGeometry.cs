using System;
using System.Globalization;
using StairDrawer.Models;

namespace StairDrawer.Geometry
{
    internal static class StairSectionGeometry
    {
        public static DrawingModel Create(StairSectionParameters parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            var drawing = new DrawingModel();
            double riserHeight = parameters.RiserHeight;
            int treadCount = parameters.TotalRisers - 1;
            double run = treadCount * parameters.TreadDepth;
            double inclinedRise = treadCount * riserHeight;
            double textHeight = Math.Max(100.0, parameters.TreadDepth * 0.45);

            AddConcreteSlab(drawing, parameters, run, inclinedRise);
            AddBrickStepsAndFinishes(drawing, parameters, riserHeight, treadCount, run);
            AddReinforcement(drawing, parameters, run, inclinedRise);
            AddAnnotations(drawing, parameters, run, inclinedRise, textHeight);

            return drawing;
        }

        private static void AddConcreteSlab(
            DrawingModel drawing,
            StairSectionParameters parameters,
            double run,
            double inclinedRise)
        {
            Point2 slopeStart = new Point2(0.0, 0.0);
            Point2 slopeEnd = new Point2(run, inclinedRise);
            Point2 downNormal = new Point2(inclinedRise, -run).Normalize();
            Point2 slabOffset = downNormal * parameters.WaistSlabThickness;

            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    slopeStart,
                    slopeEnd,
                    slopeEnd + slabOffset,
                    slopeStart + slabOffset
                },
                true,
                StairLayers.Concrete));

            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    new Point2(-parameters.LandingLength, 0.0),
                    new Point2(0.0, 0.0),
                    new Point2(0.0, -parameters.WaistSlabThickness),
                    new Point2(-parameters.LandingLength, -parameters.WaistSlabThickness)
                },
                true,
                StairLayers.Concrete));

            drawing.Polylines.Add(new PolylinePrimitive(
                new[]
                {
                    new Point2(run, parameters.FloorHeight),
                    new Point2(run + parameters.LandingLength, parameters.FloorHeight),
                    new Point2(run + parameters.LandingLength, parameters.FloorHeight - parameters.WaistSlabThickness),
                    new Point2(run, parameters.FloorHeight - parameters.WaistSlabThickness)
                },
                true,
                StairLayers.Concrete));
        }

        private static void AddBrickStepsAndFinishes(
            DrawingModel drawing,
            StairSectionParameters parameters,
            double riserHeight,
            int treadCount,
            double run)
        {
            for (int treadIndex = 0; treadIndex < treadCount; treadIndex++)
            {
                double x0 = treadIndex * parameters.TreadDepth;
                double x1 = (treadIndex + 1) * parameters.TreadDepth;
                double y0 = treadIndex * riserHeight;
                double y1 = (treadIndex + 1) * riserHeight;

                drawing.Polylines.Add(new PolylinePrimitive(
                    new[]
                    {
                        new Point2(x0, y0),
                        new Point2(x0, y1),
                        new Point2(x1, y1)
                    },
                    true,
                    StairLayers.Brick));

                AddFinishStrip(
                    drawing,
                    new Point2(x0, y1),
                    new Point2(x1, y1),
                    parameters.FinishThickness,
                    true);

                AddFinishStrip(
                    drawing,
                    new Point2(x0, y0),
                    new Point2(x0, y1),
                    parameters.FinishThickness,
                    false);
            }

            double lastTreadLevel = treadCount * riserHeight;
            AddFinishStrip(
                drawing,
                new Point2(run, lastTreadLevel),
                new Point2(run, parameters.FloorHeight),
                parameters.FinishThickness,
                false);

            AddFinishStrip(
                drawing,
                new Point2(run, parameters.FloorHeight),
                new Point2(run + parameters.LandingLength, parameters.FloorHeight),
                parameters.FinishThickness,
                true);
        }

        private static void AddFinishStrip(
            DrawingModel drawing,
            Point2 start,
            Point2 end,
            double thickness,
            bool horizontal)
        {
            if (thickness <= 1e-9)
            {
                drawing.Lines.Add(new LinePrimitive(start, end, StairLayers.Finish));
                return;
            }

            Point2 offset = horizontal
                ? new Point2(0.0, thickness)
                : new Point2(-thickness, 0.0);

            drawing.Polylines.Add(new PolylinePrimitive(
                new[] { start, end, end + offset, start + offset },
                true,
                StairLayers.Finish));
        }

        private static void AddReinforcement(
            DrawingModel drawing,
            StairSectionParameters parameters,
            double run,
            double inclinedRise)
        {
            Point2 slopeStart = new Point2(0.0, 0.0);
            Point2 slopeEnd = new Point2(run, inclinedRise);
            Point2 downNormal = new Point2(inclinedRise, -run).Normalize();
            double rebarOffset = parameters.WaistSlabThickness - parameters.ConcreteCover;
            Point2 rebarStart = slopeStart + (downNormal * rebarOffset);
            Point2 rebarEnd = slopeEnd + (downNormal * rebarOffset);

            drawing.Lines.Add(new LinePrimitive(
                new Point2(-parameters.LandingLength * 0.65, rebarStart.Y),
                rebarStart,
                StairLayers.Rebar));
            drawing.Lines.Add(new LinePrimitive(rebarStart, rebarEnd, StairLayers.Rebar));
            drawing.Lines.Add(new LinePrimitive(
                rebarEnd,
                new Point2(run + (parameters.LandingLength * 0.65), rebarEnd.Y),
                StairLayers.Rebar));

            Point2 rebarVector = rebarEnd - rebarStart;
            double symbolRadius = Math.Max(8.0, parameters.TreadDepth * 0.035);
            for (int symbolIndex = 1; symbolIndex <= 6; symbolIndex++)
            {
                double ratio = symbolIndex / 7.0;
                drawing.Circles.Add(new CirclePrimitive(
                    rebarStart + (rebarVector * ratio),
                    symbolRadius,
                    StairLayers.Rebar));
            }
        }

        private static void AddAnnotations(
            DrawingModel drawing,
            StairSectionParameters parameters,
            double run,
            double inclinedRise,
            double textHeight)
        {
            double midX = run * 0.5;
            double midSlopeY = inclinedRise * 0.5;

            drawing.Texts.Add(new TextPrimitive(
                new Point2(-parameters.LandingLength, -parameters.WaistSlabThickness - (textHeight * 2.2)),
                string.Format(
                    CultureInfo.InvariantCulture,
                    "MAT CAT CAU THANG - {0} BAC, h={1:0.#}, b={2:0.#}",
                    parameters.TotalRisers,
                    parameters.RiserHeight,
                    parameters.TreadDepth),
                textHeight,
                0.0,
                StairLayers.Annotation));

            AddCallout(
                drawing,
                new Point2(midX, midSlopeY - (parameters.WaistSlabThickness * 0.55)),
                new Point2(midX + (parameters.TreadDepth * 2.0), midSlopeY - (parameters.WaistSlabThickness * 3.0)),
                string.Format(CultureInfo.InvariantCulture, "BAN THANG BTCT d={0:0.#}", parameters.WaistSlabThickness),
                textHeight);

            AddCallout(
                drawing,
                new Point2(midX, midSlopeY + (parameters.RiserHeight * 0.65)),
                new Point2(midX + (parameters.TreadDepth * 2.0), midSlopeY + (parameters.RiserHeight * 3.0)),
                "BAC XAY GACH",
                textHeight);

            AddCallout(
                drawing,
                new Point2(parameters.TreadDepth * 1.5, parameters.RiserHeight * 2.0),
                new Point2(-parameters.LandingLength, parameters.RiserHeight * 3.0),
                string.Format(CultureInfo.InvariantCulture, "MAT BAC + CO BAC day {0:0.#}", parameters.FinishThickness),
                textHeight);

            AddCallout(
                drawing,
                new Point2(midX, midSlopeY - parameters.WaistSlabThickness),
                new Point2(midX - (parameters.TreadDepth * 2.0), midSlopeY - (parameters.WaistSlabThickness * 5.0)),
                "THEP CHIU LUC %%c12a150 (THAM KHAO)",
                textHeight);

            drawing.Texts.Add(new TextPrimitive(
                new Point2(-parameters.LandingLength, -parameters.WaistSlabThickness - (textHeight * 4.0)),
                "LUU Y: KICH THUOC VA COT THEP PHAI DUOC KY SU KET CAU KIEM TRA",
                textHeight * 0.85,
                0.0,
                StairLayers.Annotation));
        }

        private static void AddCallout(
            DrawingModel drawing,
            Point2 target,
            Point2 textPosition,
            string text,
            double textHeight)
        {
            drawing.Lines.Add(new LinePrimitive(target, textPosition, StairLayers.Annotation));
            drawing.Texts.Add(new TextPrimitive(
                textPosition,
                text,
                textHeight,
                0.0,
                StairLayers.Annotation));
        }
    }
}
