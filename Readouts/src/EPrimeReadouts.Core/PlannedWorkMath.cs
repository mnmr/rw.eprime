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

    /// Walks one carried material stack across its construction destinations.
    /// Each destination may consume no more than its current outstanding need;
    /// any remainder stays available for the next queued destination.
    public struct CappedMaterialCredit
    {
        private int remaining;

        public CappedMaterialCredit(int carried)
        {
            remaining = carried > 0 ? carried : 0;
        }

        public int Remaining => remaining;

        public int Take(int outstanding)
        {
            if (remaining <= 0 || outstanding <= 0) return 0;
            int taken = remaining < outstanding ? remaining : outstanding;
            remaining -= taken;
            return taken;
        }
    }

    /// Single-pass nearest eligible destination selection. Equal distances
    /// preserve queue order, matching vanilla's global-closest scan.
    public struct ClosestPlannedDestination
    {
        private float distanceSquared;

        public ClosestPlannedDestination()
        {
            Index = -1;
            distanceSquared = float.MaxValue;
        }

        public int Index { get; private set; }

        public void Consider(int index, float distanceSquared, bool eligible)
        {
            if (!eligible || index < 0 || distanceSquared < 0f) return;
            if (Index >= 0 && distanceSquared >= this.distanceSquared) return;
            Index = index;
            this.distanceSquared = distanceSquared;
        }
    }

    /// Planned-work arithmetic: how much material outstanding work still owes.
    /// Deterministic and game-free so every rule here is directly testable.
    public static class PlannedWorkMath
    {
        /// Expected independent attempts until one result is accepted. The
        /// published probability is authoritative; zero means the expected
        /// material demand is unbounded and downstream debt saturates.
        public static float ExpectedAttempts(double probability)
        {
            if (probability >= 1.0) return 1f;
            if (probability <= 0.0) return float.PositiveInfinity;
            double attempts = 1.0 / probability;
            return attempts >= float.MaxValue
                ? float.PositiveInfinity : (float)attempts;
        }

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

        /// Allocates a construction-haul stack the way vanilla commits it:
        /// preserve the primary target's remaining need first, then let the
        /// current destination consume only the excess. The returned credit
        /// carries any further excess into the queued destinations.
        public static CappedMaterialCredit AllocateConstructionHaul(
            int carried,
            int primaryOutstanding,
            int currentOutstanding,
            out int primaryCredit,
            out int currentCredit)
        {
            var credit = new CappedMaterialCredit(carried);
            primaryCredit = credit.Take(primaryOutstanding);
            currentCredit = credit.Take(currentOutstanding);
            return credit;
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
            double netRebuildCost = fullCost * (1.0 - returned);
            if (netRebuildCost <= 0.0) return outstanding;
            return CeilToInt(outstanding + rebuilds * netRebuildCost);
        }

        /// A finished below-target building has already consumed its first
        /// attempt. Every attempt still expected from the published quality
        /// probability is therefore a full deconstruct-and-rebuild cycle.
        public static int FailedBuildableDebt(
            int fullCost,
            float expectedAttempts,
            float returnedFraction)
        {
            if (fullCost <= 0) return 0;

            double attempts = expectedAttempts > 1f ? expectedAttempts : 1f;
            double returned = returnedFraction;
            if (returned < 0.0) returned = 0.0;
            if (returned > 1.0) returned = 1.0;
            double netCost = fullCost * (1.0 - returned);
            if (netCost <= 0.0) return 0;
            return CeilToInt(attempts * netCost);
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
