using System;
using Verse;
using WorkRoles.Core.Signals;

namespace WorkRoles.Signals
{
    internal static class SignalUiFactory
    {
        internal static SignalUiOverride ForDef(
            Def def,
            SignalDefinition definition,
            string iconKey = null,
            string authorTier = null,
            string colorKey = null,
            string description = null)
        {
            if (def == null) return null;
            return new SignalUiOverride(
                label: def.LabelCap,
                description: description ?? def.description,
                iconKey: iconKey,
                authorTier: authorTier,
                colorKey: colorKey,
                sourceDisplayName: SourceDisplayName(def, definition));
        }

        internal static string PackageId(Def def) => def?.modContentPack?.PackageId;

        internal static string SourceDisplayName(Def def, SignalDefinition definition)
        {
            string packageId = PackageId(def);
            if (packageId != null && packageId.StartsWith("ludeon.rimworld", StringComparison.OrdinalIgnoreCase))
                return definition?.FallbackUi.SourceDisplayName ?? "RimWorld";
            return string.IsNullOrWhiteSpace(def?.modContentPack?.Name)
                ? definition?.FallbackUi.SourceDisplayName ?? "Unknown mod"
                : def.modContentPack.Name;
        }
    }
}
