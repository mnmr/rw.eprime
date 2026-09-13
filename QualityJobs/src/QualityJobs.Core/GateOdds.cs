namespace QualityJobs.Core
{
    /// <summary>
    /// The quality outcome a configured gate implies.
    ///
    /// Manual gates predict from their configured minimum requirements.
    /// Auto-best predictions use the selected pawn's actual quality inputs.
    /// </summary>
    public static class GateOdds
    {
        /// <summary>
        /// Quality levels a required production-specialist role adds. Ideology's
        /// RoleEffect_ProductionQualityOffset is one level for the roles that
        /// carry it, which is what the gate can rely on.
        /// </summary>
        public const int SpecialistRoleOffset = 1;

        public static double SuccessChanceFor(in CandidateFacts candidate, int targetQuality)
            => targetQuality <= 0 ? 1.0 : SuccessChance(
                QualityOdds.Distribution(candidate.Skill, candidate.Inspired,
                    candidate.RoleOffset, candidate.QualityBonusMilli), targetQuality);

        /// <summary>Probability per QualityLevel (index 0..6) for this gate.</summary>
        public static double[] DistributionFor(in ResumeCondition condition, int qualityBonusMilli = 0)
            => QualityOdds.Distribution(
                condition.MinSkill,
                condition.RequireInspired,
                condition.RequireSpecialist ? SpecialistRoleOffset : 0, qualityBonusMilli);

        /// <summary>
        /// Probability that one attempt made at this gate lands at or above the
        /// requested quality. A target at or below Awful accepts every result.
        /// Values above Legendary clamp to Legendary.
        /// </summary>
        public static double SuccessChanceFor(in ResumeCondition condition,
            int targetQuality, int qualityBonusMilli = 0)
        {
            if (targetQuality <= 0) return 1.0;
            return SuccessChance(DistributionFor(condition, qualityBonusMilli), targetQuality);
        }

        private static double SuccessChance(double[] distribution, int targetQuality)
        {
            if (targetQuality > 6) targetQuality = 6;
            double chance = 0.0;
            for (int quality = targetQuality; quality < distribution.Length; quality++)
                chance += distribution[quality];
            return chance;
        }

        /// <summary>
        /// Expected runs to land one result at or above
        /// <paramref name="targetQuality"/> under this gate. One when the gate
        /// carries no target; <see cref="ExpectedAttempts.Max"/> when the target
        /// is unreachable for it.
        /// </summary>
        public static float AttemptsFor(in ResumeCondition condition, int targetQuality,
            int qualityBonusMilli = 0)
            => targetQuality <= 0
                ? 1f
                : ExpectedAttempts.For(DistributionFor(condition, qualityBonusMilli), targetQuality);
    }
}
