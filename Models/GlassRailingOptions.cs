using System;

namespace StairDrawer.Models
{
    internal enum GlassRailingType { WithPosts, Frameless }
    internal enum GlassMountType { SinglePad, DoublePad, RecessedChannel }

    internal sealed class GlassRailingOptions
    {
        public static readonly GlassRailingOptions Default = new GlassRailingOptions();

        public GlassRailingOptions(GlassRailingType type = GlassRailingType.WithPosts,
            GlassMountType mount = GlassMountType.DoublePad, double thickness = 12, double panelGap = 10)
        {
            if (!Enum.IsDefined(typeof(GlassRailingType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            if (!Enum.IsDefined(typeof(GlassMountType), mount)) throw new ArgumentOutOfRangeException(nameof(mount));
            if (!NumericValidation.IsFinite(thickness) || thickness < 6 || thickness > 40) throw new ArgumentOutOfRangeException(nameof(thickness));
            if (!NumericValidation.IsFinite(panelGap) || panelGap < 5 || panelGap > 50) throw new ArgumentOutOfRangeException(nameof(panelGap));
            Type = type; Mount = mount; Thickness = thickness; PanelGap = panelGap;
        }

        public GlassRailingType Type { get; }
        public GlassMountType Mount { get; }
        public double Thickness { get; }
        public double PanelGap { get; }
        public bool HasPosts => Type == GlassRailingType.WithPosts;
    }
}
