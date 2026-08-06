using System.Collections.Generic;

namespace QualityJobs.Core
{
    /// <summary>Deterministic finisher ranking (auto-best spec §2.1/§2.5):
    /// expected-quality rank (FinisherRank), XP tie-break, lowest-id fallback.
    /// Same inputs give the same answer on every MP client.</summary>
    public static class FinisherSelector
    {
        public const int None = -1;

        public static int SelectBest(IReadOnlyList<CandidateFacts> candidates, ResumeCondition condition)
            => Select(candidates, condition, applyCondition: true);

        /// <summary>Best capable pawn ignoring the resume condition (disable restore, spec §12).</summary>
        public static int SelectBestCapable(IReadOnlyList<CandidateFacts> candidates)
            => Select(candidates, default, applyCondition: false);

        private static int Select(IReadOnlyList<CandidateFacts> candidates,
            ResumeCondition condition, bool applyCondition)
        {
            int bestId = None;
            CandidateFacts best = default;
            for (int i = 0; i < candidates.Count; i++)
            {
                CandidateFacts c = candidates[i];
                if (!c.WorkTypeEnabled || !c.MeetsRecipeSkillRequirements) continue;
                if (applyCondition && !condition.IsSatisfiedBy(c)) continue;
                if (bestId == None || Better(c, best))
                {
                    bestId = c.Id;
                    best = c;
                }
            }
            return bestId;
        }

        /// <summary>Deterministic ordering (auto spec §2.1/§2.5): expected-quality
        /// rank, then XP progress, then lowest id.</summary>
        private static bool Better(in CandidateFacts a, in CandidateFacts b)
        {
            if (FinisherRank.Outranks(a, b)) return true;
            if (FinisherRank.Outranks(b, a)) return false;
            return a.Id < b.Id;
        }

        /// <summary>Auto-best pool eligibility (auto spec §2.2): capability flags
        /// plus the condition's inspired/specialist filters. MinSkill is
        /// deliberately ignored — the dynamic threshold replaces it.</summary>
        private static bool AutoEligible(in CandidateFacts c, in ResumeCondition condition)
        {
            if (!c.WorkTypeEnabled || !c.MeetsRecipeSkillRequirements) return false;
            return condition.FiltersSatisfiedBy(c);
        }

        /// <summary>Auto-best gate (auto spec §2.3): the worker passes iff eligible
        /// and no eligible pool candidate strictly outranks them on
        /// (RankMilli, XpMilli). Exact ties admit every tied pawn. The worker
        /// need not be a pool member (spec §2.2, owner-approved): a mech worker
        /// is gated against the colonist pool, and an eligible worker with no
        /// eligible pool competitor completes — it IS the best available.</summary>
        public static bool WorkerPassesAutoGate(in CandidateFacts worker,
            IReadOnlyList<CandidateFacts> pool, in ResumeCondition condition)
        {
            if (!AutoEligible(worker, condition)) return false;
            for (int i = 0; i < pool.Count; i++)
            {
                CandidateFacts c = pool[i];
                if (c.Id == worker.Id) continue;
                if (!AutoEligible(c, condition)) continue;
                if (FinisherRank.Outranks(c, worker)) return false;
            }
            return true;
        }

        /// <summary>Best dispatchable candidate that passes the auto gate against
        /// the full colony pool (auto spec §2.4). None when the colony-wide best
        /// is not dispatchable — the item waits. By construction the returned
        /// pawn always passes the gate (dispatch/gate agreement invariant).</summary>
        public static int SelectAutoBest(IReadOnlyList<CandidateFacts> dispatchable,
            IReadOnlyList<CandidateFacts> pool, in ResumeCondition condition)
        {
            int bestId = None;
            CandidateFacts best = default;
            for (int i = 0; i < dispatchable.Count; i++)
            {
                CandidateFacts c = dispatchable[i];
                if (!WorkerPassesAutoGate(c, pool, condition)) continue;
                // All gate-passers are exactly tied on (rank, xp), so Better
                // reduces to the lowest-id fallback — the §2.4 deterministic pick.
                if (bestId == None || Better(c, best))
                {
                    bestId = c.Id;
                    best = c;
                }
            }
            return bestId;
        }

        /// <summary>Top-ranked eligible pool member regardless of availability
        /// (auto spec §5: current-best display).</summary>
        public static int SelectBestOfPool(IReadOnlyList<CandidateFacts> pool,
            in ResumeCondition condition)
        {
            int bestId = None;
            CandidateFacts best = default;
            for (int i = 0; i < pool.Count; i++)
            {
                CandidateFacts c = pool[i];
                if (!AutoEligible(c, condition)) continue;
                if (bestId == None || Better(c, best))
                {
                    bestId = c.Id;
                    best = c;
                }
            }
            return bestId;
        }
    }
}
