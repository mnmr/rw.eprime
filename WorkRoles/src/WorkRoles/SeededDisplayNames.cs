using System;
using RimWorld;
using Verse;

namespace WorkRoles
{
    /// Resolves definition translations only for unchanged seeded names. The
    /// persisted names remain invariant and player-authored names remain verbatim.
    internal static class SeededDisplayNames
    {
        internal static string Role(Role role)
        {
            if (role.customName) return role.label;
            RoleDef? def = role.templateDefName.NullOrEmpty() ? null
                : DefDatabase<RoleDef>.GetNamedSilentFail(role.templateDefName);
            if (def == null && !role.generatedWorkTypeDefName.NullOrEmpty())
            {
                WorkTypeDef? workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(
                    role.generatedWorkTypeDefName);
                if (workType != null && string.Equals(role.label,
                    SeededDefIdentity.WorkTypeRoleLabel(workType), StringComparison.Ordinal))
                {
                    string name = workType.labelShort.NullOrEmpty()
                        ? workType.label.NullOrEmpty() ? role.label : workType.label
                        : workType.labelShort;
                    return name.CapitalizeFirst();
                }
            }
            return def != null
                && string.Equals(role.label, SeededDefIdentity.RoleLabel(def), StringComparison.Ordinal)
                && !def.label.NullOrEmpty() ? def.LabelCap.ToString() : role.label;
        }

        internal static string Group(RoleGroup group)
        {
            if (group.id == RoleGroup.DefaultId) return "WR_GroupDefault".Translate();
            RoleGroupDef? def = group.nameSourceDef.NullOrEmpty() ? null
                : DefDatabase<RoleGroupDef>.GetNamedSilentFail(group.nameSourceDef);
            return def != null
                && string.Equals(group.label, SeededDefIdentity.GroupLabel(def), StringComparison.Ordinal)
                && !def.label.NullOrEmpty() ? def.LabelCap.ToString() : group.label;
        }

        internal static string Group(RoleDef roleDef) =>
            SeededDefIdentity.GroupDef(roleDef)?.LabelCap.ToString()
                ?? SeededDefIdentity.GroupLabel(roleDef) ?? "";
    }
}
