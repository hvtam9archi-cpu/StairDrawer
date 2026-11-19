using System;

namespace StairDrawer.Models
{
    internal struct StairCutSelection
    {
        public StairCutSelection(double? position, bool reverseView)
        {
            if (position.HasValue && (!NumericValidation.IsFinite(position.Value) || position < 0.0 || position > 1.0))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            Position = position;
            ReverseView = reverseView;
        }

        public double? Position { get; }
        public bool ReverseView { get; }
    }
}
