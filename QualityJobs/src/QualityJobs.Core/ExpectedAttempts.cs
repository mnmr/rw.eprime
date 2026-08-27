namespace QualityJobs.Core
{
    /// <summary>
    /// How many crafting or construction runs a quality target is expected to
    /// cost. Each run is an independent draw from the same quality
    /// distribution, so the number of runs until the first success is
    /// geometric with mean 1/p where p is the chance of landing at or above the
    /// target. Consumers multiply a single run's material cost by this factor.
    ///
    /// Deterministic and game-free: identical inputs give identical results on
    /// every multiplayer client.
    /// </summary>
    public static class ExpectedAttempts
    {
        /// <summary>Ceiling on the returned multiplier. A target the crafter
        /// realistically cannot hit would otherwise produce an unbounded (or
        /// infinite) factor; callers want a large-but-finite estimate.</summary>
        public const float Max = 20f;

        /// <summary>Below this success chance the estimate is capped at
        /// <see cref="Max"/> rather than reported as a huge multiplier.</summary>
        private const double MinSuccessChance = 1.0 / Max;

        private const int TopQuality = 6;

        /// <summary>
        /// Expected runs to produce one result at or above
        /// <paramref name="targetQuality"/>.
        /// </summary>
        /// <param name="distribution">Probability per QualityLevel, index 0..6
        /// (see <see cref="QualityOdds.Distribution"/>). A null or short array
        /// means "unknown" and costs one run.</param>
        /// <param name="targetQuality">QualityLevel as int. Zero or below means
        /// any quality is accepted, which costs exactly one run. Values above
        /// Legendary clamp to Legendary.</param>
        /// <returns>A factor in [1, <see cref="Max"/>].</returns>
        public static float For(double[]? distribution, int targetQuality)
        {
            if (targetQuality <= 0) return 1f;
            if (distribution == null || distribution.Length <= TopQuality) return 1f;
            if (targetQuality > TopQuality) targetQuality = TopQuality;

            double success = 0.0;
            for (int q = targetQuality; q <= TopQuality; q++) success += distribution[q];

            if (success <= MinSuccessChance) return Max;
            float attempts = (float)(1.0 / success);
            return attempts < 1f ? 1f : attempts;
        }
    }
}
