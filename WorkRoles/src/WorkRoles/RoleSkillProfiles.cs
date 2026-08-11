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
        {
            if (role == null) return new List<RoleSkillView>();
            if (!role.composite) return ForCoverage(role.Coverage());
            // A composite unions its members' own profiles (the per-role share
            // filter runs within each member), so a bundle of specialist roles
            // shows every specialist skill. Blocker members contribute nothing
            // unless the composite itself is a blocker, mirroring Coverage().
            var store = RoleStore.Current;
            var profiles = new List<IReadOnlyList<RoleSkillView>>();
            if (store != null)
                foreach (int memberId in role.memberRoleIds)
                {
                    Role member = store.RoleById(memberId);
                    if (member == null || member.composite) continue;
                    if (member.blocker && !role.blocker) continue;
                    profiles.Add(ForCoverage(member.Coverage()));
                }
            return RoleSkillProfile.Merge(profiles);
        }

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
