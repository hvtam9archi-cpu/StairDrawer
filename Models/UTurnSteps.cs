using System;

namespace StairDrawer.Models
{
    internal struct UTurnSteps
    {
        public UTurnSteps(int middleFlightRisers, int firstWinderRisers, int secondWinderRisers)
        {
            if (middleFlightRisers < 0 || middleFlightRisers == 1)
            {
                throw new ArgumentOutOfRangeException(nameof(middleFlightRisers));
            }

            if (firstWinderRisers < 0 || secondWinderRisers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(firstWinderRisers), "Winder riser counts must be non-negative.");
            }

            MiddleFlightRisers = middleFlightRisers;
            FirstWinderRisers = firstWinderRisers;
            SecondWinderRisers = secondWinderRisers;
        }

        public int MiddleFlightRisers { get; }
        public int FirstWinderRisers { get; }
        public int SecondWinderRisers { get; }
        public long TotalRisers => (long)MiddleFlightRisers + FirstWinderRisers + SecondWinderRisers;
        public bool HasMiddleFlight => MiddleFlightRisers > 0;
        public bool HasSteps => TotalRisers > 0;

        public double GetGap(double flatGap, double treadDepth)
        {
            return HasMiddleFlight ? (MiddleFlightRisers - 1) * treadDepth : flatGap;
        }
    }
}
