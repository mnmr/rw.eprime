using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace Implanner
{
    /// Game-side mod-compatibility bridges: the tooltip text for the
    /// curated "Allow multiple ..." options, the Bionic modularity
    /// "modular replacement" mark a module worker demands on its host
    /// part, and the curated part-clearing surgery workers. Builder path
    /// only.
    internal static class ModCompatibility
    {
        // Cache contract:
        // Owner: process/loaded assembly set.
        // Key: Endless Growth's CommonPatches type and GetMaxLevelForBill method.
        // Value: immutable callable, or null when unavailable; never the maximum.
        // Dependencies: loaded assemblies (fixed for the session).
        // Refresh policy: resolve once on the first owned-bill reconciliation.
        // Equality policy: reuse the same delegate for the session.
        // Teardown: process lifetime; retains no world, pawn, bill or Unity asset.
        private static Func<int>? endlessGrowthBillMaximum;
        private static bool endlessGrowthBillMaximumResolved;

        /// Older Implanner bills overwrote the constructor's modded maximum
        /// with 20. Restore only that legacy bound, using the same provider as
        /// Endless Growth's bill constructors. Called only for owned bills in
        /// deterministic reconciliation; no UI preference enters eligibility.
        internal static void RepairBillSkillMaximum(Bill bill)
        {
            if (bill.allowedSkillRange.max != 20) return;
            if (!endlessGrowthBillMaximumResolved)
            {
                Type? type = GenTypes.GetTypeInAnyAssembly(
                    "SlimeSenpai.EndlessGrowth.CommonPatches");
                var method = type?.GetMethod("GetMaxLevelForBill",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null, Type.EmptyTypes, null);
                if (method != null && method.ReturnType == typeof(int))
                    endlessGrowthBillMaximum = (Func<int>)Delegate.CreateDelegate(
                        typeof(Func<int>), method);
                endlessGrowthBillMaximumResolved = true;
            }
            if (endlessGrowthBillMaximum != null)
                bill.allowedSkillRange.max = Math.Max(20, endlessGrowthBillMaximum());
        }

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

        /// Surgery workers known to restore the part (and everything under
        /// it) before adding their hediff, exactly like vanilla's
        /// Recipe_InstallArtificialBodyPart, without deriving from it.
        /// Matched by full type name, no assembly reference. Owner-approved
        /// curated entry (2026-09-26): Vanilla Genetics Expanded's worker
        /// derives straight from Recipe_Surgery, which would otherwise read
        /// as a module worker mounting on artificial parts.
        private static readonly string[] PartClearingWorkers =
        {
            "GeneticRim.Recipe_InstallGeneticBodyPart",
        };

        /// Whether a surgery worker restores the part before installing:
        /// vanilla's artificial-part worker and its derivatives, or a
        /// curated modded equivalent. Definition data only (a worker type
        /// per recipe), so every multiplayer client answers alike.
        internal static bool ClearsPart(Type? worker)
        {
            if (worker == null) return false;
            if (typeof(Recipe_InstallArtificialBodyPart).IsAssignableFrom(worker))
                return true;
            for (Type? walk = worker; walk != null; walk = walk.BaseType)
                for (int i = 0; i < PartClearingWorkers.Length; i++)
                    if (walk.FullName == PartClearingWorkers[i])
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
