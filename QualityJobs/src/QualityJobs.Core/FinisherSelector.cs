using System.Collections.Generic;

namespace QualityJobs.Core
{
    /// <summary>Deterministic finisher ranking (spec §6): inspired first (+2 levels),
    /// then role offset, then skill, tie-broken by lowest id. Same inputs give
    /// the same answer on every MP client.</summary>
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

        private static bool Better(in CandidateFacts a, in CandidateFacts b)
        {
            if (a.Inspired != b.Inspired) return a.Inspired;
            if (a.RoleOffset != b.RoleOffset) return a.RoleOffset > b.RoleOffset;
            if (a.Skill != b.Skill) return a.Skill > b.Skill;
            return a.Id < b.Id;
        }
    }
}
