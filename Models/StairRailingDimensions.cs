using System;

namespace StairDrawer.Models
{
    internal static class StairRailingDimensions
    {
        public const double HandrailWidth = 50.0;
        public const double HandrailEdgeClearance = 50.0;
        public const double TerminalPostWidth = 40.0;
        public const double TurnPostWidth = 20.0;

        public static double GetInset(double stairWidth)
        {
            // The clearance is measured to the handrail face, so its axis includes half the width.
            return Math.Min(HandrailEdgeClearance + (HandrailWidth * 0.5), stairWidth * 0.5);
        }
    }
}
