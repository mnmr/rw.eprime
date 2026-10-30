using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    internal static class CompositeSubstitutionPolicy
    {
        // Shared by the gated catalog, final substitution and exact plan-input
        // equality: inert composites must not consume assignment-state edits.
        internal static bool IsCandidate(RoleView role, IReadOnlyList<RoleView> roles)
        {
            if (role.MemberRoleIds == null || role.MemberRoleIds.Count == 0
                || !role.Enabled || !role.Available || role.HasRules || role.Blocker
                || role.ExcludedFromRecommendations) return false;
            for (int memberIndex = 0; memberIndex < role.MemberRoleIds.Count; memberIndex++)
            {
                RoleView? member = null;
                for (int index = 0; index < roles.Count; index++)
                    if (roles[index].Id == role.MemberRoleIds[memberIndex]) { member = roles[index]; break; }
                if (member == null || member.MemberRoleIds != null || !member.Enabled
                    || member.HasRules || member.Blocker || member.ExcludedFromRecommendations) return false;
            }
            return true;
        }
    }
}
