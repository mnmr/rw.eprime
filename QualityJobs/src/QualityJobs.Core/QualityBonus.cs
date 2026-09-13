using System;

namespace QualityJobs.Core
{
    /// <summary>Deterministic post-roll quality bonus. One thousand units add
    /// one quality level; any remainder is the chance of a further level.
    /// Six guaranteed levels already saturate every outcome at Legendary.</summary>
    public static class QualityBonus
    {
        public const int Scale = 1000;
        public const int Maximum = 6 * Scale;

        public static int Normalize(int milli)
            => milli < 0 ? 0 : (milli > Maximum ? Maximum : milli);

        /// <summary>Quantize the final pawn stat to 0.1 percentage points once
        /// at the game/Core boundary. No randomness is consumed by prediction.</summary>
        public static int FromStat(float value)
        {
            if (!(value > 0f)) return 0; // Negative and NaN also mean no bonus.
            if (value >= 6f) return Maximum;
            int whole = (int)value;
            int remainder = (int)Math.Round(((double)value - whole) * Scale,
                MidpointRounding.AwayFromZero);
            // Rounding a fractional chance must not make an upgrade guaranteed.
            return whole * Scale + Math.Min(remainder, Scale - 1);
        }
    }
}
