using System;

namespace WorkRoles.Core.Recs
{
    /// Immutable consumed inputs, projected once by each immutable options
    /// instance. Equal option edits outside this domain preserve cache keys.
    public readonly struct SkillPromotionTuning : IEquatable<SkillPromotionTuning>
    {
        private readonly int minimumSignal, strongLevel, strongSignal, greatLevel, greatSignal;

        internal SkillPromotionTuning(RecommendationsTuningOptions options)
        {
            minimumSignal = options.Get(RecommendationTuningOption.OptionalTargetMinimumSignal);
            strongLevel = options.Get(RecommendationTuningOption.OptionalTargetStrongLevel);
            strongSignal = options.Get(RecommendationTuningOption.OptionalTargetStrongPromotedSignal);
            greatLevel = options.Get(RecommendationTuningOption.OptionalTargetGreatLevel);
            greatSignal = options.Get(RecommendationTuningOption.OptionalTargetGreatPromotedSignal);
        }

        public bool Equals(SkillPromotionTuning other) => minimumSignal == other.minimumSignal
            && strongLevel == other.strongLevel && strongSignal == other.strongSignal
            && greatLevel == other.greatLevel && greatSignal == other.greatSignal;
    }

    /// Captions rank only Strong-or-better raw buckets by ChampionSkillScore.
    /// Promotion thresholds and the multipliers for excluded buckets do not
    /// participate. UI metrics and pawn facts belong to the caller's key.
    public readonly struct SkillCaptionTuning : IEquatable<SkillCaptionTuning>
    {
        private readonly int divisor, strong, great, exceptional;

        internal SkillCaptionTuning(RecommendationsTuningOptions options)
        {
            divisor = options.Get(RecommendationTuningOption.ChampionSkillDivisor);
            strong = options.Get(RecommendationTuningOption.ChampionStrongMultiplierQuarterUnits);
            great = options.Get(RecommendationTuningOption.ChampionGreatMultiplierQuarterUnits);
            exceptional = options.Get(RecommendationTuningOption.ChampionExceptionalMultiplierQuarterUnits);
        }

        public bool Equals(SkillCaptionTuning other) => divisor == other.divisor
            && strong == other.strong && great == other.great && exceptional == other.exceptional;
    }
}
