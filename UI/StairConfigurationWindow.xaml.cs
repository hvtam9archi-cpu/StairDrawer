using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using StairDrawer.Models;

namespace StairDrawer.UI
{
    public partial class StairConfigurationWindow : Window
    {
        private const double MinimumFloorHeight = 500.0;
        private const double MaximumFloorHeight = 10000.0;
        private const double MinimumTreadDepth = 150.0;
        private const double MaximumTreadDepth = 500.0;
        private const double MinimumStairWidth = 600.0;
        private const double MaximumStairWidth = 5000.0;
        private const double MinimumSlabThickness = 80.0;
        private const double MaximumSlabThickness = 500.0;
        private const double MinimumFinishThickness = 1.0;
        private const double MaximumFinishThickness = 100.0;

        private static readonly Brush PreviewPrimaryBrush = CreateBrush(37, 99, 235);
        private static readonly Brush PreviewSecondaryBrush = CreateBrush(232, 234, 237);
        private static readonly Brush PreviewHiddenBrush = CreateBrush(107, 114, 128);
        private static readonly Brush PreviewCutBrush = CreateBrush(239, 68, 68);
        private static readonly Brush PreviewConcreteBrush = CreateBrush(96, 165, 250);
        private static readonly Brush PreviewFinishBrush = CreateBrush(250, 204, 21);
        private static readonly Brush MutedStatusBrush = CreateBrush(156, 163, 175);
        private static readonly Brush WarningStatusBrush = CreateBrush(250, 204, 21);
        private static readonly Brush ErrorStatusBrush = CreateBrush(239, 68, 68);

        private TextBox _invalidTextBox;

        internal StairConfigurationWindow(
            StairDrawingMode initialDrawingMode,
            bool allowDrawingModeSelection)
        {
            InitializeComponent();

            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                return;
            }

            PlanRadioButton.IsChecked = initialDrawingMode == StairDrawingMode.Plan;
            SectionRadioButton.IsChecked = initialDrawingMode == StairDrawingMode.Section;
            DrawingTypePanel.IsEnabled = allowDrawingModeSelection;
            UpdateInterface();
        }

        internal StairDrawingConfiguration Configuration { get; private set; }

        private StairDrawingMode SelectedDrawingMode => SectionRadioButton.IsChecked == true
            ? StairDrawingMode.Section
            : StairDrawingMode.Plan;

        private StairPlanType SelectedStairType
        {
            get
            {
                if (StraightRadioButton.IsChecked == true)
                {
                    return StairPlanType.Straight;
                }

                return LShapeRadioButton.IsChecked == true
                    ? StairPlanType.LShape
                    : StairPlanType.UShape;
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs eventArgs)
        {
            ExecuteUiAction("load configuration window", UpdateInterface);
        }

        private void OnOptionChanged(object sender, RoutedEventArgs eventArgs)
        {
            if (PreviewCanvas == null || PlanFieldsPanel == null || SectionFieldsPanel == null)
            {
                return;
            }

            ExecuteUiAction("update stair preview", UpdateInterface);
        }

        private void OnParameterTextChanged(object sender, TextChangedEventArgs eventArgs)
        {
            if (StatusTextBlock == null || FloorHeightTextBox == null || TotalRisersTextBox == null)
            {
                return;
            }

            ExecuteUiAction("update riser height hint", UpdateRiserHeightHint);
        }

        private void OnDrawClicked(object sender, RoutedEventArgs eventArgs)
        {
            ExecuteUiAction(
                "accept stair configuration",
                () =>
                {
                    if (!TryBuildConfiguration(out StairDrawingConfiguration configuration))
                    {
                        return;
                    }

                    Configuration = configuration;
                    DialogResult = true;
                });
        }

        private void OnCloseClicked(object sender, RoutedEventArgs eventArgs)
        {
            ExecuteUiAction(
                "close configuration window",
                Close);
        }

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs eventArgs)
        {
            if (eventArgs.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            try
            {
                DragMove();
            }
            catch (InvalidOperationException exception)
            {
                Trace.TraceWarning("Stair configuration window could not start dragging: {0}", exception.Message);
            }
        }

        private void UpdateInterface()
        {
            StairDrawingMode drawingMode = SelectedDrawingMode;
            StairPlanType stairType = SelectedStairType;

            PlanFieldsPanel.Visibility = drawingMode == StairDrawingMode.Plan
                ? Visibility.Visible
                : Visibility.Collapsed;
            SectionFieldsPanel.Visibility = drawingMode == StairDrawingMode.Section
                ? Visibility.Visible
                : Visibility.Collapsed;
            LandingDepthRow.Visibility = stairType == StairPlanType.Straight
                ? Visibility.Collapsed
                : Visibility.Visible;
            FlightGapRow.Visibility = stairType == StairPlanType.UShape
                ? Visibility.Visible
                : Visibility.Collapsed;

            DrawPreview(drawingMode, stairType);
            UpdateRiserHeightHint();
        }

        private void DrawPreview(StairDrawingMode drawingMode, StairPlanType stairType)
        {
            PreviewCanvas.Children.Clear();
            if (drawingMode == StairDrawingMode.Plan)
            {
                DrawPlanPreview(stairType);
                PreviewModeBadge.Text = "MẶT BẰNG · " + GetStairTypeLabel(stairType).ToUpperInvariant();
                PreviewTitleTextBlock.Text = "Mặt bằng thang " + GetStairTypeLabel(stairType).ToLowerInvariant();
                PreviewDescriptionTextBlock.Text = GetPlanDescription(stairType);
                return;
            }

            DrawSectionPreview(stairType);
            PreviewModeBadge.Text = "MẶT CẮT · " + GetStairTypeLabel(stairType).ToUpperInvariant();
            PreviewTitleTextBlock.Text = stairType == StairPlanType.Straight
                ? "Mặt cắt thang thẳng"
                : "Mặt cắt triển khai thang " + GetStairTypeLabel(stairType).ToLowerInvariant();
            PreviewDescriptionTextBlock.Text = stairType == StairPlanType.Straight
                ? "Bản thang là một polyline kín; mặt bậc có mũi 20 mm và cổ bậc nằm trên mặt bậc."
                : "Hai vế được triển khai liên tục qua chiếu nghỉ; bản BTCT vẫn là một polyline kín duy nhất.";
        }

        private void DrawPlanPreview(StairPlanType stairType)
        {
            switch (stairType)
            {
                case StairPlanType.Straight:
                    DrawStraightPlanPreview();
                    break;
                case StairPlanType.LShape:
                    DrawLPlanPreview();
                    break;
                default:
                    DrawUPlanPreview();
                    break;
            }
        }

        private void DrawStraightPlanPreview()
        {
            AddRectangleOutline(70, 45, 320, 190, PreviewSecondaryBrush, 2.0);
            for (int stepIndex = 1; stepIndex < 12; stepIndex++)
            {
                double x = 70 + (stepIndex * (320.0 / 12.0));
                AddLine(x, 45, x, 235, stepIndex > 6 ? PreviewHiddenBrush : PreviewSecondaryBrush, 1.4, stepIndex > 6);
            }

            AddPolyline(
                new[]
                {
                    new Point(221, 45),
                    new Point(232, 70),
                    new Point(221, 98),
                    new Point(232, 125),
                    new Point(221, 153),
                    new Point(232, 180),
                    new Point(221, 235)
                },
                PreviewCutBrush,
                3.0);
            AddArrow(new Point(105, 140), new Point(355, 140));
        }

        private void DrawLPlanPreview()
        {
            AddRectangleOutline(45, 155, 300, 90, PreviewSecondaryBrush, 2.0);
            AddRectangleOutline(255, 45, 90, 110, PreviewSecondaryBrush, 2.0);
            for (int stepIndex = 1; stepIndex < 8; stepIndex++)
            {
                double x = 45 + (stepIndex * 26.0);
                AddLine(x, 155, x, 245, PreviewSecondaryBrush, 1.3);
            }

            for (int stepIndex = 1; stepIndex < 5; stepIndex++)
            {
                double y = 45 + (stepIndex * 22.0);
                AddLine(255, y, 345, y, PreviewHiddenBrush, 1.3, true);
            }

            AddLine(232, 155, 232, 245, PreviewCutBrush, 3.0);
            AddPolyline(
                new[] { new Point(80, 205), new Point(300, 205), new Point(300, 80) },
                PreviewPrimaryBrush,
                4.0);
            AddArrowHead(new Point(300, 80), new Vector(0, -1));
        }

        private void DrawUPlanPreview()
        {
            AddRectangleOutline(55, 75, 125, 175, PreviewSecondaryBrush, 2.0);
            AddRectangleOutline(280, 75, 125, 175, PreviewSecondaryBrush, 2.0);
            AddRectangleOutline(55, 30, 350, 45, PreviewSecondaryBrush, 2.0);
            for (int stepIndex = 1; stepIndex < 7; stepIndex++)
            {
                double y = 75 + (stepIndex * 25.0);
                AddLine(55, y, 180, y, PreviewSecondaryBrush, 1.3);
                AddLine(280, y, 405, y, PreviewHiddenBrush, 1.3, true);
            }

            AddLine(55, 150, 180, 150, PreviewCutBrush, 3.0);
            AddPolyline(
                new[]
                {
                    new Point(118, 220),
                    new Point(118, 52),
                    new Point(342, 52),
                    new Point(342, 215)
                },
                PreviewPrimaryBrush,
                4.0);
            AddArrowHead(new Point(342, 215), new Vector(0, 1));
        }

        private void DrawSectionPreview(StairPlanType stairType)
        {
            if (stairType == StairPlanType.Straight)
            {
                IReadOnlyList<Point> stepPoints = CreateStepPoints(65, 225, 12, 25, 12.5);
                AddPolyline(stepPoints, PreviewFinishBrush, 2.2);
                AddPolyline(
                    new[]
                    {
                        new Point(40, 225),
                        new Point(65, 225),
                        new Point(365, 75),
                        new Point(420, 75),
                        new Point(420, 105),
                        new Point(374, 105),
                        new Point(75, 255),
                        new Point(40, 255)
                    },
                    PreviewConcreteBrush,
                    3.0,
                    true,
                    CreateBrush(30, 58, 90));
                AddNosingMarkers(stepPoints);
                return;
            }

            var developedSteps = new List<Point>();
            developedSteps.AddRange(CreateStepPoints(45, 230, 6, 24, 13));
            developedSteps.Add(new Point(245, 152));
            developedSteps.AddRange(CreateStepPoints(245, 152, 6, 24, 13, true));
            AddPolyline(developedSteps, PreviewFinishBrush, 2.2);
            AddPolyline(
                new[]
                {
                    new Point(25, 230),
                    new Point(45, 230),
                    new Point(189, 152),
                    new Point(245, 152),
                    new Point(389, 74),
                    new Point(435, 74),
                    new Point(435, 103),
                    new Point(398, 103),
                    new Point(254, 181),
                    new Point(198, 181),
                    new Point(54, 259),
                    new Point(25, 259)
                },
                PreviewConcreteBrush,
                3.0,
                true,
                CreateBrush(30, 58, 90));
            AddNosingMarkers(developedSteps);

            var label = new TextBlock
            {
                Text = stairType == StairPlanType.LShape ? "TRIỂN KHAI L" : "TRIỂN KHAI U",
                Foreground = PreviewPrimaryBrush,
                FontWeight = FontWeights.SemiBold,
                FontSize = 11
            };
            Canvas.SetLeft(label, 180);
            Canvas.SetTop(label, 20);
            PreviewCanvas.Children.Add(label);
        }

        private static IReadOnlyList<Point> CreateStepPoints(
            double startX,
            double startY,
            int stepCount,
            double stepWidth,
            double stepHeight,
            bool omitFirstPoint = false)
        {
            var points = new List<Point>();
            double x = startX;
            double y = startY;
            if (!omitFirstPoint)
            {
                points.Add(new Point(x, y));
            }

            for (int stepIndex = 0; stepIndex < stepCount; stepIndex++)
            {
                y -= stepHeight;
                points.Add(new Point(x, y));
                x += stepWidth;
                points.Add(new Point(x, y));
            }

            return points;
        }

        private void AddNosingMarkers(IReadOnlyList<Point> stepPoints)
        {
            for (int pointIndex = 1; pointIndex < stepPoints.Count; pointIndex += 2)
            {
                Point point = stepPoints[pointIndex];
                AddLine(point.X - 5, point.Y - 3, point.X, point.Y, PreviewFinishBrush, 1.2);
            }
        }

        private void AddArrow(Point start, Point end)
        {
            AddLine(start.X, start.Y, end.X, end.Y, PreviewPrimaryBrush, 4.0);
            AddArrowHead(end, end - start);
        }

        private void AddArrowHead(Point tip, Vector direction)
        {
            if (direction.Length <= 1e-9)
            {
                return;
            }

            direction.Normalize();
            Vector perpendicular = new Vector(-direction.Y, direction.X);
            Point basePoint = tip - (direction * 18.0);
            AddPolyline(
                new[]
                {
                    basePoint + (perpendicular * 9.0),
                    tip,
                    basePoint - (perpendicular * 9.0)
                },
                PreviewPrimaryBrush,
                4.0);
        }

        private void AddRectangleOutline(
            double x,
            double y,
            double width,
            double height,
            Brush stroke,
            double thickness)
        {
            var rectangle = new Rectangle
            {
                Width = width,
                Height = height,
                Stroke = stroke,
                StrokeThickness = thickness,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(rectangle, x);
            Canvas.SetTop(rectangle, y);
            PreviewCanvas.Children.Add(rectangle);
        }

        private void AddLine(
            double x1,
            double y1,
            double x2,
            double y2,
            Brush stroke,
            double thickness,
            bool dashed = false)
        {
            var line = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            if (dashed)
            {
                line.StrokeDashArray = new DoubleCollection { 5.0, 4.0 };
            }

            PreviewCanvas.Children.Add(line);
        }

        private void AddPolyline(
            IEnumerable<Point> points,
            Brush stroke,
            double thickness,
            bool closed = false,
            Brush fill = null)
        {
            var pointCollection = new PointCollection(points);
            Shape shape = closed
                ? (Shape)new Polygon { Points = pointCollection }
                : new Polyline { Points = pointCollection };
            shape.Stroke = stroke;
            shape.StrokeThickness = thickness;
            shape.StrokeLineJoin = PenLineJoin.Round;
            shape.StrokeStartLineCap = PenLineCap.Round;
            shape.StrokeEndLineCap = PenLineCap.Round;
            shape.Fill = fill ?? Brushes.Transparent;
            PreviewCanvas.Children.Add(shape);
        }

        private bool TryBuildConfiguration(out StairDrawingConfiguration configuration)
        {
            configuration = null;
            ResetValidationState();

            StairPlanType stairType = SelectedStairType;
            int minimumRisers = stairType == StairPlanType.Straight ? 2 : 4;
            if (!TryReadDouble(FloorHeightTextBox, "Chiều cao tầng", MinimumFloorHeight, MaximumFloorHeight, out double floorHeight)
                || !TryReadInteger(TotalRisersTextBox, "Tổng số cổ bậc", minimumRisers, 60, out int totalRisers)
                || !TryReadDouble(TreadDepthTextBox, "Chiều rộng mặt bậc", MinimumTreadDepth, MaximumTreadDepth, out double treadDepth))
            {
                return false;
            }

            if (SelectedDrawingMode == StairDrawingMode.Plan)
            {
                if (!TryReadDouble(StairWidthTextBox, "Chiều rộng vế thang", MinimumStairWidth, MaximumStairWidth, out double stairWidth)
                    || !TryReadDouble(PlanCutHeightTextBox, "Cao độ mặt phẳng cắt", 100.0, floorHeight - 1.0, out double planCutHeight))
                {
                    return false;
                }

                double landingDepth = stairWidth;
                if (stairType != StairPlanType.Straight
                    && (!TryReadDouble(LandingDepthTextBox, "Chiều sâu chiếu nghỉ", MinimumStairWidth, MaximumStairWidth, out landingDepth)
                        || !ValidateLandingDepth(landingDepth, stairWidth)))
                {
                    return false;
                }

                double flightGap = 0.0;
                if (stairType == StairPlanType.UShape
                    && !TryReadDouble(FlightGapTextBox, "Khe giữa hai vế", 0.0, 1000.0, out flightGap))
                {
                    return false;
                }

                configuration = StairDrawingConfiguration.CreatePlan(new StairPlanParameters(
                    stairType,
                    floorHeight,
                    totalRisers,
                    treadDepth,
                    stairWidth,
                    landingDepth,
                    flightGap,
                    planCutHeight));
            }
            else
            {
                if (!TryReadDouble(LandingLengthTextBox, "Chiều dài chiếu nghỉ", MinimumStairWidth, MaximumStairWidth, out double landingLength)
                    || !TryReadDouble(SlabThicknessTextBox, "Chiều dày bản BTCT", MinimumSlabThickness, MaximumSlabThickness, out double slabThickness)
                    || !TryReadDouble(FinishThicknessTextBox, "Chiều dày hoàn thiện", MinimumFinishThickness, MaximumFinishThickness, out double finishThickness))
                {
                    return false;
                }

                configuration = StairDrawingConfiguration.CreateSection(new StairSectionParameters(
                    stairType,
                    floorHeight,
                    totalRisers,
                    treadDepth,
                    landingLength,
                    slabThickness,
                    finishThickness));
            }

            double riserHeight = floorHeight / totalRisers;
            if (riserHeight < 120.0 || riserHeight > 200.0)
            {
                Trace.TraceWarning(
                    "Stair configuration accepted with riser height {0:0.#} outside reference range 120-200.",
                    riserHeight);
            }

            return true;
        }

        private bool ValidateLandingDepth(double landingDepth, double stairWidth)
        {
            if (landingDepth >= stairWidth)
            {
                return true;
            }

            ShowValidationError(
                LandingDepthTextBox,
                string.Format(
                    CultureInfo.CurrentCulture,
                    "Chiều sâu chiếu nghỉ phải ≥ chiều rộng vế thang ({0:0.##} mm).",
                    stairWidth));
            return false;
        }

        private bool TryReadDouble(
            TextBox textBox,
            string label,
            double minimum,
            double maximum,
            out double value)
        {
            string text = textBox.Text.Trim();
            bool parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
            if (!parsed || !NumericValidation.IsFinite(value))
            {
                ShowValidationError(textBox, label + " phải là một số hợp lệ.");
                return false;
            }

            if (value < minimum || value > maximum)
            {
                ShowValidationError(
                    textBox,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} phải nằm trong khoảng {1:0.##}–{2:0.##} mm.",
                        label,
                        minimum,
                        maximum));
                return false;
            }

            return true;
        }

        private bool TryReadInteger(
            TextBox textBox,
            string label,
            int minimum,
            int maximum,
            out int value)
        {
            if (!int.TryParse(textBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out value))
            {
                ShowValidationError(textBox, label + " phải là một số nguyên.");
                return false;
            }

            if (value < minimum || value > maximum)
            {
                ShowValidationError(
                    textBox,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "{0} phải nằm trong khoảng {1}–{2}.",
                        label,
                        minimum,
                        maximum));
                return false;
            }

            return true;
        }

        private void UpdateRiserHeightHint()
        {
            if (!TryParseDouble(FloorHeightTextBox.Text, out double floorHeight)
                || !int.TryParse(TotalRisersTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out int totalRisers)
                || totalRisers <= 0)
            {
                StatusTextBlock.Foreground = MutedStatusBrush;
                StatusTextBlock.Text = "Nhập thông số, xem preview rồi bấm Vẽ.";
                return;
            }

            double riserHeight = floorHeight / totalRisers;
            StatusTextBlock.Text = string.Format(
                CultureInfo.CurrentCulture,
                "Chiều cao cổ bậc dự kiến: {0:0.#} mm{1}",
                riserHeight,
                riserHeight < 120.0 || riserHeight > 200.0
                    ? " · ngoài khoảng tham khảo 120–200 mm"
                    : string.Empty);
            StatusTextBlock.Foreground = riserHeight < 120.0 || riserHeight > 200.0
                ? WarningStatusBrush
                : MutedStatusBrush;
        }

        private static bool TryParseDouble(string text, out double value)
        {
            string trimmedText = text == null ? string.Empty : text.Trim();
            return (double.TryParse(trimmedText, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                    || double.TryParse(trimmedText, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                && NumericValidation.IsFinite(value);
        }

        private void ShowValidationError(TextBox textBox, string message)
        {
            _invalidTextBox = textBox;
            textBox.BorderBrush = ErrorStatusBrush;
            textBox.BorderThickness = new Thickness(2);
            textBox.Focus();
            textBox.SelectAll();
            StatusTextBlock.Foreground = ErrorStatusBrush;
            StatusTextBlock.Text = message;
        }

        private void ResetValidationState()
        {
            if (_invalidTextBox != null)
            {
                _invalidTextBox.ClearValue(BorderBrushProperty);
                _invalidTextBox.ClearValue(BorderThicknessProperty);
                _invalidTextBox = null;
            }

            StatusTextBlock.Foreground = MutedStatusBrush;
            StatusTextBlock.Text = "Đang kiểm tra thông số...";
        }

        private static string GetStairTypeLabel(StairPlanType stairType)
        {
            switch (stairType)
            {
                case StairPlanType.Straight:
                    return "Thẳng";
                case StairPlanType.LShape:
                    return "Chữ L";
                default:
                    return "Chữ U";
            }
        }

        private static string GetPlanDescription(StairPlanType stairType)
        {
            switch (stairType)
            {
                case StairPlanType.Straight:
                    return "Một vế thang liên tục; nét cắt, số bậc và đường chỉ hướng được tạo theo cao độ mặt phẳng cắt.";
                case StairPlanType.LShape:
                    return "Hai vế thang vuông góc, nối bằng chiếu nghỉ; đường chỉ hướng chạy liên tục qua cả hai vế.";
                default:
                    return "Hai vế thang song song, nối qua chiếu nghỉ; có thể điều chỉnh khe giữa hai vế.";
            }
        }

        private void ExecuteUiAction(string context, Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Trace.TraceError("Stair configuration UI failed to {0}: {1}", context, exception);
                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Foreground = ErrorStatusBrush;
                    StatusTextBlock.Text = "Không thể cập nhật giao diện. Hãy đóng cửa sổ và thử lại.";
                }
            }
        }

        private static Brush CreateBrush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}
