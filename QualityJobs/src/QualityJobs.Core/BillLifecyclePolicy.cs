namespace QualityJobs.Core
{
    public readonly struct SourceBillCompletion
    {
        public readonly int RepeatCount;
        public readonly bool NotifyCompletion;

        public SourceBillCompletion(int repeatCount, bool notifyCompletion)
        {
            RepeatCount = repeatCount;
            NotifyCompletion = notifyCompletion;
        }
    }

    /// <summary>Deterministic bill lifecycle decisions for finisher completion.</summary>
    public static class BillLifecyclePolicy
    {
        public static bool CanDeleteFinishBill(bool aliasesSource) => !aliasesSource;

        public static bool ShouldRunVanillaCompletion(bool isFinishBill, bool retry)
            => !isFinishBill && !retry;

        public static SourceBillCompletion CompleteSource(int repeatCount,
            bool sourceAvailable, bool repeatCountMode, bool aliasesFinishBill, bool retry)
        {
            if (!sourceAvailable || !repeatCountMode || aliasesFinishBill || retry
                || repeatCount <= 0)
                return new SourceBillCompletion(repeatCount, notifyCompletion: false);

            int next = repeatCount - 1;
            return new SourceBillCompletion(next, notifyCompletion: next == 0);
        }
    }
}
