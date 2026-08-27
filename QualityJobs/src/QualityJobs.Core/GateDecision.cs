using System.Collections.Generic;

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

        /// <summary>Auto-best variant (auto spec §2.3): the acting pawn may
        /// complete only when no eligible pool candidate strictly outranks them.
        /// The pool is the colony-wide candidate set built by the game side at
        /// the gate moment — never cached from the sweep.</summary>
        public static GateOutcome DecideAuto(bool billManaged, bool debugCompleted,
            in CandidateFacts worker, IReadOnlyList<CandidateFacts> pool,
            in ResumeCondition condition)
        {
            if (!billManaged || debugCompleted) return GateOutcome.Complete;
            return FinisherSelector.WorkerPassesAutoGate(worker, pool, condition)
                ? GateOutcome.Complete : GateOutcome.Pause;
        }
    }
}
