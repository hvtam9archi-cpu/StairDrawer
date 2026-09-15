using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using StairDrawer.Models;

namespace StairDrawer.Interaction
{
    internal enum StairDrawingMode
    {
        Plan,
        Section
    }

    internal sealed class StairPlacement
    {
        public StairPlacement(Point3d origin, Vector3d xAxis)
        {
            Origin = origin;
            XAxis = xAxis;
        }

        public Point3d Origin { get; }

        public Vector3d XAxis { get; }
    }

    internal static class StairInteraction
    {
        private const double DefaultFloorHeight = 3300.0;
        private const int DefaultRisers = 20;
        private const double DefaultTreadDepth = 270.0;
        private const double DefaultStairWidth = 1000.0;
        private const double DefaultLandingDepth = 1000.0;
        private const double DefaultFlightGap = 100.0;
        private const double DefaultPlanCutHeight = 1200.0;
        private const double DefaultWaistSlabThickness = 120.0;
        private const double DefaultFinishThickness = 20.0;
        private const double DefaultConcreteCover = 25.0;

        public static bool TryGetDrawingMode(Editor editor, out StairDrawingMode mode)
        {
            var options = new PromptKeywordOptions("\nChon noi dung can ve [Plan/Section] <Plan>: ")
            {
                AllowNone = true
            };
            options.Keywords.Add("Plan");
            options.Keywords.Add("Section");
            options.Keywords.Default = "Plan";

            PromptResult result = editor.GetKeywords(options);
            if (result.Status == PromptStatus.Cancel)
            {
                mode = default(StairDrawingMode);
                return false;
            }

            if (result.Status != PromptStatus.OK && result.Status != PromptStatus.None)
            {
                mode = default(StairDrawingMode);
                return false;
            }

            mode = result.Status == PromptStatus.None || result.StringResult == "Plan"
                ? StairDrawingMode.Plan
                : StairDrawingMode.Section;
            return true;
        }

        public static bool TryGetPlanParameters(Editor editor, out StairPlanParameters parameters)
        {
            parameters = null;
            if (!TryGetPlanType(editor, out StairPlanType planType)
                || !TryGetDouble(editor, "Chieu cao tang", DefaultFloorHeight, 500.0, 10000.0, out double floorHeight)
                || !TryGetInteger(editor, "Tong so bac (so co bac)", DefaultRisers, 2, 60, out int totalRisers)
                || !TryGetDouble(editor, "Chieu rong mat bac", DefaultTreadDepth, 150.0, 500.0, out double treadDepth)
                || !TryGetDouble(editor, "Chieu rong ve thang", DefaultStairWidth, 600.0, 5000.0, out double stairWidth)
                || !TryGetDouble(
                    editor,
                    "Cao do mat phang cat bang",
                    System.Math.Min(DefaultPlanCutHeight, floorHeight * 0.9),
                    100.0,
                    floorHeight - 1.0,
                    out double planCutHeight))
            {
                return false;
            }

            if (planType != StairPlanType.Straight && totalRisers < 4)
            {
                editor.WriteMessage("\nThang L/U can toi thieu 4 bac.");
                return false;
            }

            double landingDepth = stairWidth;
            if (planType != StairPlanType.Straight
                && !TryGetDouble(
                    editor,
                    "Chieu sau chieu nghi",
                    System.Math.Max(DefaultLandingDepth, stairWidth),
                    stairWidth,
                    5000.0,
                    out landingDepth))
            {
                return false;
            }

            double flightGap = 0.0;
            if (planType == StairPlanType.UShape
                && !TryGetDouble(
                    editor,
                    "Khe giua hai ve thang",
                    DefaultFlightGap,
                    0.0,
                    1000.0,
                    out flightGap,
                    true))
            {
                return false;
            }

            parameters = new StairPlanParameters(
                planType,
                floorHeight,
                totalRisers,
                treadDepth,
                stairWidth,
                landingDepth,
                flightGap,
                planCutHeight);

            WarnAboutRiserHeight(editor, parameters.RiserHeight);
            return true;
        }

        public static bool TryGetSectionParameters(Editor editor, out StairSectionParameters parameters)
        {
            parameters = null;
            if (!TryGetDouble(editor, "Chieu cao tang", DefaultFloorHeight, 500.0, 10000.0, out double floorHeight)
                || !TryGetInteger(editor, "Tong so bac (so co bac)", DefaultRisers, 2, 60, out int totalRisers)
                || !TryGetDouble(editor, "Chieu rong mat bac", DefaultTreadDepth, 150.0, 500.0, out double treadDepth)
                || !TryGetDouble(editor, "Chieu dai san chieu nghi", DefaultStairWidth, 600.0, 5000.0, out double landingLength)
                || !TryGetDouble(editor, "Chieu day ban thang BTCT", DefaultWaistSlabThickness, 80.0, 500.0, out double slabThickness)
                || !TryGetDouble(editor, "Chieu day lop hoan thien", DefaultFinishThickness, 0.0, 100.0, out double finishThickness, true)
                || !TryGetDouble(
                    editor,
                    "Lop bao ve cot thep",
                    DefaultConcreteCover,
                    5.0,
                    slabThickness - 1.0,
                    out double concreteCover))
            {
                return false;
            }

            parameters = new StairSectionParameters(
                floorHeight,
                totalRisers,
                treadDepth,
                landingLength,
                slabThickness,
                finishThickness,
                concreteCover);

            WarnAboutRiserHeight(editor, parameters.RiserHeight);
            return true;
        }

        public static bool TryGetPlacement(Editor editor, out StairPlacement placement)
        {
            placement = null;
            PromptPointResult originResult = editor.GetPoint(new PromptPointOptions("\nChon diem chen (WCS XY): "));
            if (originResult.Status != PromptStatus.OK)
            {
                return false;
            }

            Point3d origin = originResult.Value;
            var directionOptions = new PromptPointOptions("\nChon diem xac dinh huong truc X <WCS +X>: ")
            {
                AllowNone = true,
                BasePoint = origin,
                UseBasePoint = true,
                UseDashedLine = true
            };

            PromptPointResult directionResult = editor.GetPoint(directionOptions);
            if (directionResult.Status == PromptStatus.Cancel)
            {
                return false;
            }

            Vector3d xAxis = Vector3d.XAxis;
            if (directionResult.Status == PromptStatus.OK)
            {
                Vector3d direction = directionResult.Value - origin;
                xAxis = new Vector3d(direction.X, direction.Y, 0.0);
                if (xAxis.Length <= 1e-9)
                {
                    editor.WriteMessage("\nHuong ve phai co do dai khac 0 tren mat phang WCS XY.");
                    return false;
                }

                xAxis = xAxis.GetNormal();
            }
            else if (directionResult.Status != PromptStatus.None)
            {
                return false;
            }

            placement = new StairPlacement(new Point3d(origin.X, origin.Y, origin.Z), xAxis);
            return true;
        }

        private static bool TryGetPlanType(Editor editor, out StairPlanType planType)
        {
            var options = new PromptKeywordOptions("\nChon loai mat bang [Straight/L/U] <U>: ")
            {
                AllowNone = true
            };
            options.Keywords.Add("Straight");
            options.Keywords.Add("L");
            options.Keywords.Add("U");
            options.Keywords.Default = "U";

            PromptResult result = editor.GetKeywords(options);
            if (result.Status == PromptStatus.Cancel)
            {
                planType = default(StairPlanType);
                return false;
            }

            if (result.Status != PromptStatus.OK && result.Status != PromptStatus.None)
            {
                planType = default(StairPlanType);
                return false;
            }

            string value = result.Status == PromptStatus.None ? "U" : result.StringResult;
            switch (value)
            {
                case "Straight":
                    planType = StairPlanType.Straight;
                    return true;

                case "L":
                    planType = StairPlanType.LShape;
                    return true;

                default:
                    planType = StairPlanType.UShape;
                    return true;
            }
        }

        private static bool TryGetDouble(
            Editor editor,
            string label,
            double defaultValue,
            double minimum,
            double maximum,
            out double value,
            bool allowZero = false)
        {
            var options = new PromptDoubleOptions(
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "\n{0} <{1:0.##}>: ",
                    label,
                    defaultValue))
            {
                AllowNone = true,
                AllowNegative = false,
                AllowZero = allowZero,
                DefaultValue = defaultValue,
                UseDefaultValue = true
            };

            while (true)
            {
                PromptDoubleResult result = editor.GetDouble(options);
                if (result.Status == PromptStatus.OK)
                {
                    if (result.Value < minimum || result.Value > maximum)
                    {
                        editor.WriteMessage(
                            "\nGia tri phai nam trong khoang {0:0.##} den {1:0.##}.",
                            minimum,
                            maximum);
                        continue;
                    }

                    value = result.Value;
                    return true;
                }

                if (result.Status == PromptStatus.None)
                {
                    value = defaultValue;
                    return true;
                }

                value = default(double);
                return false;
            }
        }

        private static bool TryGetInteger(
            Editor editor,
            string label,
            int defaultValue,
            int minimum,
            int maximum,
            out int value)
        {
            var options = new PromptIntegerOptions(
                string.Format(
                    System.Globalization.CultureInfo.InvariantCulture,
                    "\n{0} <{1}>: ",
                    label,
                    defaultValue))
            {
                AllowNone = true,
                AllowNegative = false,
                AllowZero = false,
                DefaultValue = defaultValue,
                UseDefaultValue = true,
                LowerLimit = minimum,
                UpperLimit = maximum
            };

            PromptIntegerResult result = editor.GetInteger(options);
            if (result.Status == PromptStatus.OK)
            {
                value = result.Value;
                return true;
            }

            if (result.Status == PromptStatus.None)
            {
                value = defaultValue;
                return true;
            }

            value = default(int);
            return false;
        }

        private static void WarnAboutRiserHeight(Editor editor, double riserHeight)
        {
            if (riserHeight < 120.0 || riserHeight > 200.0)
            {
                editor.WriteMessage(
                    "\nCanh bao: chieu cao co bac {0:0.#} nam ngoai khoang tham khao 120-200.",
                    riserHeight);
            }
        }
    }
}
