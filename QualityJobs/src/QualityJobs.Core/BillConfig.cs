namespace QualityJobs.Core
{
    /// <summary>Per-bill configuration (spec §11). Equality powers no-op edit detection:
    /// commands must not bump revisions for unchanged values (AGENTS.md).</summary>
    public readonly struct BillConfig
    {
        public readonly bool Managed;
        /// <summary>Auto-best mode (auto spec §2): dynamic colony-wide threshold
        /// replaces MinSkill; inspired/specialist stay active as pool filters.</summary>
        public readonly bool AutoBest;
        public readonly ResumeCondition Condition;

        public BillConfig(bool managed, bool autoBest, ResumeCondition condition)
        {
            Managed = managed;
            AutoBest = autoBest;
            Condition = condition;
        }

        public bool Equals(in BillConfig other)
            => Managed == other.Managed && AutoBest == other.AutoBest
               && Condition.Equals(other.Condition);
    }
}
