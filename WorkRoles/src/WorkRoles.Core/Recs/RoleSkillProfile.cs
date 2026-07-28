using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    public readonly struct RoleSkillEvidence
    {
        public RoleSkillEvidence(string skillDefName, int usedJobs,
            int trainedJobs, int requiredContent, int weightedJobs = -1)
        {
            SkillDefName = skillDefName;
            UsedJobs = usedJobs;
            TrainedJobs = trainedJobs;
            RequiredContent = requiredContent;
            WeightedJobs = weightedJobs < 0 ? usedJobs : weightedJobs;
        }

        public string SkillDefName { get; }
        public int UsedJobs { get; }
        public int TrainedJobs { get; }
        public int RequiredContent { get; }
        public int WeightedJobs { get; }
    }

    /// Converts game-derived job evidence into the stable role skill profile
    /// consumed by recommendation and training-path rules.
    public static class RoleSkillProfile
    {
        /// roleWeight > 0 enables share filtering: a skill participates only
        /// when its weighted job share reaches half the role (XP-training jobs
        /// weigh 4x, so sustained work dominates one-off uses like Finish Off).
        public static List<RoleSkillView> Build(IEnumerable<RoleSkillEvidence> evidence,
            int roleWeight = 0)
        {
            var result = evidence
                .Where(e => !string.IsNullOrEmpty(e.SkillDefName))
                .Where(e => roleWeight <= 0 || e.WeightedJobs * 2 >= roleWeight)
                .GroupBy(e => e.SkillDefName)
                .Select(group => new RoleSkillView
                {
                    SkillDefName = group.Key,
                    UsedJobs = group.Sum(e => System.Math.Max(0, e.UsedJobs)),
                    TrainedJobs = group.Sum(e => System.Math.Max(0, e.TrainedJobs)),
                    RequiredContent = group.Sum(e => System.Math.Max(0, e.RequiredContent)),
                })
                .ToList();
            foreach (var skill in result)
                skill.Importance = skill.UsedJobs + skill.TrainedJobs * 2
                    + skill.RequiredContent;
            var primary = result
                .OrderByDescending(s => s.Importance)
                .ThenByDescending(s => s.TrainedJobs)
                .ThenBy(s => s.SkillDefName, System.StringComparer.Ordinal)
                .FirstOrDefault();
            if (primary != null) primary.Primary = true;
            return result
                .OrderByDescending(s => s.Primary)
                .ThenByDescending(s => s.Importance)
                .ThenBy(s => s.SkillDefName, System.StringComparer.Ordinal)
                .ToList();
        }
    }
}
