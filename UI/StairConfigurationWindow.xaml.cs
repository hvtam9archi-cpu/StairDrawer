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
        private static readonly Brush PreviewSectionTraceBrush = CreateBrush(94, 234, 212);
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
        private readonly StairDrawingConfiguration _previousConfiguration;
        private TextBox _invalidTextBox;
        private bool _isRuntimeInitialized;
        private bool _isSynchronizingFields;
        private StairPlanType? _lastStairType;
        private double? _sectionCutPosition;
        private bool _draggingSectionCut;
        private StairSectionCut _planPreviewCut;
        private PreviewTransform _planPreviewTransform;

        internal StairConfigurationWindow(
            StairDrawingMode initialDrawingMode,
            bool allowDrawingModeSelection,
            StairDrawingConfiguration previousConfiguration = null)
        {
            _initialDrawingMode = initialDrawingMode;
            _allowDrawingModeSelection = allowDrawingModeSelection;
            _previousConfiguration = previousConfiguration;
            InitializeComponent();
        }

        internal StairDrawingConfiguration Configuration { get; private set; }
        internal bool ExtractDetail { get; private set; }

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

        private bool HasStraightLanding => SelectedStairType == StairPlanType.Straight
            && StraightLandingCheckBox.IsChecked == true;

        private bool HasSecondFlight => SelectedStairType != StairPlanType.Straight || HasStraightLanding;

        private StairCutSelection SelectedSectionCut => new StairCutSelection(
            _sectionCutPosition, ReverseSectionViewCheckBox.IsChecked == true);

        private void OnWindowLoaded(object sender, RoutedEventArgs eventArgs)
        {
            if (_isRuntimeInitialized || System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
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
                    if (_previousConfiguration == null)
                    {
                        InitializeRiserDistribution();
                    }
                    else
                    {
                        RestoreStairDimensions(_previousConfiguration);
                    }

                    _isRuntimeInitialized = true;
                    UpdateInterface();
                });
        }

        private void RestoreStairDimensions(StairDrawingConfiguration configuration)
        {
            StairPlanParameters plan = configuration.PlanParameters;
            StairSectionParameters section = configuration.SectionParameters;
            StairPlanType shape = plan?.PlanType ?? section.PlanType;
            StraightRadioButton.IsChecked = shape == StairPlanType.Straight;
            LShapeRadioButton.IsChecked = shape == StairPlanType.LShape;
            UShapeRadioButton.IsChecked = shape == StairPlanType.UShape;
            StraightLandingCheckBox.IsChecked = plan?.HasStraightLanding ?? section.HasStraightLanding;
            FloorHeightTextBox.Text = (plan?.FloorHeight ?? section.FloorHeight).ToString(CultureInfo.CurrentCulture);
            TreadDepthTextBox.Text = (plan?.TreadDepth ?? section.TreadDepth).ToString(CultureInfo.CurrentCulture);
            FirstFlightRisersTextBox.Text = (plan?.FirstFlightRisers ?? section.FirstFlightRisers).ToString(CultureInfo.CurrentCulture);
            SecondFlightRisersTextBox.Text = (plan?.SecondFlightRisers ?? section.SecondFlightRisers).ToString(CultureInfo.CurrentCulture);
            StairWidthTextBox.Text = (plan?.StairWidth ?? section.PlanStairWidth).ToString(CultureInfo.CurrentCulture);
            LandingDepthTextBox.Text = (plan?.LandingDepth ?? section.PlanLandingDepth).ToString(CultureInfo.CurrentCulture);
            FlightGapTextBox.Text = (plan?.FlightGap ?? section.PlanFlightGap).ToString(CultureInfo.CurrentCulture);
            UTurnSteps steps = plan?.TurnSteps ?? section.TurnSteps;
            MiddleFlightTreadsTextBox.Text = Math.Max(0, steps.MiddleFlightRisers - 1).ToString(CultureInfo.CurrentCulture);
            FirstWinderRisersTextBox.Text = steps.FirstWinderRisers.ToString(CultureInfo.CurrentCulture);
            SecondWinderRisersTextBox.Text = steps.SecondWinderRisers.ToString(CultureInfo.CurrentCulture);
            if (plan != null)
            {
                PlanCutHeightTextBox.Text = plan.PlanCutHeight.ToString(CultureInfo.CurrentCulture);
                DrawPlanRailingCheckBox.IsChecked = plan.DrawRailing;
            }
            else
            {
                LandingLengthTextBox.Text = section.LandingLength.ToString(CultureInfo.CurrentCulture);
                SlabThicknessTextBox.Text = section.WaistSlabThickness.ToString(CultureInfo.CurrentCulture);
                FinishThicknessTextBox.Text = section.FinishThickness.ToString(CultureInfo.CurrentCulture);
                DrawRailingCheckBox.IsChecked = section.DrawRailing;
                RailingHeightTextBox.Text = section.RailingHeight.ToString(CultureInfo.CurrentCulture);
                IronRailingRadioButton.IsChecked = section.RailingType == StairRailingType.Iron;
                GlassRailingRadioButton.IsChecked = section.RailingType == StairRailingType.Glass;
            }

            StairCutSelection cut = plan?.SectionCut ?? section.SectionCut;
            _sectionCutPosition = cut.Position;
            ReverseSectionViewCheckBox.IsChecked = cut.ReverseView;
            _lastStairType = shape;
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
                        || sender == UShapeRadioButton
                        || sender == StraightLandingCheckBox)
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

        private void OnTurnStepsChanged(object sender, TextChangedEventArgs eventArgs)
        {
            if (_isRuntimeInitialized && !_isSynchronizingFields)
            {
                ExecuteUiAction("update U-turn steps", UpdateInterface);
            }
        }

        private void OnPreviewSizeChanged(object sender, SizeChangedEventArgs eventArgs)
        {
            if (_isRuntimeInitialized)
            {
                ExecuteUiAction("resize stair preview", () => DrawPreview(SelectedDrawingMode, SelectedStairType));
            }
        }

        private bool IsOnSectionCut(Point point)
        {
            if (SelectedDrawingMode != StairDrawingMode.Plan || _planPreviewTransform == null || _planPreviewCut == null)
            {
                return false;
            }

            Point start = _planPreviewTransform.ToScreen(new Point2(_planPreviewCut.StartX, _planPreviewCut.Y));
            Point end = _planPreviewTransform.ToScreen(new Point2(_planPreviewCut.EndX, _planPreviewCut.Y));
            return Math.Abs(point.Y - start.Y) <= 12.0 && point.X >= start.X - 18.0 && point.X <= end.X + 18.0;
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs eventArgs)
        {
            if (!IsOnSectionCut(eventArgs.GetPosition(PreviewCanvas)) || !PreviewCanvas.CaptureMouse())
            {
                return;
            }

            _draggingSectionCut = true;
            PreviewCanvas.Cursor = Cursors.SizeNS;
            eventArgs.Handled = true;
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs eventArgs)
        {
            Point point = eventArgs.GetPosition(PreviewCanvas);
            if (_draggingSectionCut)
            {
                MoveSectionCut(point.Y);
                return;
            }

            PreviewCanvas.Cursor = IsOnSectionCut(point) ? Cursors.SizeNS : null;
        }

        private void MoveSectionCut(double screenY)
        {
            double position = _planPreviewTransform.ToWorldY(screenY) / _planPreviewCut.TotalWidth;
            _sectionCutPosition = Math.Max(0.0, Math.Min(1.0, position));
            ExecuteUiAction("drag section cut", () => DrawPreview(SelectedDrawingMode, SelectedStairType));
        }

        private void OnPreviewMouseUp(object sender, MouseButtonEventArgs eventArgs)
        {
            if (_draggingSectionCut)
            {
                PreviewCanvas.ReleaseMouseCapture();
                eventArgs.Handled = true;
            }
        }

        private void OnPreviewLostMouseCapture(object sender, MouseEventArgs eventArgs)
        {
            if (_draggingSectionCut)
            {
                _draggingSectionCut = false;
                PreviewCanvas.Cursor = null;
                ExecuteUiAction("finish section cut drag", () => DrawPreview(SelectedDrawingMode, SelectedStairType));
            }
        }

        private void OnDrawClicked(object sender, RoutedEventArgs eventArgs)
        {
            AcceptConfiguration(false);
        }

        private void OnDrawDetailClicked(object sender, RoutedEventArgs eventArgs)
        {
            AcceptConfiguration(true);
        }

        private void AcceptConfiguration(bool extractDetail)
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
                    ExtractDetail = extractDetail;
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
            UTurnSteps turnSteps = ReadPreviewTurnSteps(stairType);
            bool isPlan = drawingMode == StairDrawingMode.Plan;
            DrawDetailButton.Visibility = isPlan ? Visibility.Collapsed : Visibility.Visible;
            bool usesPlanLanding = HasStraightLanding || stairType != StairPlanType.Straight;

            PlanFieldsHeader.Text = usesPlanLanding
                ? "Kích thước chiếu nghỉ và vế thang (mm)" : "Kích thước mặt bằng (mm)";
            PlanCutHeightRow.Visibility = isPlan ? Visibility.Visible : Visibility.Collapsed;
            DrawPlanRailingCheckBox.Visibility = isPlan ? Visibility.Visible : Visibility.Collapsed;
            UTurnFieldsPanel.Visibility = stairType == StairPlanType.UShape ? Visibility.Visible : Visibility.Collapsed;
            StraightLandingCheckBox.Visibility = stairType == StairPlanType.Straight ? Visibility.Visible : Visibility.Collapsed;
            SectionFieldsPanel.Visibility = drawingMode == StairDrawingMode.Section
                ? Visibility.Visible
                : Visibility.Collapsed;
            LandingDepthRow.Visibility = HasSecondFlight ? Visibility.Visible : Visibility.Collapsed;
            LandingDepthLabel.Text = HasStraightLanding ? "Chiều dài chiếu nghỉ (E)" : "Chiều sâu chiếu nghỉ (E)";
            LandingDepthTextBox.ToolTip = HasStraightLanding ? "600–5000 mm" : "Không nhỏ hơn chiều rộng vế thang";
            FlightGapRow.Visibility = stairType == StairPlanType.UShape && !turnSteps.HasMiddleFlight
                ? Visibility.Visible
                : Visibility.Collapsed;
            SecondFlightRisersRow.Visibility = HasSecondFlight ? Visibility.Visible : Visibility.Collapsed;
            SecondFlightRisersLabel.Text = turnSteps.HasSteps ? "Số cổ bậc vế trên (B)" : "Số cổ bậc vế 2 (B)";
            SectionLandingLengthRow.Visibility = stairType == StairPlanType.LShape
                ? Visibility.Collapsed : Visibility.Visible;
            RailingOptionsPanel.IsEnabled = drawingMode == StairDrawingMode.Section
                && DrawRailingCheckBox.IsChecked == true;

            TotalRisersTextBox.IsReadOnly = true;
            TotalRisersLabel.Text = "Tổng số cổ bậc (tự tính)";

            SynchronizeTotalRisers();
            DrawPreview(drawingMode, stairType);
            UpdateRiserHeightHint();
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
                if (!HasSecondFlight)
                {
                    FirstFlightRisersTextBox.Text = totalRisers.ToString(CultureInfo.CurrentCulture);
                    SecondFlightRisersTextBox.Text = "0";
                }
                else
                {
                    totalRisers = Math.Max(4, totalRisers - (int)ReadPreviewTurnSteps(stairType).TotalRisers);
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
            if (HasSecondFlight
                && !int.TryParse(
                    SecondFlightRisersTextBox.Text.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.CurrentCulture,
                    out secondFlightRisers))
            {
                return;
            }

            long turnRisers = 0;
            if (SelectedStairType == StairPlanType.UShape)
            {
                if (!TryParseInteger(MiddleFlightTreadsTextBox, out int middleTreads)
                    || !TryParseInteger(FirstWinderRisersTextBox, out int firstWinder)
                    || !TryParseInteger(SecondWinderRisersTextBox, out int secondWinder))
                {
                    return;
                }

                turnRisers = (long)middleTreads + (middleTreads > 0 ? 1 : 0) + firstWinder + secondWinder;
            }

            _isSynchronizingFields = true;
            try
            {
                TotalRisersTextBox.Text = (turnRisers + firstFlightRisers + secondFlightRisers)
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
            StairSectionCut cut;
            if (drawingMode == StairDrawingMode.Plan)
            {
                var parameters = new StairPlanParameters(
                    stairType,
                    values.FloorHeight,
                    values.TotalRisers,
                    values.FirstFlightRisers,
                    values.TreadDepth,
                    values.StairWidth,
                    values.Landing,
                    values.FlightGap,
                    values.PlanCutHeight,
                    values.TurnSteps,
                    values.HasStraightLanding,
                    DrawPlanRailingCheckBox.IsChecked == true,
                    SelectedSectionCut);
                cut = StairSectionCut.FromPlan(parameters);
                drawing = StairPlanGeometry.Create(parameters);
            }
            else
            {
                var parameters = new StairSectionParameters(
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
                    values.RailingHeight,
                    values.TurnSteps,
                    values.StairWidth,
                    values.PlanLandingDepth,
                    values.HasStraightLanding,
                    values.FlightGap,
                    SelectedSectionCut);
                cut = StairSectionCut.FromSection(parameters);
                drawing = StairSectionGeometry.Create(parameters);
            }

            PreviewTransform transform = _draggingSectionCut && drawingMode == StairDrawingMode.Plan
                ? _planPreviewTransform
                : CreatePreviewTransform(drawing, drawingMode, values);
            if (drawingMode == StairDrawingMode.Section)
            {
                DrawSectionFloorLevels(values, transform);
            }
            else
            {
                _planPreviewCut = cut;
                _planPreviewTransform = transform;
            }

            RenderDrawingModel(drawing, transform);
            if (drawingMode == StairDrawingMode.Plan)
            {
                DrawPlanDimensions(stairType, values, transform);
            }

            PreviewModeBadge.Text = (drawingMode == StairDrawingMode.Plan ? "MẶT BẰNG · " : "MẶT CẮT · ")
                + GetStairTypeLabel(stairType).ToUpperInvariant();
        }

        private PreviewValues ReadPreviewValues(StairDrawingMode drawingMode, StairPlanType stairType)
        {
            int firstFlightRisers = ReadPreviewInteger(
                FirstFlightRisersTextBox,
                HasSecondFlight ? 10 : 20,
                2,
                HasSecondFlight ? 58 : 60);
            int secondFlightRisers = HasSecondFlight ? ReadPreviewInteger(SecondFlightRisersTextBox, 10, 2, 58) : 0;

            double floorHeight = ReadPreviewDouble(FloorHeightTextBox, 3600.0, MinimumFloorHeight, MaximumFloorHeight);
            double stairWidth = ReadPreviewDouble(StairWidthTextBox, 1000.0, MinimumStairWidth, MaximumStairWidth);
            double planLandingDepth = ReadPreviewDouble(LandingDepthTextBox, 1000.0, MinimumStairWidth, MaximumStairWidth);
            if (stairType != StairPlanType.Straight)
            {
                planLandingDepth = Math.Max(stairWidth, planLandingDepth);
            }
            double landing = drawingMode == StairDrawingMode.Plan
                ? planLandingDepth
                : stairType == StairPlanType.LShape ? 1000.0
                : ReadPreviewDouble(LandingLengthTextBox, 1000.0, MinimumStairWidth, MaximumStairWidth);

            return new PreviewValues(
                floorHeight,
                firstFlightRisers,
                secondFlightRisers,
                ReadPreviewDouble(TreadDepthTextBox, 250.0, MinimumTreadDepth, MaximumTreadDepth),
                stairWidth,
                landing,
                ReadPreviewDouble(FlightGapTextBox, 100.0, 0.0, 1000.0),
                ReadPreviewDouble(PlanCutHeightTextBox, 1200.0, 1.0, floorHeight - 1.0),
                ReadPreviewDouble(SlabThicknessTextBox, 120.0, MinimumSlabThickness, MaximumSlabThickness),
                ReadPreviewDouble(FinishThicknessTextBox, 20.0, MinimumFinishThickness, MaximumFinishThickness),
                drawingMode == StairDrawingMode.Section && DrawRailingCheckBox.IsChecked == true,
                SelectedRailingType,
                ReadPreviewDouble(RailingHeightTextBox, 900.0, MinimumRailingHeight, MaximumRailingHeight),
                ReadPreviewTurnSteps(stairType),
                planLandingDepth,
                HasStraightLanding);
        }

        private UTurnSteps ReadPreviewTurnSteps(StairPlanType stairType)
        {
            if (stairType != StairPlanType.UShape)
            {
                return default;
            }

            int middleTreads = ReadPreviewInteger(MiddleFlightTreadsTextBox, 0, 0, 55);
            return new UTurnSteps(
                middleTreads == 0 ? 0 : middleTreads + 1,
                ReadPreviewInteger(FirstWinderRisersTextBox, 0, 0, 56),
                ReadPreviewInteger(SecondWinderRisersTextBox, 0, 0, 56));
        }

        private static int ReadPreviewInteger(TextBox textBox, int fallback, int minimum, int maximum)
        {
            if (!TryParseInteger(textBox, out int value))
            {
                return fallback;
            }

            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static bool TryParseInteger(TextBox textBox, out int value)
        {
            return int.TryParse(textBox.Text.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out value);
        }

        private static double ReadPreviewDouble(TextBox textBox, double fallback, double minimum, double maximum)
        {
            if (!TryParseDouble(textBox.Text, out double value))
            {
                return fallback;
            }

            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private PreviewTransform CreatePreviewTransform(
            DrawingModel drawing, StairDrawingMode drawingMode, PreviewValues values)
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

            if (drawingMode == StairDrawingMode.Section)
            {
                minimumY = Math.Min(minimumY, values.FinishThickness);
                maximumY = Math.Max(maximumY, values.FloorHeight + values.FinishThickness);
            }

            return new PreviewTransform(
                minimumX, minimumY, maximumX, maximumY,
                PreviewCanvas.ActualWidth, PreviewCanvas.ActualHeight, drawingMode == StairDrawingMode.Section);
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
                Line shape = AddLine(
                    start.X,
                    start.Y,
                    end.X,
                    end.Y,
                    GetPreviewBrush(line.LayerName),
                    GetPreviewStrokeThickness(line.LayerName),
                    line.LayerName == StairLayers.PlanHidden);
                if (line.LayerName == StairLayers.SectionTrace)
                {
                    shape.StrokeDashArray = new DoubleCollection { 11.0, 4.0, 1.0, 4.0 };
                }
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
                    FontSize = Math.Max(3.0, Math.Min(16.0, text.Height * transform.Scale)),
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
            Brush brickHatch = null;
            Brush concreteHatch = null;
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

                Brush fill = GetPreviewFill(polyline.LayerName);
                if (polyline.Closed && polyline.LayerName == StairLayers.Brick)
                {
                    brickHatch = brickHatch ?? CreateBrickHatchBrush(transform.Scale);
                    fill = brickHatch;
                }
                else if (polyline.Closed && polyline.LayerName == StairLayers.Concrete)
                {
                    concreteHatch = concreteHatch ?? CreateConcreteHatchBrush(transform.Scale);
                    fill = concreteHatch;
                }
                else if (polyline.Closed && polyline.LayerName == StairLayers.RailingGlass)
                {
                    RenderGlassHatch(polyline, transform);
                }

                AddPolyline(
                    points,
                    GetPreviewBrush(polyline.LayerName),
                    polyline.GlobalWidth > 0.0
                        ? Math.Max(2.0, polyline.GlobalWidth * transform.Scale)
                        : GetPreviewStrokeThickness(polyline.LayerName),
                    polyline.Closed,
                    fill,
                    polyline.LayerName == StairLayers.PlanHidden);
            }
        }

        private void RenderGlassHatch(PolylinePrimitive panel, PreviewTransform transform)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            var clip = new StreamGeometry();
            using (StreamGeometryContext context = clip.Open())
            {
                context.BeginFigure(transform.ToScreen(panel.Vertices[0]), true, true);
                for (int i = 0; i < panel.Vertices.Count; i++)
                {
                    Point2 vertex = panel.Vertices[i];
                    minX = Math.Min(minX, vertex.X); maxX = Math.Max(maxX, vertex.X);
                    minY = Math.Min(minY, vertex.Y); maxY = Math.Max(maxY, vertex.Y);
                    if (i > 0) context.LineTo(transform.ToScreen(vertex), true, false);
                }
            }

            // Metric AR-RROOF definitions from acadiso.pat, at hatch scale 10.
            var lines = new GeometryGroup();
            AddGlassHatchRows(lines, transform, minX, maxX, minY, maxY,
                0, 0, 558.8, 254, new[] { 3810.0, -508, 1270, -254 });
            AddGlassHatchRows(lines, transform, minX, maxX, minY, maxY,
                337.82, 127, -254, 337.82, new[] { 762.0, -83.82, 1524, -190.5 });
            AddGlassHatchRows(lines, transform, minX, maxX, minY, maxY,
                127, 215.9, 1320.8, 170.18, new[] { 2032.0, -355.6, 1016, -254 });
            PreviewGeometryCanvas.Children.Add(new Path
            {
                Data = lines, Clip = clip, Stroke = PreviewGlassBrush, StrokeThickness = 0.8,
                Opacity = 64.0 / 255.0, IsHitTestVisible = false
            });
        }

        private static void AddGlassHatchRows(GeometryGroup lines, PreviewTransform transform,
            double minX, double maxX, double minY, double maxY,
            double originX, double originY, double offsetX, double offsetY, double[] dashes)
        {
            double period = 0;
            foreach (double dash in dashes) period += Math.Abs(dash);
            int first = (int)Math.Ceiling((minY - originY) / offsetY);
            int last = (int)Math.Floor((maxY - originY) / offsetY);
            for (int row = first; row <= last; row++)
            {
                double y = originY + row * offsetY;
                double rowX = originX + row * offsetX;
                double start = rowX + Math.Floor((minX - rowX) / period) * period;
                for (double cycle = start; cycle < maxX; cycle += period)
                {
                    double x = cycle;
                    foreach (double dash in dashes)
                    {
                        double end = x + Math.Abs(dash);
                        if (dash > 0 && end > minX && x < maxX)
                            lines.Children.Add(new LineGeometry(
                                transform.ToScreen(new Point2(Math.Max(minX, x), y)),
                                transform.ToScreen(new Point2(Math.Min(maxX, end), y))));
                        x = end;
                    }
                }
            }
        }

        private static Brush CreateBrickHatchBrush(double scale)
        {
            double side = Math.Max(5.0, 12.7 * Math.Sqrt(2.0) * scale);
            var diagonal = new LineGeometry(new Point(0.0, side), new Point(side, 0.0));
            var tile = new Rect(0.0, 0.0, side, side);
            return new DrawingBrush(new GeometryDrawing(null, new Pen(PreviewBrickBrush, 0.7), diagonal))
            {
                TileMode = TileMode.Tile,
                Viewbox = tile,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewport = tile,
                ViewportUnits = BrushMappingMode.Absolute
            };
        }

        private static Brush CreateConcreteHatchBrush(double scale)
        {
            double side = Math.Max(8.0, 37.5 * Math.Sqrt(2.0) * scale);
            double offset = side / 3.0;
            var lines = new GeometryGroup
            {
                Children = new GeometryCollection
                {
                    new LineGeometry(new Point(0.0, side), new Point(side, 0.0)),
                    new LineGeometry(new Point(0.0, offset), new Point(offset, 0.0)),
                    new LineGeometry(new Point(offset, side), new Point(side, offset)),
                    new LineGeometry(new Point(side * 0.32, side * 0.28), new Point(side * 0.43, side * 0.34)),
                    new LineGeometry(new Point(side * 0.43, side * 0.34), new Point(side * 0.38, side * 0.43)),
                    new LineGeometry(new Point(side * 0.38, side * 0.43), new Point(side * 0.32, side * 0.28))
                }
            };
            var tile = new Rect(0.0, 0.0, side, side);
            var drawing = new DrawingGroup
            {
                Children = new DrawingCollection
                {
                    new GeometryDrawing(PreviewConcreteFillBrush, null, new RectangleGeometry(tile)),
                    new GeometryDrawing(null, new Pen(PreviewConcreteBrush, 0.65), lines)
                }
            };
            return new DrawingBrush(drawing)
            {
                TileMode = TileMode.Tile,
                Viewbox = tile,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewport = tile,
                ViewportUnits = BrushMappingMode.Absolute
            };
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

        private void DrawPlanDimensions(StairPlanType stairType, PreviewValues values, PreviewTransform transform)
        {
            double firstRun = GetPreviewFlightRun(values.FirstFlightRisers, values.TreadDepth);
            double secondRun = values.SecondFlightRisers > 0
                ? GetPreviewFlightRun(values.SecondFlightRisers, values.TreadDepth)
                : 0.0;

            double length = firstRun;
            double width = values.StairWidth;
            double firstStartX = 0.0;
            double gap = values.TurnSteps.GetGap(values.FlightGap, values.TreadDepth);
            switch (stairType)
            {
                case StairPlanType.Straight:
                    if (values.HasStraightLanding)
                    {
                        length += values.Landing + secondRun;
                    }
                    break;
                case StairPlanType.LShape:
                    length += values.Landing;
                    width += secondRun;
                    break;
                case StairPlanType.UShape:
                    double maximumRun = Math.Max(firstRun, secondRun);
                    firstStartX = maximumRun - firstRun;
                    length = maximumRun + values.Landing;
                    width = (width * 2.0) + gap;
                    break;
            }

            double below = transform.DrawingBottom + 5.0;
            double above = transform.DrawingTop - 5.0;
            double right = transform.DrawingRight + 5.0;
            DrawPlanHorizontalDimension(0.0, length, below, 40.0, transform);
            DrawMeasuredPreviewDimension(
                new Point(right, transform.ToScreen(new Point2(0.0, 0.0)).Y),
                new Point(right, transform.ToScreen(new Point2(0.0, width)).Y),
                new Vector(35.0, 0.0), FormatDimension(width), transform, verticalText: true);

            if (secondRun <= 0.0)
            {
                return;
            }

            double lowerEnd = firstStartX + firstRun;
            DrawPlanHorizontalDimension(firstStartX, lowerEnd, below, 15.0, transform);
            DrawPlanHorizontalDimension(lowerEnd, lowerEnd + values.Landing, below, 15.0, transform);

            if (values.HasStraightLanding)
            {
                DrawPlanHorizontalDimension(lowerEnd + values.Landing, length, below, 15.0, transform);
            }
            else if (stairType == StairPlanType.UShape)
            {
                DrawPlanHorizontalDimension(lowerEnd - secondRun, lowerEnd, above, -14.0, transform);
                if (gap > 1e-6)
                {
                    DrawMeasuredPreviewDimension(
                        new Point(right, transform.ToScreen(new Point2(0.0, values.StairWidth)).Y),
                        new Point(right, transform.ToScreen(new Point2(0.0, values.StairWidth + gap)).Y),
                        new Vector(12.0, 0.0), FormatDimension(gap), transform, verticalText: true);
                }
            }
            else
            {
                DrawMeasuredPreviewDimension(
                    new Point(right, transform.ToScreen(new Point2(0.0, values.StairWidth)).Y),
                    new Point(right, transform.ToScreen(new Point2(0.0, width)).Y),
                    new Vector(12.0, 0.0), FormatDimension(secondRun), transform, verticalText: true);
            }
        }

        private void DrawPlanHorizontalDimension(double startX, double endX, double y, double offset, PreviewTransform transform)
        {
            Point start = new Point(transform.ToScreen(new Point2(startX, 0.0)).X, y);
            Point end = new Point(transform.ToScreen(new Point2(endX, 0.0)).X, y);
            if (Math.Abs(offset) <= 15.0 && end.X - start.X < 58.0)
            {
                DrawPreviewDimension(start, end, new Vector(0.0, offset));
                return;
            }

            DrawMeasuredPreviewDimension(start, end, new Vector(0.0, offset), FormatDimension(endX - startX), transform);
        }

        private void DrawSectionFloorLevels(PreviewValues values, PreviewTransform transform)
        {
            double bottom = values.FinishThickness;
            double top = bottom + values.FloorHeight;
            double left = transform.ToScreen(new Point2(transform.MinimumX, bottom)).X;
            double right = transform.ToScreen(new Point2(transform.MaximumX, bottom)).X;
            double axisStart = Math.Max(6.0, left - 67.0);
            double axisEnd = right + 13.0;
            double bottomY = transform.ToScreen(new Point2(0.0, bottom)).Y;
            double topY = transform.ToScreen(new Point2(0.0, top)).Y;
            DrawFloorAxis(axisStart, axisEnd, bottomY, "±0.000", transform);
            DrawFloorAxis(
                axisStart, axisEnd, topY,
                "+" + (values.FloorHeight / 1000.0).ToString("0.000", CultureInfo.InvariantCulture), transform);
            DrawMeasuredPreviewDimension(
                new Point(axisEnd, bottomY), new Point(axisEnd, topY),
                new Vector(23.0, 0.0), FormatDimension(values.FloorHeight), transform, verticalText: true);
        }

        private void DrawFloorAxis(double startX, double endX, double y, string level, PreviewTransform transform)
        {
            PreviewGeometryCanvas.Children.Add(new Line
            {
                X1 = startX, Y1 = y, X2 = endX, Y2 = y,
                Stroke = PreviewHiddenBrush, StrokeThickness = 1.0,
                StrokeDashArray = new DoubleCollection { 11.0, 4.0, 1.0, 4.0 }
            });
            AddPolyline(
                new[] { new Point(startX + 5.0, y - 6.0), new Point(startX + 11.0, y), new Point(startX + 17.0, y - 6.0) },
                PreviewHiddenBrush, 1.0);
            DrawPreviewLabel(new Point(startX + 42.0, y - 10.0), level, transform);
        }

        private void DrawMeasuredPreviewDimension(
            Point first, Point second, Vector offset, string text, PreviewTransform transform, bool verticalText = false)
        {
            Point center = DrawPreviewDimension(first, second, offset);
            DrawPreviewLabel(center, text, transform, verticalText);
        }

        private static string FormatDimension(double millimeters)
        {
            return millimeters.ToString("0.##", CultureInfo.CurrentCulture) + " mm";
        }

        private void DrawPreviewLabel(Point center, string text, PreviewTransform transform, bool verticalText = false)
        {
            var label = new TextBlock
            {
                Text = text,
                FontSize = 11.0,
                Foreground = PreviewSecondaryBrush
            };
            if (verticalText)
            {
                label.LayoutTransform = new RotateTransform(-90.0);
            }

            var badge = new Border
            {
                Background = PreviewWipeoutBrush,
                Padding = new Thickness(4.0, 1.0, 4.0, 1.0),
                Child = label
            };
            PlaceEditorAt(badge, center, transform);
            PreviewGeometryCanvas.Children.Add(badge);
        }

        private Point DrawPreviewDimension(Point firstPoint, Point secondPoint, Vector screenOffset)
        {
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

            return new Point(
                (firstDimensionPoint.X + secondDimensionPoint.X) * 0.5,
                (firstDimensionPoint.Y + secondDimensionPoint.Y) * 0.5);
        }

        private static void PlaceEditorAt(FrameworkElement editor, Point center, PreviewTransform transform)
        {
            editor.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double width = editor.DesiredSize.Width;
            double height = editor.DesiredSize.Height;
            Canvas.SetLeft(editor, Math.Max(2.0, Math.Min(transform.Width - 2.0 - width, center.X - (width * 0.5))));
            Canvas.SetTop(editor, Math.Max(2.0, Math.Min(transform.Height - 2.0 - height, center.Y - (height * 0.5))));
        }

        private static double GetPreviewFlightRun(int risers, double treadDepth)
        {
            return Math.Max(1, risers - 1) * treadDepth;
        }

        private static Brush GetPreviewBrush(string layerName)
        {
            if (layerName == StairLayers.SectionTrace || layerName == StairLayers.SectionTraceMark)
            {
                return PreviewSectionTraceBrush;
            }

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
            if (layerName == StairLayers.Concrete) return PreviewConcreteFillBrush;
            return Brushes.Transparent;
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

        private Line AddLine(
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
            return line;
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
            bool hasStraightLanding = HasStraightLanding;
            if (!TryReadDouble(FloorHeightTextBox, "Chiều cao tầng", MinimumFloorHeight, MaximumFloorHeight, out double floorHeight)
                || !TryReadDouble(TreadDepthTextBox, "Chiều rộng mặt bậc", MinimumTreadDepth, MaximumTreadDepth, out double treadDepth)
                || !TryReadFlightRisers(out int firstFlightRisers, out int secondFlightRisers)
                || !TryReadTurnSteps(stairType, out UTurnSteps turnSteps))
            {
                return false;
            }

            int totalRisers = firstFlightRisers + secondFlightRisers + (int)turnSteps.TotalRisers;
            if (totalRisers > 60)
            {
                ShowValidationError(SecondFlightRisersTextBox, "Tổng số cổ bậc không được vượt quá 60.");
                return false;
            }

            bool isPlan = SelectedDrawingMode == StairDrawingMode.Plan;
            double stairWidth = 0.0;
            double landingDepth = 0.0;
            double flightGap = 0.0;
            if (!TryReadPlanDimensions(stairType, turnSteps, out stairWidth, out landingDepth, out flightGap))
            {
                return false;
            }

            if (isPlan)
            {
                if (!TryReadDouble(PlanCutHeightTextBox, "Cao độ mặt phẳng cắt", 100.0, floorHeight - 1.0, out double planCutHeight))
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
                    planCutHeight,
                    turnSteps,
                    hasStraightLanding,
                    DrawPlanRailingCheckBox.IsChecked == true,
                    SelectedSectionCut));
            }
            else
            {
                double landingLength = 1000.0;
                if ((stairType != StairPlanType.LShape
                        && !TryReadDouble(LandingLengthTextBox, SectionLandingLengthLabel.Text, MinimumStairWidth, MaximumStairWidth, out landingLength))
                    || !TryReadDouble(SlabThicknessTextBox, "Chiều dày bản BTCT", MinimumSlabThickness, MaximumSlabThickness, out double slabThickness)
                    || !TryReadDouble(FinishThicknessTextBox, "Chiều dày đá ốp", MinimumFinishThickness, MaximumFinishThickness, out double finishThickness))
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
                    railingHeight,
                    turnSteps,
                    stairWidth,
                    landingDepth,
                    hasStraightLanding,
                    flightGap,
                    SelectedSectionCut));
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
            out int firstFlightRisers,
            out int secondFlightRisers)
        {
            secondFlightRisers = 0;
            int maximumFirstFlightRisers = HasSecondFlight ? 58 : 60;
            if (!TryReadInteger(
                FirstFlightRisersTextBox,
                "Số cổ bậc vế 1",
                2,
                maximumFirstFlightRisers,
                out firstFlightRisers))
            {
                return false;
            }

            if (!HasSecondFlight)
            {
                return true;
            }

            return TryReadInteger(
                SecondFlightRisersTextBox,
                SecondFlightRisersLabel.Text,
                2,
                58,
                out secondFlightRisers);
        }

        private bool TryReadTurnSteps(StairPlanType stairType, out UTurnSteps steps)
        {
            steps = default;
            if (stairType != StairPlanType.UShape)
            {
                return true;
            }

            if (!TryReadInteger(MiddleFlightTreadsTextBox, "Số mặt bậc vế giữa", 0, 55, out int middleTreads)
                || !TryReadInteger(FirstWinderRisersTextBox, "Số cổ bậc chéo chiếu 1", 0, 56, out int firstWinder)
                || !TryReadInteger(SecondWinderRisersTextBox, "Số cổ bậc chéo chiếu 2", 0, 56, out int secondWinder))
            {
                return false;
            }

            steps = new UTurnSteps(middleTreads == 0 ? 0 : middleTreads + 1, firstWinder, secondWinder);
            return true;
        }

        private bool TryReadPlanDimensions(
            StairPlanType stairType, UTurnSteps steps, out double width, out double depth, out double gap)
        {
            depth = 0.0;
            gap = 0.0;
            if (!TryReadDouble(StairWidthTextBox, "Chiều rộng vế thang", MinimumStairWidth, MaximumStairWidth, out width))
            {
                return false;
            }

            depth = width;
            if (HasSecondFlight
                && (!TryReadDouble(LandingDepthTextBox, LandingDepthLabel.Text, MinimumStairWidth, MaximumStairWidth, out depth)
                    || (stairType != StairPlanType.Straight && !ValidateLandingDepth(depth, width))))
            {
                return false;
            }

            return stairType != StairPlanType.UShape || steps.HasMiddleFlight
                || TryReadDouble(FlightGapTextBox, "Khe giữa hai vế song song", 0.0, 1000.0, out gap);
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
                double railingHeight,
                UTurnSteps turnSteps,
                double planLandingDepth,
                bool hasStraightLanding)
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
                TurnSteps = turnSteps;
                PlanLandingDepth = planLandingDepth;
                HasStraightLanding = hasStraightLanding;
            }

            public double FloorHeight { get; }

            public int FirstFlightRisers { get; }

            public int SecondFlightRisers { get; }

            public int TotalRisers => FirstFlightRisers + SecondFlightRisers + (int)TurnSteps.TotalRisers;

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

            public UTurnSteps TurnSteps { get; }

            public double PlanLandingDepth { get; }

            public bool HasStraightLanding { get; }
        }

        private sealed class PreviewTransform
        {
            private readonly double _worldCenterX;
            private readonly double _worldCenterY;
            private readonly double _screenCenterX;
            private readonly double _screenCenterY;
            private readonly double _minimumY;
            private readonly double _maximumY;

            public PreviewTransform(
                double minimumX,
                double minimumY,
                double maximumX,
                double maximumY,
                double viewportWidth,
                double viewportHeight,
                bool section)
            {
                Width = viewportWidth > 1.0 ? viewportWidth : 460.0;
                Height = viewportHeight > 1.0 ? viewportHeight : 280.0;
                MinimumX = minimumX;
                MaximumX = maximumX;
                _minimumY = minimumY;
                _maximumY = maximumY;
                double worldWidth = Math.Max(1.0, maximumX - minimumX);
                double worldHeight = Math.Max(1.0, maximumY - minimumY);
                double left = Math.Min(section ? 79.0 : 38.0, Width * 0.18);
                double right = Math.Min(53.0, Width * 0.16);
                double top = Math.Min(36.0, Height * 0.12);
                double bottom = Math.Min(section ? 36.0 : 59.0, Height * 0.22);
                Scale = Math.Min(
                    (Width - left - right) / worldWidth,
                    (Height - top - bottom) / worldHeight);
                _worldCenterX = (minimumX + maximumX) * 0.5;
                _worldCenterY = (minimumY + maximumY) * 0.5;
                _screenCenterX = (Width + left - right) * 0.5;
                _screenCenterY = (Height + top - bottom) * 0.5;
            }

            public double Scale { get; }
            public double Width { get; }
            public double Height { get; }
            public double MinimumX { get; }
            public double MaximumX { get; }
            public double DrawingTop => ToScreen(new Point2(0.0, _maximumY)).Y;
            public double DrawingBottom => ToScreen(new Point2(0.0, _minimumY)).Y;
            public double DrawingRight => ToScreen(new Point2(MaximumX, 0.0)).X;

            public Point ToScreen(Point2 point)
            {
                return new Point(
                    _screenCenterX + ((point.X - _worldCenterX) * Scale),
                    _screenCenterY - ((point.Y - _worldCenterY) * Scale));
            }

            public double ToWorldY(double screenY)
            {
                return _worldCenterY + ((_screenCenterY - screenY) / Scale);
            }
        }

        private static Brush CreateBrush(byte red, byte green, byte blue, byte alpha = 255)
        {
            var brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}
