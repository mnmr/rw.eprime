namespace QualityJobs.Core
{
    /// <summary>Construction retry decision (spec §10; game wiring ships in phase 2).</summary>
    public static class RetryDecision
    {
        public static bool ShouldRetry(QualityLevel rolled, QualityLevel minimumAcceptable)
            => rolled < minimumAcceptable;
    }
}
