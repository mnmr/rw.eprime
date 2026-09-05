using System;
using System.Collections.Generic;
using System.Text;
using Verse;

namespace Implanner
{
    /// Game-side mod-compatibility bridges: the tooltip text for the
    /// curated "Allow multiple ..." options, and the Bionic modularity
    /// "modular replacement" mark a module worker demands on its host
    /// part. Builder path only.
    internal static class ModCompatibility
    {
        // Cache contract:
        // Owner: process.
        // Key: none.
        // Value: the Bionic modularity DefExtension_ModularHediff type, or
        //   null when the mod is not loaded (immutable).
        // Dependencies: the loaded assembly set (static per session).
        // Refresh policy: resolved once on first query.
        // Equality policy: never changes within a session.
        // Teardown: none needed; a process-lifetime immutable fact.
        private static Type? modularExtension;
        private static bool modularExtensionResolved;

        /// Whether a replacement can host Bionic modularity modules. The
        /// mod marks host bionics with DefExtension_ModularHediff (by
        /// patch, or on every replacement under its "all replacements are
        /// modular" setting); with the mod absent every replacement
        /// qualifies, since no other loaded content demands the mark.
        internal static bool IsModularReplacement(HediffDef def)
        {
            if (!modularExtensionResolved)
            {
                modularExtension = GenTypes.GetTypeInAnyAssembly(
                    "BionicModularity.DefExtension_ModularHediff");
                modularExtensionResolved = true;
            }
            if (modularExtension == null) return true;
            List<DefModExtension>? extensions = def.modExtensions;
            if (extensions == null) return false;
            for (int i = 0; i < extensions.Count; i++)
                if (extensions[i] != null && extensions[i].GetType() == modularExtension)
                    return true;
            return false;
        }

        /// One "label (mod name)" line per loaded implant of the group, in
        /// the curated order; a translated placeholder line when none of
        /// the affected content is loaded.
        internal static string TipLines(string[] defNames)
        {
            var lines = new StringBuilder();
            for (int i = 0; i < defNames.Length; i++)
            {
                HediffDef? def = DefDatabase<HediffDef>.GetNamedSilentFail(defNames[i]);
                if (def != null) AppendLine(lines, def);
            }
            return Finish(lines);
        }

        /// One "label (mod name)" line per purchase-only catalog kind, in
        /// catalog (group, label) order.
        internal static string PurchaseOnlyTipLines()
        {
            var lines = new StringBuilder();
            IReadOnlyList<ImplantCatalogEntry> catalog = Catalogs.Implants();
            for (int i = 0; i < catalog.Count; i++)
                if (catalog[i].PurchaseOnly)
                    AppendLine(lines, catalog[i].Def);
            return Finish(lines);
        }

        private static void AppendLine(StringBuilder lines, HediffDef def)
        {
            if (lines.Length > 0) lines.Append('\n');
            lines.Append(def.LabelCap.ToString());
            string? mod = def.modContentPack?.Name;
            if (!string.IsNullOrEmpty(mod))
                lines.Append(" (").Append(mod).Append(')');
        }

        private static string Finish(StringBuilder lines) =>
            lines.Length > 0
                ? lines.ToString()
                : "IMP_OptCompatNoneLoaded".Translate().ToString();
    }
}
