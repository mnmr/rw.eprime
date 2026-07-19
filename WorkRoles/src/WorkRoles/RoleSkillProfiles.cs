using System.Collections.Generic;
using System.Linq;
using WorkRoles.Core.Recs;

namespace WorkRoles
{
    /// Derives role-level skill evidence from the role's resolved work givers.
    /// The game/mod definitions are session-static, while role coverage is
    /// cached by Role, so this work happens only while building UI snapshots.
    internal static class RoleSkillProfiles
    {
        internal static List<RoleSkillView> ForRole(Role role)
            => role == null ? new List<RoleSkillView>() : ForCoverage(role.Coverage());

        internal static List<RoleSkillView> ForCoverage(IEnumerable<string> giverNames)
        {
            var evidence = new List<RoleSkillEvidence>();
            foreach (string giverName in giverNames.Distinct())
            {
                var profile = JobSkillProfiles.ForGiver(giverName);
                if (profile == null) continue;
                var used = new HashSet<string>(profile.UsedSkillDefNames);
                var trained = new HashSet<string>(profile.TrainedSkillDefNames);
                var requirements = profile.Requirements
                    .Where(r => !string.IsNullOrEmpty(r.SkillDefName))
                    .GroupBy(r => r.SkillDefName)
                    .ToDictionary(group => group.Key,
                        group => group.Sum(r => System.Math.Max(1, r.Gated)));
                foreach (string skill in used.Union(trained).Union(requirements.Keys))
                    evidence.Add(new RoleSkillEvidence(skill,
                        used.Contains(skill) ? 1 : 0,
                        trained.Contains(skill) ? 1 : 0,
                        requirements.TryGetValue(skill, out int required) ? required : 0));
            }
            return RoleSkillProfile.Build(evidence);
        }
    }
}
