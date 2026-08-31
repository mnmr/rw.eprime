using System;
using System.Collections.Generic;
using Implanner.Core;
using RimWorld;
using Verse;

namespace Implanner
{
    /// Game-side adapter for the Core implant-conflict rules: extracts one
    /// PlannedSlotFacts per catalog slot (reference-body record identity and
    /// ancestry, hediff tags, recipe incompatibility tags) and answers
    /// whether two planned slots can never coexist. Builder path only.
    // Cache contract:
    // Owner: process.
    // Key: implant defName (facts array indexed by slot ordinal).
    // Value: immutable per-slot conflict facts; observed defs and records
    //   are never mutated.
    // Dependencies: the loaded definition set (static per session; labels
    //   are not inputs, so the cache does not follow the language revision).
    // Refresh policy: built lazily per def on first query.
    // Equality policy: entries never change within a session.
    // Teardown: Release clears the map (called from Catalogs.Release).
    internal static class ImplantConflicts
    {
        private static readonly Dictionary<string, PlannedSlotFacts?[]> facts =
            new Dictionary<string, PlannedSlotFacts?[]>(StringComparer.Ordinal);

        /// The resolver injected into PlannerModel.SlotConflictResolver;
        /// cached so injection never allocates.
        internal static readonly Func<ImplantGoal, int, ImplantGoal, int, bool>
            Resolver = (a, ordA, b, ordB) =>
                Conflicts(a.ImplantDefName, ordA, b.ImplantDefName, ordB);

        internal static void Release()
        {
            facts.Clear();
            installedFacts.Clear();
        }

        /// Whether an INSTALLED implant kind excludes installing a planned
        /// kind on the same anatomy instance — the evaluator's substitution
        /// gate: an artificial part occupies its slot (nothing can be
        /// mounted on or swapped under it without destroying it), and
        /// recipe/hediff tag clashes are mutually exclusive; everything else
        /// coexists, so a joywire never satisfies a neurocalculator goal.
        /// Cached static delegate: reached from the reconcile tick path via
        /// PawnProjection and must not allocate per call.
        internal static readonly Func<string, string, bool> SameSlotExclusive =
            static (installedDef, goalDef) =>
            {
                ImplantCatalogEntry? goal = Catalogs.ImplantByDefName(goalDef);
                // Temporarily missing goal content evaluates as blocked
                // anatomy elsewhere; keep the historical permissive answer.
                if (goal == null) return true;
                InstalledFacts installed = InstalledFactsOf(installedDef);
                if (installed.OccupiesPart || goal.IsReplacement) return true;
                List<string>? goalTags = goal.Def.tags;
                return ImplantConflictRules.TagsClash(
                        goal.IncompatibleTags, installed.Tags)
                    || (goalTags != null && ImplantConflictRules.TagsClash(
                        installed.IncompatibleTags, goalTags));
            };

        /// Per-installed-kind exclusivity facts. Installed hediffs may sit
        /// outside the catalog (excluded kinds like joywire, mutant or
        /// modded content), so facts fall back to the HediffDef itself: an
        /// added part occupies the slot, tags come from the def, and an
        /// off-catalog kind contributes no recipe incompatibility tags.
        // Cache contract:
        // Owner: process.
        // Key: installed implant HediffDef name.
        // Value: immutable exclusivity facts.
        // Dependencies: the loaded definition set (static per session;
        //   label-free, so not language-gated).
        // Refresh policy: built lazily per def on first query.
        // Equality policy: entries never change within a session.
        // Teardown: Release clears the map (called from Catalogs.Release).
        private sealed class InstalledFacts
        {
            internal bool OccupiesPart;
            internal IReadOnlyList<string> Tags = NoTags;
            internal IReadOnlyList<string> IncompatibleTags = NoTags;
        }

        private static readonly Dictionary<string, InstalledFacts> installedFacts =
            new Dictionary<string, InstalledFacts>(StringComparer.Ordinal);

        private static InstalledFacts InstalledFactsOf(string defName)
        {
            if (installedFacts.TryGetValue(defName, out InstalledFacts known))
                return known;
            var result = new InstalledFacts();
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(defName);
            if (entry != null)
            {
                result.OccupiesPart = entry.IsReplacement;
                List<string>? tags = entry.Def.tags;
                if (tags != null && tags.Count > 0) result.Tags = tags;
                result.IncompatibleTags = entry.IncompatibleTags;
            }
            else
            {
                HediffDef? def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
                result.OccupiesPart = def?.addedPartProps != null;
                List<string>? tags = def?.tags;
                if (tags != null && tags.Count > 0) result.Tags = tags;
            }
            installedFacts.Add(defName, result);
            return result;
        }

        internal static bool Conflicts(
            string defA, int ordinalA, string defB, int ordinalB)
        {
            PlannedSlotFacts? a = FactsOf(defA, ordinalA);
            PlannedSlotFacts? b = FactsOf(defB, ordinalB);
            if (a == null || b == null) return false;
            return ImplantConflictRules.Conflicts(a, b);
        }

        private static PlannedSlotFacts? FactsOf(string defName, int ordinal)
        {
            if (!facts.TryGetValue(defName, out PlannedSlotFacts?[] slots))
            {
                slots = Build(defName);
                facts.Add(defName, slots);
            }
            return ordinal >= 0 && ordinal < slots.Length ? slots[ordinal] : null;
        }

        private static readonly string[] NoTags = Array.Empty<string>();

        private static PlannedSlotFacts?[] Build(string defName)
        {
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(defName);
            if (entry == null) return Array.Empty<PlannedSlotFacts?>();

            BodyDef body = BodyDefOf.Human;
            List<string>? defTags = entry.Def.tags;
            IReadOnlyList<string> tags = defTags != null && defTags.Count > 0
                ? defTags
                : NoTags;
            var slots = new PlannedSlotFacts?[entry.SlotRecords.Count];
            for (int i = 0; i < entry.SlotRecords.Count; i++)
            {
                BodyPartRecord record = entry.SlotRecords[i];
                if (record == null) continue; // placeholder slot: no anatomy
                var ancestors = new List<int>();
                for (BodyPartRecord parent = record.parent; parent != null;
                    parent = parent.parent)
                    ancestors.Add(body.GetIndexOfPart(parent));
                slots[i] = new PlannedSlotFacts(defName, entry.IsReplacement,
                    body.GetIndexOfPart(record), ancestors,
                    tags, entry.IncompatibleTags);
            }
            return slots;
        }
    }
}
