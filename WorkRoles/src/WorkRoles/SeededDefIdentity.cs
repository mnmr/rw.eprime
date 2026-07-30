using System;
using System.Linq;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Invariant names and references copied from definitions into save data.
    /// Def.label remains client-local presentation only.
    internal static class SeededDefIdentity
    {
        internal static string RoleLabel(RoleDef def) =>
            def == null ? null : !def.seedLabel.NullOrEmpty()
                ? def.seedLabel : InvariantDefName.Humanize(def.defName, "WS_");

        internal static string GroupLabel(RoleGroupDef def) =>
            def == null ? null : InvariantDefName.Humanize(def.defName, "WS_Group");

        internal static string ScaleName(ScaleDef def) =>
            def == null ? null : InvariantDefName.Humanize(def.defName, "WS_Scale");

        internal static string PathName(TrainingPathDef def) =>
            def == null ? null : InvariantDefName.Humanize(def.defName, "WS_Path");

        internal static string WorkTypeRoleLabel(WorkTypeDef def) =>
            def == null ? null : InvariantDefName.Humanize(def.defName);

        internal static string GroupLabel(RoleDef def)
        {
            var groupDef = GroupDef(def);
            return groupDef == null ? def?.group?.Trim() : GroupLabel(groupDef);
        }

        internal static string GroupIdentity(RoleDef def) =>
            GroupDef(def)?.defName ?? def?.group?.Trim();

        internal static string ScaleName(RoleDef def)
        {
            var scaleDef = ScaleDef(def);
            return scaleDef == null ? def?.holderScale?.Trim() : ScaleName(scaleDef);
        }

        internal static string ScaleIdentity(RoleDef def) =>
            ScaleDef(def)?.defName ?? def?.holderScale?.Trim();

        internal static ScaleDef ScaleDef(RoleDef roleDef)
        {
            string name = roleDef?.holderScale?.Trim();
            if (name.NullOrEmpty()) return null;
            return DefDatabase<ScaleDef>.AllDefsListForReading.FirstOrDefault(def =>
                string.Equals(ScaleName(def), name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(def.label, name, StringComparison.OrdinalIgnoreCase));
        }

        private static RoleGroupDef GroupDef(RoleDef roleDef)
        {
            string name = roleDef?.group?.Trim();
            if (name.NullOrEmpty()) return null;
            return DefDatabase<RoleGroupDef>.AllDefsListForReading.FirstOrDefault(def =>
                string.Equals(GroupLabel(def), name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(def.label, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
