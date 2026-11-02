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
    ///
    /// With Endless Growth loaded (SkillRules.EndlessGrowth) the replica
    /// follows that mod's transpiler instead: the center comes from its
    /// skill curve (linear between its points, 0..100), and the roll clamp,
    /// the re-roll trigger and the re-roll clamp all move from Masterwork to
    /// Legendary, so an uninspired roll can land on Legendary directly.
    /// </summary>
    public static class QualityOdds
    {
        // Index = skill level 0..20 (QualityUtility.cs:155-220).
        private static readonly double[] Centers =
        {
            0.7, 1.1, 1.5, 1.8, 2.0, 2.2, 2.4, 2.6, 2.8, 2.95,
            3.1, 3.25, 3.4, 3.5, 3.6, 3.7, 3.8, 3.9, 4.0, 4.1, 4.2,
        };

        // Endless Growth's QualityModifierCurve (HarmonyPatches.cs): the
        // vanilla table up to 20, then its own points. SimpleCurve.Evaluate
        // interpolates linearly and holds the last value beyond it.
        private static readonly double[] EndlessCurveSkills = { 0, 2, 3, 4, 8, 12, 20, 30, 40, 50, 100 };
        private static readonly double[] EndlessCurveCenters = { 0.7, 1.5, 1.8, 2.0, 2.8, 3.4, 4.2, 5.0, 5.7, 6.0, 10.0 };

        private const double SigmaLower = 0.6;
        private const double SigmaUpper = 0.8;
        private const double SigmaUpperReroll = 0.95;

        /// <summary>The roll center Endless Growth's curve gives a skill.</summary>
        public static double EndlessGrowthCenter(int skillLevel)
        {
            if (skillLevel <= 0) return EndlessCurveCenters[0];
            int last = EndlessCurveSkills.Length - 1;
            if (skillLevel >= EndlessCurveSkills[last]) return EndlessCurveCenters[last];
            int i = 1;
            while (EndlessCurveSkills[i] < skillLevel) i++;
            double t = (skillLevel - EndlessCurveSkills[i - 1])
                / (EndlessCurveSkills[i] - EndlessCurveSkills[i - 1]);
            return EndlessCurveCenters[i - 1] + t * (EndlessCurveCenters[i] - EndlessCurveCenters[i - 1]);
        }

        /// <summary>
        /// Returns the probability distribution over all 7 QualityLevel values
        /// (length 7, indices match QualityLevel enum, sum == 1.0).
        /// </summary>
        /// <param name="skillLevel">Crafter skill; clamped to SkillRules.MaxSkill.</param>
        /// <param name="inspired">Whether the pawn has an active Inspired_Creativity inspiration.</param>
        /// <param name="roleOffset">Ideology role quality offset in levels (0 when no role).</param>
        /// <param name="qualityBonusMilli">Post-roll upgrade bonus in thousandths of a level.</param>
        public static double[] Distribution(int skillLevel, bool inspired, int roleOffset,
            int qualityBonusMilli = 0)
        {
            skillLevel = SkillRules.ClampSkill(skillLevel);
            bool endless = SkillRules.EndlessGrowth;
            double center = endless ? EndlessGrowthCenter(skillLevel) : Centers[skillLevel];
            // The highest bucket a raw roll can land in: Masterwork in
            // vanilla, Legendary under Endless Growth.
            int top = endless ? 6 : 5;

            // P(clamped-roll == k) for k in 0..top, using each upper sigma.
            double[] baseRoll = TruncatedRoll(center, SigmaUpper, top);
            double[] reRoll = TruncatedRoll(center, SigmaUpperReroll, top);

            // Vanilla: if first roll == 5, 50% chance to re-roll with wider upper sigma
            // (QualityUtility.cs:223-227). The re-roll replaces the outcome entirely.
            // Net adjustment per bucket k:
            //   +baseRoll[top] * 0.5 * reRoll[k]   (gain from re-roll landing on k)
            //   -baseRoll[top] * 0.5 * (k==top ? 1 : 0) (remove the half that re-rolls away from top)
            var afterReroll = new double[7];
            for (int k = 0; k <= top; k++)
                afterReroll[k] = baseRoll[k] + baseRoll[top] * 0.5 * (reRoll[k] - (k == top ? 1.0 : 0.0));

            // Apply inspiration (+2) and role offset, clamped to Legendary (6).
            int shift = (inspired ? 2 : 0) + roleOffset;
            int bonus = QualityBonus.Normalize(qualityBonusMilli);
            int wholeBonus = bonus / QualityBonus.Scale;
            double extraChance = (double)(bonus % QualityBonus.Scale) / QualityBonus.Scale;
            var result = new double[7];
            for (int k = 0; k <= top; k++)
            {
                int target = k + shift;
                if (target < 0) target = 0;
                if (target > 6) target = 6;
                // VSE applies its bonus after vanilla inspiration/role shifts.
                target = Math.Min(6, target + wholeBonus);
                result[target] += afterReroll[k] * (1.0 - extraChance);
                result[Math.Min(6, target + 1)] += afterReroll[k] * extraChance;
            }
            return result;
        }

        /// <summary>
        /// Computes P((int)GaussianAsymmetric(center, SigmaLower, sigmaUpper) clamped 0..top == k)
        /// for k in 0..top. The int cast truncates toward zero, so all values below 1.0
        /// (including all negative draws) fall into bucket 0.
        /// </summary>
        private static double[] TruncatedRoll(double center, double sigmaUpper, int top)
        {
            // Bucket 0: everything < 1.0 (covers all negatives + [0,1))
            // Bucket k (1..top-1): [k, k+1)
            // Bucket top: everything >= top
            var p = new double[7];
            p[0] = Cdf(1.0, center, sigmaUpper);
            for (int k = 1; k < top; k++)
                p[k] = Cdf(k + 1, center, sigmaUpper) - Cdf(k, center, sigmaUpper);
            p[top] = 1.0 - Cdf(top, center, sigmaUpper);
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
