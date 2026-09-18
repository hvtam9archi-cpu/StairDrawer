using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using StairDrawer.Geometry;
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
        private const double MinimumRailingHeight = 600.0;
        private const double MaximumRailingHeight = 1500.0;

        private static readonly Brush PreviewPrimaryBrush = CreateBrush(37, 99, 235);
        private static readonly Brush PreviewSecondaryBrush = CreateBrush(232, 234, 237);
        private static readonly Brush PreviewHiddenBrush = CreateBrush(107, 114, 128);
        private static readonly Brush PreviewCutBrush = CreateBrush(239, 68, 68);
        private static readonly Brush PreviewConcreteBrush = CreateBrush(96, 165, 250);
        private static readonly Brush PreviewConcreteFillBrush = CreateBrush(30, 58, 90);
        private static readonly Brush PreviewWipeoutBrush = CreateBrush(26, 28, 33);
        private static readonly Brush PreviewBrickBrush = CreateBrush(249, 115, 22);
        private static readonly Brush PreviewFinishBrush = CreateBrush(250, 204, 21);
        private static readonly Brush PreviewRailingBrush = CreateBrush(34, 197, 94);
        private static readonly Brush PreviewGlassBrush = CreateBrush(34, 211, 238);
        private static readonly Brush MutedStatusBrush = CreateBrush(156, 163, 175);
        private static readonly Brush WarningStatusBrush = CreateBrush(250, 204, 21);
        private static readonly Brush ErrorStatusBrush = CreateBrush(239, 68, 68);

        private readonly StairDrawingMode _initialDrawingMode;
        private readonly bool _allowDrawingModeSelection;
        private TextBox _invalidTextBox;
        private bool _isRuntimeInitialized;
        private bool _isSynchronizingFields;
        private StairPlanType? _lastStairType;

        internal StairConfigurationWindow(
            StairDrawingMode initialDrawingMode,
            bool allowDrawingModeSelection)
        {
            _initialDrawingMode = initialDrawingMode;
            _allowDrawingModeSelection = allowDrawingModeSelection;
            InitializeComponent();
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

        private StairRailingType SelectedRailingType => GlassRailingRadioButton.IsChecked == true
            ? StairRailingType.Glass
            : StairRailingType.Iron;

        private void OnWindowLoaded(object sender, RoutedEventArgs eventArgs)
        {
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                return;
            }

            ExecuteUiAction(
                "load configuration window",
                () =>
                {
                    PlanRadioButton.IsChecked = _initialDrawingMode == StairDrawingMode.Plan;
                    SectionRadioButton.IsChecked = _initialDrawingMode == StairDrawingMode.Section;
                    DrawingTypePanel.IsEnabled = _allowDrawingModeSelection;
                    _isRuntimeInitialized = true;
                    InitializeRiserDistribution();
                    UpdateInterface();
                });
        }

        private void OnOptionChanged(object sender, RoutedEventArgs eventArgs)
        {
            if (!_isRuntimeInitialized
                || PreviewCanvas == null
                || PlanFieldsPanel == null
                || SectionFieldsPanel == null)
            {
                return;
            }

            ExecuteUiAction(
                "update stair preview",
                () =>
                {
                    if (sender == StraightRadioButton
                        || sender == LShapeRadioButton
                        || sender == UShapeRadioButton)
                    {
                        InitializeRiserDistribution();
                    }

                    UpdateInterface();
                });
        }

        private void OnParameterTextChanged(object sender, TextChangedEventArgs eventArgs)
        {
            if (!_isRuntimeInitialized
                || _isSynchronizingFields
                || StatusTextBlock == null
                || FloorHeightTextBox == null
                || TotalRisersTextBox == null)
            {
                return;
            }

            ExecuteUiAction(
                "update stair preview",
                () =>
                {
                    if (sender == LandingDepthTextBox || sender == LandingLengthTextBox)
                    {
                        SynchronizeLandingPreviewFromForm();
                    }

                    DrawPreview(SelectedDrawingMode, SelectedStairType);
                    UpdateRiserHeightHint();
                });
        }

        private void OnLandingPreviewTextChanged(object sender, TextChangedEventArgs eventArgs)
        {
            if (!_isRuntimeInitialized
                || _isSynchronizingFields
                || LandingPreviewTextBox == null)
            {
                return;
            }

            ExecuteUiAction(
                "update landing from stair preview",
                () =>
                {
                    TextBox landingTextBox = SelectedDrawingMode == StairDrawingMode.Plan
                        ? LandingDepthTextBox
                        : LandingLengthTextBox;
                    _isSynchronizingFields = true;
                    try
                    {
                        landingTextBox.Text = LandingPreviewTextBox.Text;
                    }
                    finally
                    {
                        _isSynchronizingFields = false;
                    }

                    DrawPreview(SelectedDrawingMode, SelectedStairType);
                    UpdateRiserHeightHint();
                });
        }

        private void OnFlightRisersTextChanged(object sender, TextChangedEventArgs eventArgs)
        {
            if (!_isRuntimeInitialized || _isSynchronizingFields)
            {
                return;
            }

            ExecuteUiAction(
                "update flight riser count",
                () =>
                {
                    SynchronizeTotalRisers();
                    DrawPreview(SelectedDrawingMode, SelectedStairType);
                    UpdateRiserHeightHint();
                });
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
            SecondFlightRisersRow.Visibility = stairType == StairPlanType.Straight
                ? Visibility.Collapsed
                : Visibility.Visible;
            RailingOptionsPanel.IsEnabled = drawingMode == StairDrawingMode.Section
                && DrawRailingCheckBox.IsChecked == true;

            TotalRisersTextBox.IsReadOnly = true;
            TotalRisersLabel.Text = "Tổng số cổ bậc (tự tính)";

            SynchronizeLandingPreviewFromForm();
            DrawPreview(drawingMode, stairType);
            UpdateRiserHeightHint();
        }

        private void SynchronizeLandingPreviewFromForm()
        {
            if (LandingPreviewTextBox == null)
            {
                return;
            }

            TextBox landingTextBox = SelectedDrawingMode == StairDrawingMode.Plan
                ? LandingDepthTextBox
                : LandingLengthTextBox;
            _isSynchronizingFields = true;
            try
            {
                LandingPreviewTextBox.Text = landingTextBox.Text;
            }
            finally
            {
                _isSynchronizingFields = false;
            }
        }

        private void InitializeRiserDistribution()
        {
            StairPlanType stairType = SelectedStairType;
            if (_lastStairType.HasValue
                && _lastStairType.Value != StairPlanType.Straight
                && stairType != StairPlanType.Straight)
            {
                _lastStairType = stairType;
                SynchronizeTotalRisers();
                return;
            }

            int totalRisers = ReadPreviewInteger(TotalRisersTextBox, 20, 2, 60);
            _isSynchronizingFields = true;
            try
            {
                if (stairType == StairPlanType.Straight)
                {
                    FirstFlightRisersTextBox.Text = totalRisers.ToString(CultureInfo.CurrentCulture);
                    SecondFlightRisersTextBox.Text = "0";
                }
                else
                {
                    int firstFlightRisers = totalRisers / 2;
                    FirstFlightRisersTextBox.Text = firstFlightRisers.ToString(CultureInfo.CurrentCulture);
                    SecondFlightRisersTextBox.Text = (totalRisers - firstFlightRisers).ToString(CultureInfo.CurrentCulture);
                }
            }
            finally
            {
                _isSynchronizingFields = false;
            }

            _lastStairType = stairType;
            SynchronizeTotalRisers();
        }

        private void SynchronizeTotalRisers()
        {
            if (!int.TryParse(
                    FirstFlightRisersTextBox.Text.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.CurrentCulture,
                    out int firstFlightRisers))
            {
                return;
            }

            int secondFlightRisers = 0;
            if (SelectedStairType != StairPlanType.Straight
                && !int.TryParse(
                    SecondFlightRisersTextBox.Text.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.CurrentCulture,
                    out secondFlightRisers))
            {
                return;
            }

            _isSynchronizingFields = true;
            try
            {
                TotalRisersTextBox.Text = (firstFlightRisers + secondFlightRisers)
                    .ToString(CultureInfo.CurrentCulture);
            }
            finally
            {
                _isSynchronizingFields = false;
            }
        }

        private void DrawPreview(StairDrawingMode drawingMode, StairPlanType stairType)
        {
            PreviewGeometryCanvas.Children.Clear();
            PreviewValues values = ReadPreviewValues(drawingMode, stairType);
            DrawingModel drawing;
            if (drawingMode == StairDrawingMode.Plan)
            {
                drawing = StairPlanGeometry.Create(new StairPlanParameters(
                    stairType,
                    values.FloorHeight,
                    values.TotalRisers,
                    values.FirstFlightRisers,
                    values.TreadDepth,
                    values.StairWidth,
                    values.Landing,
                    values.FlightGap,
                    values.PlanCutHeight));
            }
            else
            {
                drawing = StairSectionGeometry.Create(new StairSectionParameters(
                    stairType,
                    values.FloorHeight,
                    values.TotalRisers,
                    values.FirstFlightRisers,
                    values.TreadDepth,
                    values.Landing,
                    values.SlabThickness,
                    values.FinishThickness,
                    values.DrawRailing,
                    values.RailingType,
                    values.RailingHeight));
            }

            PreviewTransform transform = CreatePreviewTransform(drawing);
            RenderDrawingModel(drawing, transform);
            PositionDimensionEditors(drawingMode, stairType, values, transform);

            if (drawingMode == StairDrawingMode.Plan)
            {
                PreviewModeBadge.Text = "MẶT BẰNG · " + GetStairTypeLabel(stairType).ToUpperInvariant();
                PreviewTitleTextBlock.Text = "Mặt bằng thang " + GetStairTypeLabel(stairType).ToLowerInvariant();
                PreviewDescriptionTextBlock.Text = GetPlanDescription(stairType)
                    + " Ký hiệu A–F tương ứng với bảng thông số bên trái.";
                return;
            }

            PreviewModeBadge.Text = "MẶT CẮT · " + GetStairTypeLabel(stairType).ToUpperInvariant();
            PreviewTitleTextBlock.Text = stairType == StairPlanType.Straight
                ? "Mặt cắt thang thẳng"
                : "Mặt cắt thang " + GetStairTypeLabel(stairType).ToLowerInvariant();
            string sectionDescription = stairType == StairPlanType.Straight
                ? "Vế thang được cắt qua, thể hiện đầy đủ bản BTCT, bậc xây và lớp hoàn thiện. Ký hiệu dùng bảng bên trái."
                : "Vế 1 là phần bị cắt; vế 2 phía sau thể hiện bằng nét thấy. Ký hiệu dùng bảng thông số bên trái.";
            PreviewDescriptionTextBlock.Text = values.DrawRailing
                ? sectionDescription + " Lan can "
                    + (values.RailingType == StairRailingType.Glass ? "kính" : "sắt")
                    + " dùng chiều cao G."
                : sectionDescription;
        }

        private PreviewValues ReadPreviewValues(StairDrawingMode drawingMode, StairPlanType stairType)
        {
            int firstFlightRisers = ReadPreviewInteger(
                FirstFlightRisersTextBox,
                stairType == StairPlanType.Straight ? 20 : 10,
                2,
                stairType == StairPlanType.Straight ? 60 : 58);
            int secondFlightRisers = stairType == StairPlanType.Straight
                ? 0
                : ReadPreviewInteger(SecondFlightRisersTextBox, 10, 2, 58);

            double floorHeight = ReadPreviewDouble(FloorHeightTextBox, 3300.0, MinimumFloorHeight, MaximumFloorHeight);
            double stairWidth = ReadPreviewDouble(StairWidthTextBox, 1000.0, MinimumStairWidth, MaximumStairWidth);
            TextBox landingTextBox = drawingMode == StairDrawingMode.Plan
                ? LandingDepthTextBox
                : LandingLengthTextBox;
            double landing = ReadPreviewDouble(landingTextBox, 1000.0, MinimumStairWidth, MaximumStairWidth);
            if (drawingMode == StairDrawingMode.Plan && stairType != StairPlanType.Straight)
            {
                landing = Math.Max(landing, stairWidth);
            }

            return new PreviewValues(
                floorHeight,
                firstFlightRisers,
                secondFlightRisers,
                ReadPreviewDouble(TreadDepthTextBox, 270.0, MinimumTreadDepth, MaximumTreadDepth),
                stairWidth,
                landing,
                ReadPreviewDouble(FlightGapTextBox, 100.0, 0.0, 1000.0),
                ReadPreviewDouble(PlanCutHeightTextBox, 1200.0, 1.0, floorHeight - 1.0),
                ReadPreviewDouble(SlabThicknessTextBox, 120.0, MinimumSlabThickness, MaximumSlabThickness),
                ReadPreviewDouble(FinishThicknessTextBox, 20.0, MinimumFinishThickness, MaximumFinishThickness),
                drawingMode == StairDrawingMode.Section && DrawRailingCheckBox.IsChecked == true,
                SelectedRailingType,
                ReadPreviewDouble(RailingHeightTextBox, 900.0, MinimumRailingHeight, MaximumRailingHeight));
        }

        private static int ReadPreviewInteger(TextBox textBox, int fallback, int minimum, int maximum)
        {
            if (!int.TryParse(textBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out int value))
            {
                return fallback;
            }

            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static double ReadPreviewDouble(TextBox textBox, double fallback, double minimum, double maximum)
        {
            if (!TryParseDouble(textBox.Text, out double value))
            {
                return fallback;
            }

            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static PreviewTransform CreatePreviewTransform(DrawingModel drawing)
        {
            double minimumX = double.PositiveInfinity;
            double minimumY = double.PositiveInfinity;
            double maximumX = double.NegativeInfinity;
            double maximumY = double.NegativeInfinity;

            foreach (LinePrimitive line in drawing.Lines)
            {
                ExpandBounds(line.Start, ref minimumX, ref minimumY, ref maximumX, ref maximumY);
                ExpandBounds(line.End, ref minimumX, ref minimumY, ref maximumX, ref maximumY);
            }

            foreach (PolylinePrimitive polyline in drawing.Polylines)
            {
                foreach (Point2 vertex in polyline.Vertices)
                {
                    ExpandBounds(vertex, ref minimumX, ref minimumY, ref maximumX, ref maximumY);
                }
            }

            foreach (WipeoutPrimitive wipeout in drawing.Wipeouts)
            {
                foreach (Point2 vertex in wipeout.Boundary)
                {
                    ExpandBounds(vertex, ref minimumX, ref minimumY, ref maximumX, ref maximumY);
                }
            }

            foreach (CirclePrimitive circle in drawing.Circles)
            {
                ExpandBounds(new Point2(circle.Center.X - circle.Radius, circle.Center.Y - circle.Radius), ref minimumX, ref minimumY, ref maximumX, ref maximumY);
                ExpandBounds(new Point2(circle.Center.X + circle.Radius, circle.Center.Y + circle.Radius), ref minimumX, ref minimumY, ref maximumX, ref maximumY);
            }

            foreach (TextPrimitive text in drawing.Texts)
            {
                ExpandBounds(text.Position, ref minimumX, ref minimumY, ref maximumX, ref maximumY);
            }

            if (double.IsInfinity(minimumX))
            {
                minimumX = 0.0;
                minimumY = 0.0;
                maximumX = 1.0;
                maximumY = 1.0;
            }

            return new PreviewTransform(minimumX, minimumY, maximumX, maximumY);
        }

        private static void ExpandBounds(
            Point2 point,
            ref double minimumX,
            ref double minimumY,
            ref double maximumX,
            ref double maximumY)
        {
            minimumX = Math.Min(minimumX, point.X);
            minimumY = Math.Min(minimumY, point.Y);
            maximumX = Math.Max(maximumX, point.X);
            maximumY = Math.Max(maximumY, point.Y);
        }

        private void RenderDrawingModel(DrawingModel drawing, PreviewTransform transform)
        {
            foreach (LinePrimitive line in drawing.Lines)
            {
                Point start = transform.ToScreen(line.Start);
                Point end = transform.ToScreen(line.End);
                AddLine(
                    start.X,
                    start.Y,
                    end.X,
                    end.Y,
                    GetPreviewBrush(line.LayerName),
                    GetPreviewStrokeThickness(line.LayerName),
                    line.LayerName == StairLayers.PlanHidden);
            }

            RenderPolylines(drawing, transform, false, false);
            RenderPolylines(drawing, transform, true, false);
            RenderWipeouts(drawing, transform);
            RenderPolylines(drawing, transform, true, true);

            foreach (CirclePrimitive circle in drawing.Circles)
            {
                Point center = transform.ToScreen(circle.Center);
                double radius = Math.Max(2.0, circle.Radius * transform.Scale);
                var ellipse = new Ellipse
                {
                    Width = radius * 2.0,
                    Height = radius * 2.0,
                    Stroke = GetPreviewBrush(circle.LayerName),
                    StrokeThickness = GetPreviewStrokeThickness(circle.LayerName),
                    Fill = Brushes.Transparent
                };
                Canvas.SetLeft(ellipse, center.X - radius);
                Canvas.SetTop(ellipse, center.Y - radius);
                PreviewGeometryCanvas.Children.Add(ellipse);
            }

            foreach (TextPrimitive text in drawing.Texts)
            {
                Point position = transform.ToScreen(text.Position);
                var label = new TextBlock
                {
                    Text = text.Text,
                    Foreground = GetPreviewBrush(text.LayerName),
                    FontSize = Math.Max(7.0, Math.Min(11.0, text.Height * transform.Scale)),
                    RenderTransform = new RotateTransform(-text.Rotation * 180.0 / Math.PI),
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(label, position.X - (label.DesiredSize.Width * 0.5));
                Canvas.SetTop(label, position.Y - (label.DesiredSize.Height * 0.5));
                PreviewGeometryCanvas.Children.Add(label);
            }
        }

        private void RenderPolylines(
            DrawingModel drawing,
            PreviewTransform transform,
            bool drawOnTop,
            bool occludesBackground)
        {
            foreach (PolylinePrimitive polyline in drawing.Polylines)
            {
                if (polyline.DrawOnTop != drawOnTop
                    || (drawOnTop && polyline.OccludesBackground != occludesBackground))
                {
                    continue;
                }

                var points = new List<Point>();
                foreach (Point2 vertex in polyline.Vertices)
                {
                    points.Add(transform.ToScreen(vertex));
                }

                AddPolyline(
                    points,
                    GetPreviewBrush(polyline.LayerName),
                    polyline.GlobalWidth > 0.0
                        ? Math.Max(2.0, polyline.GlobalWidth * transform.Scale)
                        : GetPreviewStrokeThickness(polyline.LayerName),
                    polyline.Closed,
                    GetPreviewFill(polyline.LayerName),
                    polyline.LayerName == StairLayers.PlanHidden);
            }
        }

        private void RenderWipeouts(DrawingModel drawing, PreviewTransform transform)
        {
            foreach (WipeoutPrimitive wipeout in drawing.Wipeouts)
            {
                var points = new List<Point>();
                foreach (Point2 vertex in wipeout.Boundary)
                {
                    points.Add(transform.ToScreen(vertex));
                }

                AddPolyline(
                    points,
                    Brushes.Transparent,
                    0.0,
                    true,
                    PreviewWipeoutBrush);
            }
        }

        private void PositionDimensionEditors(
            StairDrawingMode drawingMode,
            StairPlanType stairType,
            PreviewValues values,
            PreviewTransform transform)
        {
            bool hasSecondFlight = stairType != StairPlanType.Straight;
            FirstFlightRisersDimensionBorder.Visibility = Visibility.Visible;
            SecondFlightRisersDimensionBorder.Visibility = hasSecondFlight ? Visibility.Visible : Visibility.Collapsed;
            TreadDepthDimensionBorder.Visibility = Visibility.Visible;
            StairWidthDimensionBorder.Visibility = drawingMode == StairDrawingMode.Plan ? Visibility.Visible : Visibility.Collapsed;
            LandingDimensionBorder.Visibility = drawingMode == StairDrawingMode.Section || hasSecondFlight
                ? Visibility.Visible
                : Visibility.Collapsed;
            FlightGapDimensionBorder.Visibility = drawingMode == StairDrawingMode.Plan && stairType == StairPlanType.UShape
                ? Visibility.Visible
                : Visibility.Collapsed;
            RailingHeightDimensionBorder.Visibility = drawingMode == StairDrawingMode.Section && values.DrawRailing
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (drawingMode == StairDrawingMode.Section)
            {
                PositionSectionDimensionEditors(stairType, values, transform);
                return;
            }

            PositionPlanDimensionEditors(stairType, values, transform);
        }

        private void PositionPlanDimensionEditors(
            StairPlanType stairType,
            PreviewValues values,
            PreviewTransform transform)
        {
            double firstRun = GetPreviewFlightRun(values.FirstFlightRisers, values.TreadDepth);
            double secondRun = stairType == StairPlanType.Straight
                ? 0.0
                : GetPreviewFlightRun(values.SecondFlightRisers, values.TreadDepth);
            double firstStartX = stairType == StairPlanType.UShape
                ? Math.Max(firstRun, secondRun) - firstRun
                : 0.0;
            PlaceDimensionEditor(
                FirstFlightRisersDimensionBorder,
                new Point2(firstStartX, 0.0),
                new Point2(firstStartX + firstRun, 0.0),
                new Vector(0.0, 22.0),
                transform);
            PlaceDimensionEditor(
                TreadDepthDimensionBorder,
                new Point2(firstStartX, values.StairWidth),
                new Point2(firstStartX + values.TreadDepth, values.StairWidth),
                new Vector(0.0, -18.0),
                transform);
            PlaceDimensionEditor(
                StairWidthDimensionBorder,
                new Point2(firstStartX, 0.0),
                new Point2(firstStartX, values.StairWidth),
                new Vector(-22.0, 0.0),
                transform);

            if (stairType == StairPlanType.Straight)
            {
                return;
            }

            if (stairType == StairPlanType.LShape)
            {
                double secondStartX = firstRun + ((values.Landing - values.StairWidth) * 0.5);
                PlaceDimensionEditor(
                    SecondFlightRisersDimensionBorder,
                    new Point2(secondStartX, values.StairWidth),
                    new Point2(secondStartX, values.StairWidth + secondRun),
                    new Vector(42.0, 0.0),
                    transform);
                PlaceDimensionEditor(
                    LandingDimensionBorder,
                    new Point2(firstRun, values.StairWidth),
                    new Point2(firstRun + values.Landing, values.StairWidth),
                    new Vector(0.0, -24.0),
                    transform);
                return;
            }

            double maximumRun = Math.Max(firstRun, secondRun);
            double totalWidth = (values.StairWidth * 2.0) + values.FlightGap;
            PlaceDimensionEditor(
                SecondFlightRisersDimensionBorder,
                new Point2(maximumRun, totalWidth),
                new Point2(maximumRun - secondRun, totalWidth),
                new Vector(0.0, -22.0),
                transform);
            PlaceDimensionEditor(
                LandingDimensionBorder,
                new Point2(maximumRun, 0.0),
                new Point2(maximumRun + values.Landing, 0.0),
                new Vector(0.0, 24.0),
                transform);
            PlaceDimensionEditor(
                FlightGapDimensionBorder,
                new Point2(maximumRun * 0.55, values.StairWidth),
                new Point2(maximumRun * 0.55, values.StairWidth + Math.Max(values.FlightGap, 1.0)),
                new Vector(0.0, 0.0),
                transform);
        }

        private void PositionSectionDimensionEditors(
            StairPlanType stairType,
            PreviewValues values,
            PreviewTransform transform)
        {
            double firstRun = GetPreviewFlightRun(values.FirstFlightRisers, values.TreadDepth);
            PlaceDimensionEditor(
                FirstFlightRisersDimensionBorder,
                new Point2(0.0, 0.0),
                new Point2(firstRun, 0.0),
                new Vector(0.0, 22.0),
                transform);
            PlaceDimensionEditor(
                TreadDepthDimensionBorder,
                new Point2(0.0, 0.0),
                new Point2(values.TreadDepth, 0.0),
                new Vector(0.0, -22.0),
                transform);

            if (values.DrawRailing)
            {
                double riserHeight = values.FloorHeight / values.TotalRisers;
                double railingBaseElevation = riserHeight + values.FinishThickness;
                PlaceDimensionEditor(
                    RailingHeightDimensionBorder,
                    new Point2(0.0, railingBaseElevation),
                    new Point2(0.0, railingBaseElevation + values.RailingHeight),
                    new Vector(-34.0, 0.0),
                    transform);
            }

            if (stairType == StairPlanType.Straight)
            {
                PlaceDimensionEditor(
                    LandingDimensionBorder,
                    new Point2(firstRun, values.FloorHeight),
                    new Point2(firstRun + values.Landing, values.FloorHeight),
                    new Vector(0.0, -22.0),
                    transform);
                return;
            }

            double firstLandingElevation = values.FirstFlightRisers * (values.FloorHeight / values.TotalRisers);
            double secondStartX = firstRun;
            double secondRun = GetPreviewFlightRun(values.SecondFlightRisers, values.TreadDepth);
            double secondEndX = stairType == StairPlanType.UShape
                ? secondStartX - secondRun
                : secondStartX + secondRun;
            PlaceDimensionEditor(
                SecondFlightRisersDimensionBorder,
                new Point2(secondStartX, firstLandingElevation),
                new Point2(secondEndX, values.FloorHeight),
                new Vector(0.0, -24.0),
                transform);
            PlaceDimensionEditor(
                LandingDimensionBorder,
                new Point2(firstRun, firstLandingElevation),
                new Point2(firstRun + values.Landing, firstLandingElevation),
                new Vector(0.0, 24.0),
                transform);
        }

        private void PlaceDimensionEditor(
            FrameworkElement editor,
            Point2 firstWorldPoint,
            Point2 secondWorldPoint,
            Vector screenOffset,
            PreviewTransform transform)
        {
            Point firstPoint = transform.ToScreen(firstWorldPoint);
            Point secondPoint = transform.ToScreen(secondWorldPoint);
            Point firstDimensionPoint = firstPoint + screenOffset;
            Point secondDimensionPoint = secondPoint + screenOffset;
            AddLine(firstPoint.X, firstPoint.Y, firstDimensionPoint.X, firstDimensionPoint.Y, PreviewHiddenBrush, 1.0);
            AddLine(secondPoint.X, secondPoint.Y, secondDimensionPoint.X, secondDimensionPoint.Y, PreviewHiddenBrush, 1.0);
            AddLine(firstDimensionPoint.X, firstDimensionPoint.Y, secondDimensionPoint.X, secondDimensionPoint.Y, PreviewPrimaryBrush, 1.2);

            Vector direction = secondDimensionPoint - firstDimensionPoint;
            if (direction.Length > 1e-6)
            {
                direction.Normalize();
                Vector tick = new Vector(-direction.Y, direction.X) * 4.0;
                AddLine(firstDimensionPoint.X - tick.X, firstDimensionPoint.Y - tick.Y, firstDimensionPoint.X + tick.X, firstDimensionPoint.Y + tick.Y, PreviewPrimaryBrush, 1.2);
                AddLine(secondDimensionPoint.X - tick.X, secondDimensionPoint.Y - tick.Y, secondDimensionPoint.X + tick.X, secondDimensionPoint.Y + tick.Y, PreviewPrimaryBrush, 1.2);
            }

            Point center = new Point(
                (firstDimensionPoint.X + secondDimensionPoint.X) * 0.5,
                (firstDimensionPoint.Y + secondDimensionPoint.Y) * 0.5);
            PlaceEditorAt(editor, center);
        }

        private static void PlaceEditorAt(FrameworkElement editor, Point center)
        {
            editor.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double width = Math.Max(36.0, editor.DesiredSize.Width);
            double height = Math.Max(16.0, editor.DesiredSize.Height);
            Canvas.SetLeft(editor, Math.Max(2.0, Math.Min(458.0 - width, center.X - (width * 0.5))));
            Canvas.SetTop(editor, Math.Max(2.0, Math.Min(278.0 - height, center.Y - (height * 0.5))));
        }

        private static double GetPreviewFlightRun(int risers, double treadDepth)
        {
            return Math.Max(1, risers - 1) * treadDepth;
        }

        private static Brush GetPreviewBrush(string layerName)
        {
            if (layerName == StairLayers.PlanHidden)
            {
                return PreviewHiddenBrush;
            }

            if (layerName == StairLayers.PlanCut)
            {
                return PreviewCutBrush;
            }

            if (layerName == StairLayers.PlanAnnotation)
            {
                return PreviewPrimaryBrush;
            }

            if (layerName == StairLayers.Concrete)
            {
                return PreviewConcreteBrush;
            }

            if (layerName == StairLayers.Finish)
            {
                return PreviewFinishBrush;
            }

            if (layerName == StairLayers.Brick)
            {
                return PreviewBrickBrush;
            }

            if (layerName == StairLayers.Railing)
            {
                return PreviewRailingBrush;
            }

            if (layerName == StairLayers.RailingGlass)
            {
                return PreviewGlassBrush;
            }

            return PreviewSecondaryBrush;
        }

        private static Brush GetPreviewFill(string layerName)
        {
            return layerName == StairLayers.Concrete
                ? PreviewConcreteFillBrush
                : Brushes.Transparent;
        }

        private static double GetPreviewStrokeThickness(string layerName)
        {
            if (layerName == StairLayers.PlanCut || layerName == StairLayers.Concrete)
            {
                return 2.4;
            }

            if (layerName == StairLayers.PlanAnnotation || layerName == StairLayers.Finish)
            {
                return 2.0;
            }

            if (layerName == StairLayers.Railing)
            {
                return 1.6;
            }

            if (layerName == StairLayers.RailingGlass)
            {
                return 1.2;
            }

            return 1.2;
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

            PreviewGeometryCanvas.Children.Add(line);
        }

        private void AddPolyline(
            IEnumerable<Point> points,
            Brush stroke,
            double thickness,
            bool closed = false,
            Brush fill = null,
            bool dashed = false)
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
            if (dashed)
            {
                shape.StrokeDashArray = new DoubleCollection { 5.0, 4.0 };
            }

            PreviewGeometryCanvas.Children.Add(shape);
        }

        private bool TryBuildConfiguration(out StairDrawingConfiguration configuration)
        {
            configuration = null;
            ResetValidationState();

            StairPlanType stairType = SelectedStairType;
            if (!TryReadDouble(FloorHeightTextBox, "Chiều cao tầng", MinimumFloorHeight, MaximumFloorHeight, out double floorHeight)
                || !TryReadDouble(TreadDepthTextBox, "Chiều rộng mặt bậc", MinimumTreadDepth, MaximumTreadDepth, out double treadDepth)
                || !TryReadFlightRisers(stairType, out int firstFlightRisers, out int secondFlightRisers))
            {
                return false;
            }

            int totalRisers = firstFlightRisers + secondFlightRisers;

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
                    firstFlightRisers,
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

                bool drawRailing = DrawRailingCheckBox.IsChecked == true;
                double railingHeight = 900.0;
                if (drawRailing
                    && !TryReadDouble(
                        RailingHeightTextBox,
                        "Chiều cao lan can",
                        MinimumRailingHeight,
                        MaximumRailingHeight,
                        out railingHeight))
                {
                    return false;
                }

                configuration = StairDrawingConfiguration.CreateSection(new StairSectionParameters(
                    stairType,
                    floorHeight,
                    totalRisers,
                    firstFlightRisers,
                    treadDepth,
                    landingLength,
                    slabThickness,
                    finishThickness,
                    drawRailing,
                    SelectedRailingType,
                    railingHeight));
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

        private bool TryReadFlightRisers(
            StairPlanType stairType,
            out int firstFlightRisers,
            out int secondFlightRisers)
        {
            secondFlightRisers = 0;
            int maximumFirstFlightRisers = stairType == StairPlanType.Straight ? 60 : 58;
            if (!TryReadInteger(
                FirstFlightRisersTextBox,
                "Số cổ bậc vế 1",
                2,
                maximumFirstFlightRisers,
                out firstFlightRisers))
            {
                return false;
            }

            if (stairType == StairPlanType.Straight)
            {
                return true;
            }

            if (!TryReadInteger(
                SecondFlightRisersTextBox,
                "Số cổ bậc vế 2",
                2,
                58,
                out secondFlightRisers))
            {
                return false;
            }

            if (firstFlightRisers + secondFlightRisers <= 60)
            {
                return true;
            }

            ShowValidationError(
                SecondFlightRisersTextBox,
                "Tổng số cổ bậc của hai vế không được vượt quá 60.");
            return false;
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

            if (totalRisers > 60)
            {
                StatusTextBlock.Foreground = ErrorStatusBrush;
                StatusTextBlock.Text = "Tổng số cổ bậc của các vế không được vượt quá 60.";
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

        private sealed class PreviewValues
        {
            public PreviewValues(
                double floorHeight,
                int firstFlightRisers,
                int secondFlightRisers,
                double treadDepth,
                double stairWidth,
                double landing,
                double flightGap,
                double planCutHeight,
                double slabThickness,
                double finishThickness,
                bool drawRailing,
                StairRailingType railingType,
                double railingHeight)
            {
                FloorHeight = floorHeight;
                FirstFlightRisers = firstFlightRisers;
                SecondFlightRisers = secondFlightRisers;
                TreadDepth = treadDepth;
                StairWidth = stairWidth;
                Landing = landing;
                FlightGap = flightGap;
                PlanCutHeight = planCutHeight;
                SlabThickness = slabThickness;
                FinishThickness = finishThickness;
                DrawRailing = drawRailing;
                RailingType = railingType;
                RailingHeight = railingHeight;
            }

            public double FloorHeight { get; }

            public int FirstFlightRisers { get; }

            public int SecondFlightRisers { get; }

            public int TotalRisers => FirstFlightRisers + SecondFlightRisers;

            public double TreadDepth { get; }

            public double StairWidth { get; }

            public double Landing { get; }

            public double FlightGap { get; }

            public double PlanCutHeight { get; }

            public double SlabThickness { get; }

            public double FinishThickness { get; }

            public bool DrawRailing { get; }

            public StairRailingType RailingType { get; }

            public double RailingHeight { get; }
        }

        private sealed class PreviewTransform
        {
            private const double CanvasLeft = 52.0;
            private const double CanvasTop = 28.0;
            private const double CanvasRight = 408.0;
            private const double CanvasBottom = 242.0;

            private readonly double _worldCenterX;
            private readonly double _worldCenterY;

            public PreviewTransform(
                double minimumX,
                double minimumY,
                double maximumX,
                double maximumY)
            {
                double worldWidth = Math.Max(1.0, maximumX - minimumX);
                double worldHeight = Math.Max(1.0, maximumY - minimumY);
                Scale = Math.Min(
                    (CanvasRight - CanvasLeft) / worldWidth,
                    (CanvasBottom - CanvasTop) / worldHeight);
                _worldCenterX = (minimumX + maximumX) * 0.5;
                _worldCenterY = (minimumY + maximumY) * 0.5;
            }

            public double Scale { get; }

            public Point ToScreen(Point2 point)
            {
                double canvasCenterX = (CanvasLeft + CanvasRight) * 0.5;
                double canvasCenterY = (CanvasTop + CanvasBottom) * 0.5;
                return new Point(
                    canvasCenterX + ((point.X - _worldCenterX) * Scale),
                    canvasCenterY - ((point.Y - _worldCenterY) * Scale));
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
