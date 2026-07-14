using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Pure math over per-level stat curves (a value per skill level 0..20).
    public static class JobSkillMath
    {
        /// First level whose value meets or exceeds target; -1 when never reached.
        /// For success/yield factors, target 1.0 finds "full effectiveness".
        public static int LevelReaching(IReadOnlyList<float> valuesPerLevel, float target)
        {
            for (int i = 0; i < valuesPerLevel.Count; i++)
                if (valuesPerLevel[i] >= target)
                    return i;
            return -1;
        }

        /// First level attaining the curve's minimum — where a bad stat
        /// (failure/poison chance) bottoms out.
        public static int LevelOfMinimum(IReadOnlyList<float> valuesPerLevel)
        {
            int best = 0;
            for (int i = 1; i < valuesPerLevel.Count; i++)
                if (valuesPerLevel[i] < valuesPerLevel[best])
                    best = i;
            return best;
        }

        /// Milestones along a rising curve (success/yield): the level-0 baseline,
        /// then the first level reaching each target. Targets already met at an
        /// earlier milestone add nothing.
        public static List<(int level, float value)> RisingMilestones(
            IReadOnlyList<float> valuesPerLevel, IReadOnlyList<float> targets)
        {
            var milestones = new List<(int level, float value)> { (0, valuesPerLevel[0]) };
            foreach (var target in targets)
            {
                int level = LevelReaching(valuesPerLevel, target);
                if (level > milestones[milestones.Count - 1].level)
                    milestones.Add((level, valuesPerLevel[level]));
            }
            return milestones;
        }

        /// Milestones along a falling curve (failure chance): the level-0
        /// baseline, the first levels dropping to each fraction of the start,
        /// and the level where the curve bottoms out.
        public static List<(int level, float value)> FallingMilestones(
            IReadOnlyList<float> valuesPerLevel, IReadOnlyList<float> fractionsOfStart)
        {
            var milestones = new List<(int level, float value)> { (0, valuesPerLevel[0]) };
            foreach (var fraction in fractionsOfStart)
            {
                float target = valuesPerLevel[0] * fraction;
                for (int i = 0; i < valuesPerLevel.Count; i++)
                    if (valuesPerLevel[i] <= target)
                    {
                        if (i > milestones[milestones.Count - 1].level)
                            milestones.Add((i, valuesPerLevel[i]));
                        break;
                    }
            }
            int bottom = LevelOfMinimum(valuesPerLevel);
            if (bottom > milestones[milestones.Count - 1].level)
                milestones.Add((bottom, valuesPerLevel[bottom]));
            return milestones;
        }
    }
}
