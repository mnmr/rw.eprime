using System.Collections.Generic;
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
            var scratch = new RoleSkillEvidenceAccumulator();
            var evidence = EvidenceForCoverage(giverNames, scratch);
            return RoleSkillProfile.Build(evidence, scratch.RoleWeight);
        }

        internal static IReadOnlyList<RoleSkillEvidence> EvidenceForCoverage(
            IEnumerable<string> giverNames,
            RoleSkillEvidenceAccumulator scratch)
        {
            if (scratch == null)
                throw new System.ArgumentNullException(nameof(scratch));
            scratch.BeginRole();
            if (giverNames == null) return scratch.CompleteRole();

            foreach (string giverName in giverNames)
            {
                if (!scratch.BeginSource(giverName)) continue;
                var profile = JobSkillProfiles.ForGiver(giverName);
                if (profile == null) continue;
                if (profile.TrainedSkillDefNames.Count > 0)
                    scratch.SetSourceWeight(4);
                for (int i = 0; i < profile.UsedSkillDefNames.Count; i++)
                    scratch.AddUsedSkill(profile.UsedSkillDefNames[i]);
                for (int i = 0; i < profile.TrainedSkillDefNames.Count; i++)
                    scratch.AddTrainedSkill(profile.TrainedSkillDefNames[i]);
                for (int i = 0; i < profile.Requirements.Count; i++)
                {
                    var requirement = profile.Requirements[i];
                    scratch.AddRequiredContent(
                        requirement.SkillDefName, requirement.Gated);
                }
            }
            return scratch.CompleteRole();
        }
    }
}
