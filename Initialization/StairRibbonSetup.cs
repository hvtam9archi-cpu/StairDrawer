using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Windows;
using StairDrawer.Commands;
using AutoCADApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using MediaGeometry = System.Windows.Media.Geometry;

namespace StairDrawer.Initialization
{
    internal sealed class StairRibbonSetup
    {
        private const string TabId = "TH_TOOLS_TAB";
        private const string PanelId = "STAIRDRAWER_PANEL";
        private bool _initialized;
        private bool _waitingForRibbon;

        public void Initialize()
        {
            if (_initialized) return;
            AutoCADApplication.SystemVariableChanged += OnSystemVariableChanged;
            _initialized = true;
            WaitForRibbon();
        }

        public void Terminate()
        {
            if (!_initialized) return;
            StopWaiting();
            AutoCADApplication.SystemVariableChanged -= OnSystemVariableChanged;
            _initialized = false;
        }

        private void WaitForRibbon()
        {
            if (_waitingForRibbon) return;
            AutoCADApplication.Idle += OnIdle;
            _waitingForRibbon = true;
        }

        private void StopWaiting()
        {
            if (!_waitingForRibbon) return;
            AutoCADApplication.Idle -= OnIdle;
            _waitingForRibbon = false;
        }

        private void OnSystemVariableChanged(object sender, SystemVariableChangedEventArgs args)
        {
            if (string.Equals(args.Name, "WSCURRENT", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args.Name, "RIBBONSTATE", StringComparison.OrdinalIgnoreCase)) WaitForRibbon();
        }

        private void OnIdle(object sender, EventArgs args)
        {
            try
            {
                if (Install(ComponentManager.Ribbon)) StopWaiting();
            }
            catch (Exception error)
            {
                Trace.TraceWarning("StairDrawer ribbon setup: {0}", error);
            }
        }

        internal static bool Install(RibbonControl ribbon)
        {
            if (ribbon == null) return false;
            RibbonTab tab = ribbon.FindTab(TabId);
            if (tab == null)
            {
                tab = new RibbonTab { Id = TabId, Title = "TH Tools" };
                ribbon.Tabs.Add(tab);
                tab.IsActive = true;
            }

            RibbonPanelSource source = null;
            foreach (RibbonPanel panel in tab.Panels)
                if (panel.Source?.Id == PanelId) { source = panel.Source; break; }
            if (source == null)
            {
                source = new RibbonPanelSource { Id = PanelId, Title = "Cầu thang" };
                tab.Panels.Add(new RibbonPanel { Source = source });
            }

            AddButton(source, "STAIRDRAWER_DRAW", "Vẽ thang", CommandNames.Draw,
                "Vẽ mặt bằng hoặc mặt cắt thang thẳng, chữ L, chữ U.", false);
            AddButton(source, "STAIRDRAWER_DETAIL", "Trích chi tiết", CommandNames.Detail,
                "Trích chi tiết bậc thang hoặc mũi bậc từ mặt cắt đã có.", true);
            return true;
        }

        private static void AddButton(RibbonPanelSource panel, string id, string text, string command, string toolTip, bool detail)
        {
            foreach (RibbonItem item in panel.Items) if (item.Id == id) return;
            panel.Items.Add(new RibbonButton
            {
                Id = id, Text = text, ShowText = true, ShowImage = true,
                Size = RibbonItemSize.Large, Orientation = Orientation.Vertical,
                CommandParameter = command, CommandHandler = RibbonCommandHandler.Instance,
                Image = CreateIcon(16, detail), LargeImage = CreateIcon(32, detail), ToolTip = toolTip
            });
        }

        private static ImageSource CreateIcon(int size, bool detail)
        {
            var group = new DrawingGroup();
            group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 32, 32))));
            var blue = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            var cyan = new SolidColorBrush(Color.FromRgb(103, 232, 249));
            var gold = new SolidColorBrush(Color.FromRgb(250, 204, 21));
            group.Children.Add(new GeometryDrawing(blue, null, new RectangleGeometry(new Rect(1, 1, 30, 30), 6, 6)));
            var stairPen = new Pen(Brushes.White, detail ? 2 : 2.5)
                { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            if (detail)
            {
                group.Children.Add(new GeometryDrawing(null, new Pen(cyan, 2), new EllipseGeometry(new Point(13, 13), 9, 9)));
                group.Children.Add(new GeometryDrawing(null, new Pen(gold, 3), new LineGeometry(new Point(20, 20), new Point(27, 27))));
                group.Children.Add(new GeometryDrawing(null, stairPen, MediaGeometry.Parse("M 6,17 L 10,17 10,13 14,13 14,9 18,9")));
            }
            else
            {
                group.Children.Add(new GeometryDrawing(null, stairPen, MediaGeometry.Parse("M 5,26 L 5,20 12,20 12,13 19,13 19,6 27,6")));
                group.Children.Add(new GeometryDrawing(null, new Pen(cyan, 2), new LineGeometry(new Point(5, 16), new Point(23, 3))));
                group.Children.Add(new GeometryDrawing(null, new Pen(gold, 2), new LineGeometry(new Point(6, 28), new Point(28, 28))));
            }
            group.Transform = new ScaleTransform(size / 32.0, size / 32.0);
            var image = new DrawingImage(group);
            image.Freeze();
            return image;
        }

        private sealed class RibbonCommandHandler : ICommand
        {
            public static readonly RibbonCommandHandler Instance = new RibbonCommandHandler();
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true;

            public void Execute(object parameter)
            {
                string command = (parameter as RibbonButton)?.CommandParameter as string ?? parameter as string;
                if (command != CommandNames.Draw && command != CommandNames.Detail) return;
                Document document = AutoCADApplication.DocumentManager.MdiActiveDocument;
                if (document == null) return;
                document.SendStringToExecute("\x1B\x1B", true, false, false);
                document.SendStringToExecute(command + "\n", true, false, false);
            }
        }
    }
}
