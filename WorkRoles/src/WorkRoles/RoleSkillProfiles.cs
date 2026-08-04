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
            => RoleSkillEvidenceSource.ForCoverage(
                giverNames, JobSkillProfiles.GiverFacts(), scratch);
    }
}
