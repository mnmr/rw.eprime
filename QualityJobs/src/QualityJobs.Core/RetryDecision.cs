namespace QualityJobs.Core
{
    /// <summary>Retry decision for bills and construction (spec §10). Like
    /// GateDecision, the game side supplies managed=false for a bill whose
    /// Quality Jobs management is off; a stored target quality is inert then.</summary>
    public static class RetryDecision
    {
        public static bool ShouldRetry(bool managed, QualityLevel rolled,
            QualityLevel minimumAcceptable)
            => managed && rolled < minimumAcceptable;
    }
}
