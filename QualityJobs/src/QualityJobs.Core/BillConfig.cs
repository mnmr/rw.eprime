namespace QualityJobs.Core
{
    /// <summary>Per-bill configuration (spec §11). Equality powers no-op edit detection:
    /// commands must not bump revisions for unchanged values (AGENTS.md).</summary>
    public readonly struct BillConfig
    {
        public readonly bool Managed;
        public readonly ResumeCondition Condition;

        public BillConfig(bool managed, ResumeCondition condition)
        {
            Managed = managed;
            Condition = condition;
        }

        public bool Equals(in BillConfig other)
            => Managed == other.Managed && Condition.Equals(other.Condition);
    }
}
