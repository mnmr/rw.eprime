using System;

namespace QualityJobs.Core
{
    /// <summary>
    /// Analytic replica of QualityUtility.GenerateQualityCreatedByPawn(int, bool)
    /// (RimWorld 1.6). Centers, sigmas, the masterwork re-roll rule, the
    /// inspiration +2 shift, and the role offset track the game verbatim for
    /// base-game inputs (roleOffset 0 or 1). The combined shift with a single
    /// [0,6] clamp intentionally deviates from vanilla for negative role offsets:
    /// vanilla applies sequential unclamped-below AddLevels calls, which can
    /// produce out-of-range values; we clamp at Awful instead.
    /// </summary>
    public static class QualityOdds
    {
        // Index = skill level 0..20 (QualityUtility.cs:155-220).
        private static readonly double[] Centers =
        {
            0.7, 1.1, 1.5, 1.8, 2.0, 2.2, 2.4, 2.6, 2.8, 2.95,
            3.1, 3.25, 3.4, 3.5, 3.6, 3.7, 3.8, 3.9, 4.0, 4.1, 4.2,
        };

        private const double SigmaLower = 0.6;
        private const double SigmaUpper = 0.8;
        private const double SigmaUpperReroll = 0.95;

        /// <summary>
        /// Returns the probability distribution over all 7 QualityLevel values
        /// (length 7, indices match QualityLevel enum, sum == 1.0).
        /// </summary>
        /// <param name="skillLevel">Crafter skill 0..20; clamped to range.</param>
        /// <param name="inspired">Whether the pawn has an active Inspired_Creativity inspiration.</param>
        /// <param name="roleOffset">Ideology role quality offset in levels (0 when no role).</param>
        public static double[] Distribution(int skillLevel, bool inspired, int roleOffset)
        {
            if (skillLevel < 0) skillLevel = 0;
            if (skillLevel > 20) skillLevel = 20;
            double center = Centers[skillLevel];

            // P(clamped-roll == k) for k in 0..5, using each upper sigma.
            double[] baseRoll = TruncatedRoll(center, SigmaUpper);
            double[] reRoll = TruncatedRoll(center, SigmaUpperReroll);

            // Vanilla: if first roll == 5, 50% chance to re-roll with wider upper sigma
            // (QualityUtility.cs:223-227). The re-roll replaces the outcome entirely.
            // Net adjustment per bucket k:
            //   +baseRoll[5] * 0.5 * reRoll[k]   (gain from re-roll landing on k)
            //   -baseRoll[5] * 0.5 * (k==5 ? 1 : 0) (remove the half that re-rolls away from 5)
            var afterReroll = new double[6];
            for (int k = 0; k <= 5; k++)
                afterReroll[k] = baseRoll[k] + baseRoll[5] * 0.5 * (reRoll[k] - (k == 5 ? 1.0 : 0.0));

            // Apply inspiration (+2) and role offset, clamped to Legendary (6).
            int shift = (inspired ? 2 : 0) + roleOffset;
            var result = new double[7];
            for (int k = 0; k <= 5; k++)
            {
                int target = k + shift;
                if (target < 0) target = 0;
                if (target > 6) target = 6;
                result[target] += afterReroll[k];
            }
            return result;
        }

        /// <summary>
        /// Computes P((int)GaussianAsymmetric(center, SigmaLower, sigmaUpper) clamped 0..5 == k)
        /// for k in 0..5. The int cast truncates toward zero, so all values below 1.0
        /// (including all negative draws) fall into bucket 0.
        /// </summary>
        private static double[] TruncatedRoll(double center, double sigmaUpper)
        {
            // Bucket 0: everything < 1.0 (covers all negatives + [0,1))
            // Bucket k (1..4): [k, k+1)
            // Bucket 5: everything >= 5.0
            var p = new double[6];
            p[0] = Cdf(1.0, center, sigmaUpper);
            for (int k = 1; k <= 4; k++)
                p[k] = Cdf(k + 1, center, sigmaUpper) - Cdf(k, center, sigmaUpper);
            p[5] = 1.0 - Cdf(5.0, center, sigmaUpper);
            return p;
        }

        /// <summary>
        /// CDF of the asymmetric Gaussian at x. Each half carries total probability 0.5
        /// and is scaled by its own sigma.
        /// </summary>
        private static double Cdf(double x, double center, double sigmaUpper)
        {
            double z = x - center;
            return z <= 0 ? Phi(z / SigmaLower) : Phi(z / sigmaUpper);
        }

        /// <summary>
        /// Standard normal CDF via Abramowitz-Stegun 7.1.26 erf approximation
        /// (max absolute error ~1.5e-7, more than sufficient for display odds).
        /// </summary>
        private static double Phi(double z)
        {
            double x = z / Math.Sqrt(2.0);
            double sign = x < 0 ? -1.0 : 1.0;
            x = Math.Abs(x);
            double t = 1.0 / (1.0 + 0.3275911 * x);
            double y = 1.0 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t
                - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
            return 0.5 * (1.0 + sign * y);
        }
    }
}
