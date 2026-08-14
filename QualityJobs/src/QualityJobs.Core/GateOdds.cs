namespace QualityJobs.Core
{
    /// <summary>
    /// The quality outcome a configured gate implies.
    ///
    /// A bill or construction plan predicts from its own settings, not from
    /// whoever happens to be available: the resume condition names the worker
    /// the gate will admit, and that fixes the distribution. Auto-best mode
    /// moves the gate's skill value to track the colony's best finisher, but
    /// the prediction is still read off the resulting gate — so the same
    /// configuration always yields the same percentage.
    /// </summary>
    public static class GateOdds
    {
        /// <summary>
        /// Quality levels a required production-specialist role adds. Ideology's
        /// RoleEffect_ProductionQualityOffset is one level for the roles that
        /// carry it, which is what the gate can rely on.
        /// </summary>
        public const int SpecialistRoleOffset = 1;

        /// <summary>Probability per QualityLevel (index 0..6) for this gate.</summary>
        public static double[] DistributionFor(in ResumeCondition condition)
            => QualityOdds.Distribution(
                condition.MinSkill,
                condition.RequireInspired,
                condition.RequireSpecialist ? SpecialistRoleOffset : 0);

        /// <summary>
        /// Expected runs to land one result at or above
        /// <paramref name="targetQuality"/> under this gate. One when the gate
        /// carries no target; <see cref="ExpectedAttempts.Max"/> when the target
        /// is unreachable for it.
        /// </summary>
        public static float AttemptsFor(in ResumeCondition condition, int targetQuality)
            => targetQuality <= 0
                ? 1f
                : ExpectedAttempts.For(DistributionFor(condition), targetQuality);
    }
}
