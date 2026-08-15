using System;

namespace EPrimeReadouts.Core
{
    /// How a bill repeats. Mirrors vanilla's BillRepeatModeDefOf entries; kept
    /// here so the arithmetic stays testable without the game.
    public enum BillRepeat
    {
        /// Runs until the player stops it. Only the next run is reservable —
        /// an unbounded bill would otherwise shave the counter forever.
        Forever = 0,
        /// Runs a fixed number of further times.
        RepeatCount = 1,
        /// Runs until the colony holds a target amount of the product.
        TargetCount = 2,
    }

    /// Planned-work arithmetic: how much material outstanding work still owes.
    /// Deterministic and game-free so every rule here is directly testable.
    public static class PlannedWorkMath
    {
        /// Ceiling on the iterations a single bill may reserve for. A
        /// do-until-you-have bill with a huge target would otherwise zero every
        /// counter it touches on the strength of work that will take seasons.
        public const int MaxIterationsPerBill = 1000;

        /// Iterations the bill still owes.
        /// <paramref name="produced"/> and <paramref name="yieldPerIteration"/>
        /// only matter in <see cref="BillRepeat.TargetCount"/> mode.
        public static int BillIterations(
            BillRepeat mode,
            int repeatCount,
            int targetCount,
            int produced,
            int yieldPerIteration)
        {
            int iterations;
            switch (mode)
            {
                case BillRepeat.Forever:
                    iterations = 1;
                    break;
                case BillRepeat.RepeatCount:
                    iterations = repeatCount;
                    break;
                default:
                    int shortfall = targetCount - produced;
                    if (shortfall <= 0) return 0;
                    int yield = yieldPerIteration > 0 ? yieldPerIteration : 1;
                    // Ceiling division: the run that overshoots still happens.
                    iterations = (shortfall + yield - 1) / yield;
                    break;
            }
            if (iterations <= 0) return 0;
            return iterations > MaxIterationsPerBill
                ? MaxIterationsPerBill : iterations;
        }

        /// Ingredient debt for one bill and one ingredient def.
        /// <paramref name="expectedAttempts"/> is the quality-rework multiplier
        /// (1 when no target quality applies): every attempt consumes a full
        /// set of ingredients, and a below-target craft is kept rather than
        /// recycled, so nothing comes back.
        public static int BillDebt(
            int perIterationCost, int iterations, float expectedAttempts)
        {
            if (perIterationCost <= 0 || iterations <= 0) return 0;
            double attempts = expectedAttempts > 1f ? expectedAttempts : 1f;
            return CeilToInt((double)perIterationCost * iterations * attempts);
        }

        /// The share of one material a deconstruct hands back, mirroring the
        /// per-item rules vanilla applies while building its leavings.
        ///
        /// A building's own fraction is the usual answer, but its definition can
        /// force a material back in full or withhold it entirely — and a zero
        /// fraction means the building leaves nothing at all, which overrides
        /// even a forced material because the whole leavings pass is skipped.
        /// Forcing is tested before withholding, as vanilla does.
        public static float ReturnedFraction(
            float defaultFraction, bool forced, bool blacklisted)
        {
            if (defaultFraction <= 0f) return 0f;
            if (forced) return 1f;
            if (blacklisted) return 0f;
            return defaultFraction > 1f ? 1f : defaultFraction;
        }

        /// Material debt for one blueprint or frame and one material def.
        ///
        /// <paramref name="outstanding"/> is what still has to be hauled to
        /// finish the current attempt. Each further attempt tears the finished
        /// building down and rebuilds it, so it costs a full
        /// <paramref name="fullCost"/> and refunds
        /// <paramref name="returnedFraction"/> of it.
        public static int BuildableDebt(
            int outstanding,
            int fullCost,
            float expectedAttempts,
            float returnedFraction)
        {
            if (outstanding < 0) outstanding = 0;

            double attempts = expectedAttempts > 1f ? expectedAttempts : 1f;
            double rebuilds = attempts - 1.0;
            if (rebuilds <= 0.0 || fullCost <= 0) return outstanding;

            double returned = returnedFraction;
            if (returned < 0.0) returned = 0.0;
            if (returned > 1.0) returned = 1.0;

            return CeilToInt(outstanding + rebuilds * fullCost * (1.0 - returned));
        }

        /// Rounds up, and never below zero. Debts are shortfalls: rounding a
        /// partial unit down would understate what the colony still owes.
        private static int CeilToInt(double value)
        {
            if (value <= 0.0) return 0;
            double ceiling = Math.Ceiling(value - 1e-6);
            if (ceiling < 1.0) return 1;
            return ceiling >= int.MaxValue ? int.MaxValue : (int)ceiling;
        }
    }
}
