using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using StairDrawer.Models;

namespace StairDrawer.UI
{
    public partial class StairDetailWindow : Window
    {
        private bool _ready;
        internal StairDetailOptions Options { get; private set; }
        internal StairDetailWindow(double stoneThickness = 20.0, double sourceScale = 50.0, string nextNumber = "01")
        {
            InitializeComponent();
            NumberText.Text = nextNumber;
            SourceScaleText.Text = sourceScale.ToString("0.##", CultureInfo.CurrentCulture);
            TreadNoteText.Text = "ĐÁ ỐP MẶT BẬC DÀY " + stoneThickness.ToString("0.##") + " MM";
            RiserNoteText.Text = "ĐÁ ỐP CỔ BẬC DÀY " + stoneThickness.ToString("0.##") + " MM";
            _ready = true;
            UpdateScaleHint();
        }

        private void OnKindChanged(object sender, RoutedEventArgs e)
        {
            if (!_ready) return;
            bool nose = NoseRadio.IsChecked == true;
            RadiusText.Text = nose ? "90" : "450";
            DetailScaleText.Text = nose ? "5" : "10";
            TitleText.Text = nose ? "CHI TIẾT MŨI BẬC" : "CHI TIẾT BẬC THANG";
            DimensionsCheck.IsChecked = !nose;
            BrickNoteText.Text = nose ? "" : "BẬC XÂY GẠCH ĐẶC, VỮA XI MĂNG";
            ConcreteNoteText.Text = nose ? "" : "BẢN THANG BTCT ĐỔ TẠI CHỖ";
        }

        private void OnScaleChanged(object sender, TextChangedEventArgs e) { if (_ready) UpdateScaleHint(); }
        private void UpdateScaleHint()
        {
            if (Read(SourceScaleText, out double source) && Read(DetailScaleText, out double target)
                && NumericValidation.IsFinite(source) && NumericValidation.IsFinite(target) && source > 0 && target > 0)
                ScaleHint.Text = "Phóng " + (source / target).ToString("0.##") + " lần; kích thước dim giữ giá trị thực.";
        }

        private void OnAccept(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Read(RadiusText, out double radius) || !Read(SourceScaleText, out double source)
                    || !Read(DetailScaleText, out double target))
                    throw new ArgumentException("Nhập bán kính và tỷ lệ bằng số hợp lệ.");
                Options = new StairDetailOptions(radius, source, target, NumberText.Text, TitleText.Text,
                    DimensionsCheck.IsChecked == true, NotesCheck.IsChecked == true, HideRailingCheck.IsChecked == true,
                    HorizontalCheck.IsChecked == true, LeaderCheck.IsChecked == true, SheetText.Text,
                    TreadNoteText.Text, RiserNoteText.Text, BrickNoteText.Text, ConcreteNoteText.Text);
                DialogResult = true;
            }
            catch (ArgumentException error) { ErrorText.Text = error is ArgumentOutOfRangeException
                ? "Bán kính 20–5000 mm. Tỷ lệ 1–1000; mẫu số chi tiết không lớn hơn mẫu số mặt cắt gốc."
                : error.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0]; }
        }

        private static bool Read(TextBox field, out double value) => double.TryParse(field.Text,
            NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            || double.TryParse(field.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
