namespace QualityJobs.Core
{
    public enum GateOutcome
    {
        Complete,
        Pause,
    }

    /// <summary>The sole enforcement point (spec §5): decides at zero-work whether the
    /// acting pawn may create the product. The game-side patch is responsible
    /// for supplying billManaged=false when the store is absent/disabled, the
    /// recipe is unmanaged, or there is no UFT.</summary>
    public static class GateDecision
    {
        public static GateOutcome Decide(bool billManaged, bool debugCompleted,
            in CandidateFacts worker, in ResumeCondition condition)
        {
            if (!billManaged || debugCompleted) return GateOutcome.Complete;
            return condition.IsSatisfiedBy(worker) ? GateOutcome.Complete : GateOutcome.Pause;
        }
    }
}
