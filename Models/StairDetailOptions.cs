using System;

namespace StairDrawer.Models
{
    internal sealed class StairDetailOptions
    {
        public StairDetailOptions(double radius, double sourceScale, double detailScale, string number,
            string title, bool dimensions = true, bool notes = true, bool hideRailing = true,
            bool horizontal = true, bool connectingLeader = false, string sheet = "",
            string treadNote = "ĐÁ ỐP MẶT BẬC DÀY 20 MM", string riserNote = "ĐÁ ỐP CỔ BẬC DÀY 20 MM",
            string brickNote = "BẬC XÂY GẠCH ĐẶC, VỮA XI MĂNG", string concreteNote = "BẢN THANG BTCT ĐỔ TẠI CHỖ")
        {
            if (!NumericValidation.IsFinite(radius) || radius < 20 || radius > 5000) throw new ArgumentOutOfRangeException(nameof(radius));
            if (!NumericValidation.IsFinite(sourceScale) || sourceScale < 1 || sourceScale > 1000) throw new ArgumentOutOfRangeException(nameof(sourceScale));
            if (!NumericValidation.IsFinite(detailScale) || detailScale < 1 || detailScale > sourceScale) throw new ArgumentOutOfRangeException(nameof(detailScale));
            if (string.IsNullOrWhiteSpace(number) || number.Trim().Length > 8) throw new ArgumentException("Nhập số chi tiết (tối đa 8 ký tự).", nameof(number));
            if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 100) throw new ArgumentException("Nhập tên chi tiết (tối đa 100 ký tự).", nameof(title));
            Radius = radius; SourceScale = sourceScale; DetailScale = detailScale;
            Number = number.Trim(); Title = title.Trim(); Dimensions = dimensions; Notes = notes;
            HideRailing = hideRailing; Horizontal = horizontal; ConnectingLeader = connectingLeader;
            Sheet = (sheet ?? "").Trim(); TreadNote = treadNote ?? ""; RiserNote = riserNote ?? "";
            BrickNote = brickNote ?? ""; ConcreteNote = concreteNote ?? "";
        }

        public double Radius { get; }
        public double SourceScale { get; }
        public double DetailScale { get; }
        public double Magnification => SourceScale / DetailScale;
        public string Number { get; }
        public string Title { get; }
        public bool Dimensions { get; }
        public bool Notes { get; }
        public bool HideRailing { get; }
        public bool Horizontal { get; }
        public bool ConnectingLeader { get; }
        public string Sheet { get; }
        public string TreadNote { get; }
        public string RiserNote { get; }
        public string BrickNote { get; }
        public string ConcreteNote { get; }
    }
}
