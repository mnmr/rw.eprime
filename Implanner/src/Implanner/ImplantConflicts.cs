using System;
using System.Collections.Generic;
using Implanner.Core;
using RimWorld;
using Verse;

namespace Implanner
{
    /// Game-side adapter for the Core implant-conflict rules: extracts
    /// PlannedSlotFacts per catalog slot and plannable body (that body's
    /// record identity and ancestry, hediff tags, recipe incompatibility
    /// tags, hediff removal tags) and answers whether two planned slots can
    /// never coexist on any body carrying both. Builder path only.
    // Cache contract:
    // Owner: process.
    // Key: implant defName (facts indexed by catalog slot, then by
    //   Catalogs.PlannableBodies position).
    // Value: immutable per-slot, per-body conflict facts (null where the
    //   body lacks the slot); observed defs and records are never mutated.
    // Dependencies: the loaded definition set (static per session; labels
    //   are not inputs, so the cache does not follow the language revision).
    // Refresh policy: built lazily per def on first query.
    // Equality policy: entries never change within a session.
    // Teardown: Release clears the map (called from Catalogs.Release).
    internal static class ImplantConflicts
    {
        private static readonly Dictionary<string, PlannedSlotFacts?[][]> facts =
            new Dictionary<string, PlannedSlotFacts?[][]>(StringComparer.Ordinal);

        /// The resolver injected into PlannerModel.SlotConflictResolver;
        /// cached so injection never allocates.
        internal static readonly Func<ImplantGoal, PartAddress, ImplantGoal, PartAddress, bool>
            Resolver = (a, slotA, b, slotB) =>
                Conflicts(a.ImplantDefName, slotA, b.ImplantDefName, slotB);

        internal static void Release()
        {
            facts.Clear();
            installedFacts.Clear();
        }

        /// Whether an INSTALLED implant kind excludes installing a planned
        /// kind on the same anatomy instance — the evaluator's substitution
        /// gate (ImplantConflictRules.CompeteForSlot): an artificial part
        /// occupies its slot (nothing can be mounted on or swapped under it
        /// without destroying it, except a module whose worker mounts on
        /// artificial parts: that goal stays installable, so the bionic
        /// never stands in for it), two part-clearing installs push each
        /// other out, and recipe/hediff tag clashes are mutually exclusive;
        /// everything else coexists, so a joywire never satisfies a
        /// neurocalculator goal and neuron reinforcement (a part wiper that
        /// leaves the brain natural) never satisfies a brain implant goal.
        /// Cached static delegate: reached from the reconcile tick path via
        /// PawnProjection and must not allocate per call.
        internal static readonly Func<string, string, bool> SameSlotExclusive =
            static (installedDef, goalDef) =>
            {
                ImplantCatalogEntry? goal = Catalogs.ImplantByDefName(goalDef);
                // Temporarily missing goal content evaluates as blocked
                // anatomy elsewhere; keep the historical permissive answer.
                if (goal == null) return true;
                // The goal upgrades the installed kind in place (or a kind
                // below it in the chain): still installable there, so the
                // base never stands in for its upgrade.
                if (goal.UpgradesFrom != null
                    && !string.Equals(installedDef, goalDef, StringComparison.Ordinal)
                    && Catalogs.UpgradeChainContains(goal, installedDef))
                    return false;
                InstalledFacts installed = InstalledFactsOf(installedDef);
                if (ImplantConflictRules.CompeteForSlot(installed.OccupiesPart,
                        installed.WipesPart, installed.MountsOnArtificialParts,
                        goal.IsReplacement, goal.WipesPart, goal.MountsOnArtificialParts))
                    return true;
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
        /// off-catalog kind contributes no recipe incompatibility tags and
        /// counts as neither a part wiper nor a module.
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
            internal bool WipesPart;
            internal bool MountsOnArtificialParts;
            internal IReadOnlyList<string> Tags = NoTags;
            internal IReadOnlyList<string> IncompatibleTags = NoTags;
        }

        private static readonly Dictionary<string, InstalledFacts> installedFacts =
            new Dictionary<string, InstalledFacts>(StringComparer.Ordinal);

        private static InstalledFacts InstalledFactsOf(string defName)
        {
            if (installedFacts.TryGetValue(defName, out InstalledFacts? known))
                return known;
            var result = new InstalledFacts();
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(defName);
            if (entry != null)
            {
                result.OccupiesPart = entry.IsReplacement;
                result.WipesPart = entry.WipesPart;
                result.MountsOnArtificialParts = entry.MountsOnArtificialParts;
                List<string>? tags = entry.Def.tags;
                if (tags != null && tags.Count > 0) result.Tags = tags;
                result.IncompatibleTags = entry.IncompatibleTags;
            }
            else
            {
                HediffDef? def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
                result.OccupiesPart = def != null
                    && typeof(Hediff_AddedPart).IsAssignableFrom(def.hediffClass);
                List<string>? tags = def?.tags;
                if (tags != null && tags.Count > 0) result.Tags = tags;
            }
            installedFacts.Add(defName, result);
            return result;
        }

        /// Whether the two planned slots can never coexist on a colonist:
        /// on some plannable body carrying both, the Core rules exclude the
        /// pair. Slots no single body carries (a human's eye and an
        /// android's sight sensor) never meet on one pawn.
        internal static bool Conflicts(
            string defA, PartAddress slotA, string defB, PartAddress slotB)
        {
            PlannedSlotFacts?[]? a = FactsOf(defA, slotA);
            PlannedSlotFacts?[]? b = FactsOf(defB, slotB);
            if (a == null || b == null) return false;
            for (int body = 0; body < a.Length; body++)
                if (a[body] != null && b[body] != null
                    && ImplantConflictRules.Conflicts(a[body]!, b[body]!))
                    return true;
            return false;
        }

        private static PlannedSlotFacts?[]? FactsOf(string defName, PartAddress slot)
        {
            if (!facts.TryGetValue(defName, out PlannedSlotFacts?[][]? slots))
            {
                slots = Build(defName);
                facts.Add(defName, slots);
            }
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(defName);
            int index = entry?.SlotIndexOf(slot) ?? -1;
            return index >= 0 && index < slots.Length ? slots[index] : null;
        }

        private static readonly string[] NoTags = Array.Empty<string>();

        private static PlannedSlotFacts?[][] Build(string defName)
        {
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(defName);
            if (entry == null) return Array.Empty<PlannedSlotFacts?[]>();

            List<BodyDef> bodies = Catalogs.PlannableBodies();
            List<string>? defTags = entry.Def.tags;
            IReadOnlyList<string> tags = defTags != null && defTags.Count > 0
                ? defTags
                : NoTags;
            List<string>? defRemoveWith = entry.Def.removeWithTags;
            IReadOnlyList<string> removeWith =
                defRemoveWith != null && defRemoveWith.Count > 0
                    ? defRemoveWith
                    : NoTags;
            var slots = new PlannedSlotFacts?[entry.Slots.Count][];
            for (int i = 0; i < entry.Slots.Count; i++)
            {
                CatalogSlot slot = entry.Slots[i];
                slots[i] = new PlannedSlotFacts?[bodies.Count];
                for (int b = 0; b < bodies.Count; b++)
                {
                    BodyDef body = bodies[b];
                    List<BodyPartRecord> records = body.GetPartsWithDef(slot.Part);
                    if (slot.Address.Occurrence >= records.Count) continue;
                    BodyPartRecord record = records[slot.Address.Occurrence];
                    var ancestors = new List<int>();
                    for (BodyPartRecord parent = record.parent; parent != null;
                        parent = parent.parent)
                        ancestors.Add(body.GetIndexOfPart(parent));
                    slots[i][b] = new PlannedSlotFacts(defName, entry.IsReplacement,
                        body.GetIndexOfPart(record), ancestors,
                        tags, entry.IncompatibleTags, removeWith,
                        entry.MountsOnArtificialParts, entry.WipesPart);
                }
            }
            return slots;
        }
    }
}
