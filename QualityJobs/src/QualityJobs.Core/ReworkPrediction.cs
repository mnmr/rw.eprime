namespace QualityJobs.Core
{
    /// <summary>
    /// Whether an expected-attempts prediction applies to a bill at all.
    /// Mirrors the enforcement rules: the retry loop marks below-target
    /// iterations only for managed, target-carrying bills in repeat-count
    /// mode, and a one-shot finish bill never reworks itself — a below-target
    /// roll leaves the debt on the source bill's undecremented repeat count.
    /// </summary>
    public static class ReworkPrediction
    {
        public static bool PredictsRework(bool isFinishBill, bool repeatCountMode,
            bool managed, int targetQuality)
            => !isFinishBill && repeatCountMode && managed && targetQuality > 0;
    }
}
