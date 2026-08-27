namespace QualityJobs.Core
{
    /// <summary>Structured AND condition (spec §6): min skill, require inspiration,
    /// require production-specialist role.</summary>
    public readonly struct ResumeCondition
    {
        public readonly int MinSkill;
        public readonly bool RequireInspired;
        public readonly bool RequireSpecialist;

        public ResumeCondition(int minSkill, bool requireInspired, bool requireSpecialist)
        {
            MinSkill = minSkill < 0 ? 0 : (minSkill > 20 ? 20 : minSkill);
            RequireInspired = requireInspired;
            RequireSpecialist = requireSpecialist;
        }

        public bool IsSatisfiedBy(in CandidateFacts facts)
        {
            if (facts.Skill < MinSkill) return false;
            return FiltersSatisfiedBy(facts);
        }

        /// <summary>The non-skill filters only (inspired/specialist). The auto-best
        /// path uses this instead of IsSatisfiedBy because MinSkill is replaced by
        /// the dynamic threshold there (auto spec §2.2) — sharing the filter logic
        /// keeps a future filter field from silently diverging between modes.</summary>
        public bool FiltersSatisfiedBy(in CandidateFacts facts)
        {
            if (RequireInspired && !facts.Inspired) return false;
            if (RequireSpecialist && facts.RoleOffset <= 0) return false;
            return true;
        }

        public bool Equals(in ResumeCondition other)
            => MinSkill == other.MinSkill
               && RequireInspired == other.RequireInspired
               && RequireSpecialist == other.RequireSpecialist;
    }
}
