namespace QualityJobs.Core
{
    public enum ManagedBillRepeat
    {
        Unsupported = 0,
        Forever = 1,
        RepeatCount = 2,
        TargetCount = 3,
    }

    public static class ManagedBillWorkload
    {
        public static int Iterations(ManagedBillRepeat mode, int repeatCount,
            int targetCount, int currentCount, int yieldPerIteration)
        {
            if (mode == ManagedBillRepeat.Forever) return 1;
            if (mode == ManagedBillRepeat.RepeatCount)
                return repeatCount > 0 ? repeatCount : 0;
            if (mode != ManagedBillRepeat.TargetCount) return 0;

            int shortfall = targetCount - currentCount;
            if (shortfall <= 0) return 0;
            if (yieldPerIteration < 1) yieldPerIteration = 1;
            return (shortfall + yieldPerIteration - 1) / yieldPerIteration;
        }
    }

    /// <summary>
    /// Immutable bill counter content published by the managed-jobs snapshot.
    /// Mode remains part of identity even when two modes currently imply the
    /// same number of accepted iterations.
    /// </summary>
    public readonly struct ManagedBillCounter
    {
        public ManagedBillCounter(ManagedBillRepeat mode,
            int remainingAcceptedIterations)
        {
            Mode = mode;
            RemainingAcceptedIterations = remainingAcceptedIterations;
        }

        public ManagedBillRepeat Mode { get; }
        public int RemainingAcceptedIterations { get; }

        public bool HasSameContent(in ManagedBillCounter other)
            => Mode == other.Mode
               && RemainingAcceptedIterations == other.RemainingAcceptedIterations;
    }

    public static class ManagedJobPolicy
    {
        public static bool IncludeBill(bool managed, bool suspended, bool paused,
            bool deleted, bool finishBill)
            => managed && !suspended && !paused && !deleted && !finishBill;

        public static bool IncludeConstruction(bool forbidden, bool destroyed)
            => !forbidden && !destroyed;
    }
}
