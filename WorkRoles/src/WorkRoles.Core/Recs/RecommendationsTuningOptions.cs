using System;

namespace WorkRoles.Core.Recs
{
    public enum RecommendationTuningOption
    {
        ChampionSkillDivisor,
        ChampionGreatMultiplierHalfUnits,
        SurplusMinimumSignal,
        OrderingGreatSignalPoints,
    }

    /// <summary>
    /// Immutable, deterministic inputs to recommendation formulas. A changed
    /// value publishes a new snapshot; a normalized no-op preserves identity.
    /// </summary>
    public sealed class RecommendationsTuningOptions
    {
        private static readonly int[] Defaults = { 2, 4, 2, 3 };
        private static readonly int[] Minimums = { 1, 0, 0, -20 };
        private static readonly int[] Maximums = { 20, 20, 4, 20 };

        private readonly int[] values;

        private RecommendationsTuningOptions(int[] values)
        {
            this.values = values;
        }

        public static RecommendationsTuningOptions Default { get; } =
            new RecommendationsTuningOptions((int[])Defaults.Clone());

        public int Get(RecommendationTuningOption option)
        {
            int index = (int)option;
            if (index < 0 || index >= values.Length)
                throw new ArgumentOutOfRangeException(nameof(option));
            return values[index];
        }

        public RecommendationsTuningOptions With(
            RecommendationTuningOption option,
            int value)
        {
            int index = (int)option;
            if (index < 0 || index >= values.Length)
                throw new ArgumentOutOfRangeException(nameof(option));
            int normalized = Math.Max(
                Minimums[index], Math.Min(Maximums[index], value));
            if (values[index] == normalized) return this;
            var changed = (int[])values.Clone();
            changed[index] = normalized;
            return new RecommendationsTuningOptions(changed);
        }
    }

    internal sealed class RecommendationFormulaEngine
    {
        private readonly RecommendationsTuningOptions options;

        internal RecommendationFormulaEngine(
            RecommendationsTuningOptions options)
        {
            this.options = options
                ?? throw new ArgumentNullException(nameof(options));
        }

        internal SignalBucket SurplusMinimumSignal => (SignalBucket)options.Get(
            RecommendationTuningOption.SurplusMinimumSignal);

        internal int ChampionSkillScore(int level, SignalBucket verdict)
        {
            int divisor = options.Get(
                RecommendationTuningOption.ChampionSkillDivisor);
            int roundedSkill = (Math.Max(0, level) + divisor - 1) / divisor;
            int multiplier;
            switch (verdict)
            {
                case SignalBucket.Exceptional: multiplier = 5; break;
                case SignalBucket.Great:
                    multiplier = options.Get(
                        RecommendationTuningOption
                            .ChampionGreatMultiplierHalfUnits);
                    break;
                case SignalBucket.Strong: multiplier = 3; break;
                case SignalBucket.Neutral: multiplier = 2; break;
                case SignalBucket.Poor: multiplier = 1; break;
                default: multiplier = 0; break;
            }
            return roundedSkill * multiplier;
        }

        internal int OrderingScore(
            SignalBucket verdict,
            int skillLevel,
            byte minimumBonus)
        {
            int signal;
            switch (verdict)
            {
                case SignalBucket.Exceptional: signal = 5; break;
                case SignalBucket.Great:
                    signal = options.Get(
                        RecommendationTuningOption.OrderingGreatSignalPoints);
                    break;
                case SignalBucket.Strong: signal = 1; break;
                case SignalBucket.Neutral: signal = -3; break;
                default: signal = -5; break;
            }
            int skill = (Math.Max(0, skillLevel) + 4) / 5;
            return signal + skill + minimumBonus;
        }
    }
}
