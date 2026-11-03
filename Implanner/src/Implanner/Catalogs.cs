using System;
using System.Collections.Generic;
using Implanner.Core;
using RimShared.UiLib;
using RimWorld;
using Verse;

namespace Implanner
{
    /// The anatomy region an implant targets, for the plan editor's picker
    /// segments. Regions cluster the slots that can block each other: a
    /// stomach choice competes inside Torso, a knee spike and a bionic leg
    /// inside Limbs; brain implants live under Head with the rest of the
    /// skull.
    internal enum ImplantRegion
    {
        Limbs = 0,
        Torso = 1,
        Head = 2,
    }

    /// One plannable anatomy instance of a catalog implant: its part
    /// address, and the first plannable body carrying it (Catalogs
    /// .PlannableBodies order) with that body's record and label ("left
    /// eye", "right sight sensor"). Observed defs and records, never
    /// mutated.
    internal sealed class CatalogSlot
    {
        internal CatalogSlot(PartAddress address, BodyPartDef part, BodyDef body,
            BodyPartRecord record)
        {
            Address = address;
            Part = part;
            Body = body;
            Record = record;
            Label = record.Label;
        }

        internal PartAddress Address { get; }
        internal BodyPartDef Part { get; }
        internal BodyDef Body { get; }
        internal BodyPartRecord Record { get; }
        internal string Label { get; }
    }

    /// One selectable implant kind: a hediff added by a surgery recipe,
    /// with the anatomy it can target.
    internal sealed class ImplantCatalogEntry
    {
        internal ImplantCatalogEntry(HediffDef def, string label, float efficiency,
            List<BodyPartDef> fixedParts, string groupLabel, List<CatalogSlot> slots,
            List<RecipeDef> surgeryRecipes,
            bool isReplacement, ImplantRegion region, LimbKind limb,
            List<string> incompatibleTags, bool mountsOnArtificialParts,
            bool requiresArtificialPart, string? upgradesFrom, bool purchaseOnly,
            bool wipesPart, bool mountsUnderArtificialParts,
            List<string> requiredMutants, List<string> blacklistedMutants,
            ThingDef? item)
        {
            Item = item;
            RequiredMutants = requiredMutants;
            BlacklistedMutants = blacklistedMutants;
            WipesPart = wipesPart;
            MountsUnderArtificialParts = mountsUnderArtificialParts;
            RequiresArtificialPart = requiresArtificialPart;
            UpgradesFrom = upgradesFrom;
            PurchaseOnly = purchaseOnly;
            Def = def;
            Label = label;
            Efficiency = efficiency;
            FixedParts = fixedParts;
            GroupLabel = groupLabel;
            Slots = slots;
            SurgeryRecipes = surgeryRecipes;
            IsReplacement = isReplacement;
            Region = region;
            Limb = limb;
            IncompatibleTags = incompatibleTags;
            MountsOnArtificialParts = mountsOnArtificialParts;
        }

        internal HediffDef Def { get; }
        internal string Label { get; }
        internal float Efficiency { get; }

        /// The implant item its surgery consumes, or null for a kind whose
        /// surgeries take no tech-hediff item: the hediff's
        /// spawnThingOnRemoved where it has one (every vanilla bionic), else
        /// the lowest-defName tech-hediff item the surgery recipes'
        /// ingredient filters allow (Anomaly's ghoul plating and barbs drop
        /// nothing when removed, so their hediffs name no item). Automation
        /// reserves, binds, crafts and counts this item; what removal
        /// drops stays Def.spawnThingOnRemoved (reinstalls, loss gate).
        internal ThingDef? Item { get; }
        internal List<BodyPartDef> FixedParts { get; }
        internal string GroupLabel { get; }

        /// True when the implant is an artificial version of the part it
        /// occupies rather than something mounted on it: its hediff is a
        /// Hediff_AddedPart, the class every game check for an artificial
        /// part tests (HediffSet.HasDirectlyAddedPartFor), whatever the
        /// surgery worker. In-place upgrades count as replacements.
        internal bool IsReplacement { get; }

        /// True when the surgery restores the part (and everything under
        /// it) before adding an implant that is not an added part, leaving
        /// the part natural: Recipe_InstallArtificialBodyPart used for an
        /// implant-class hediff, or Vanilla Genetics Expanded's install
        /// worker (ModCompatibility.ClearsPart). Implants installed after it
        /// stay; implants already there are pushed out, so automation puts
        /// it in first and reinstalls whatever it pushed out.
        internal bool WipesPart { get; }

        /// True when at least one surgery recipe's worker inherits neither
        /// vanilla install worker, so it carries no refusal of parts that
        /// are or sit under an artificial part: modded module workers
        /// (Bionic modularity's Recipe_InstallModule derives straight from
        /// Recipe_Surgery) mount ON a bionic limb. Always false for
        /// replacements.
        internal bool MountsOnArtificialParts { get; }

        /// True when such a worker also reaches parts UNDER an artificial
        /// part. The game marks every part below an added part missing, so
        /// a worker that checks for missing parts (Combat Bionics'
        /// Recipe_InstallImplantOverBionics, SurgeryWorkerBehavior
        /// .MountsOnArtificialPart) mounts on the artificial part at its
        /// own record only; the unclassified-worker assumption keeps both.
        /// Always false for replacements.
        internal bool MountsUnderArtificialParts { get; }

        /// A mounts-on-artificial kind whose every surgery uses such a
        /// worker (no vanilla implant recipe that could take a natural
        /// part): the game only ever offers it on a replacement, so the
        /// picker asks which replacement to plan alongside it. Inferred
        /// from the recipe set (Bionic modularity ships limb modules with
        /// the module worker only, organ modules with both).
        internal bool RequiresArtificialPart { get; }

        /// The kind this entry upgrades in place, or null. An upgrade's
        /// surgery (Integrated Implants' "modularize bionic eye":
        /// removesHediff = the base, addsHediff = this kind, no fixed body
        /// parts; the worker targets wherever the base sits) is only ever
        /// offered on a part carrying the base, so automation installs the
        /// base first when the colonist lacks it, then upgrades. The entry
        /// inherits the base's anatomy and is a replacement like it, so the
        /// two exclude each other in plans: picking the upgrade supersedes
        /// the base.
        internal string? UpgradesFrom { get; }

        /// The implant item its surgery consumes cannot be produced by any
        /// recipe (archotech parts and kin without a crafting mod): only
        /// trade, quests or salvage supply it. Listed in the picker only
        /// while PlannerModel.ShowPurchaseOnly is on; automation treats its
        /// slots as optional so a batch never waits for it.
        internal bool PurchaseOnly { get; }

        /// MutantDef names of which the pawn must be one for any surgery of
        /// this kind to apply: every surgery recipe carries a
        /// mutantPrerequisite (Anomaly's ghoul kit, Odyssey's blood warmer).
        /// Empty = ordinary colonists and mutants alike.
        internal List<string> RequiredMutants { get; }

        /// MutantDef names every surgery recipe refuses (mutantBlacklist on
        /// all of them: revenant vertebrae for ghouls).
        internal List<string> BlacklistedMutants { get; }

        /// Whether a pawn of the given mutant kind ("" = none) can take any
        /// surgery of this kind, by the recipes' mutant rules
        /// (Recipe_Surgery.AvailableOnNow). Allocation-free.
        internal bool AcceptsMutant(string mutant)
        {
            if (mutant.Length == 0) return RequiredMutants.Count == 0;
            if (BlacklistedMutants.Contains(mutant)) return false;
            return RequiredMutants.Count == 0 || RequiredMutants.Contains(mutant);
        }

        /// The anatomy region of the entry's targeted parts (classified on
        /// its first slot's body).
        internal ImplantRegion Region { get; }

        /// The limb family of the targeted parts (leg or arm by the body
        /// part tags the game's own Moving and Manipulation capacities
        /// read), for the ASAP candidate ranking.
        internal LimbKind Limb { get; }

        /// Union of the surgery recipes' incompatibleWithHediffTags: hediff
        /// tags that block this implant on a part carrying them (the vanilla
        /// skin-gland exclusivity mechanism).
        internal List<string> IncompatibleTags { get; }

        /// Every anatomy instance the implant can target on any plannable
        /// body (Catalogs.PlannableBodies), in body order then record
        /// order: the human body's left eye before its right, an android's
        /// sensors after them. One entry per part address, never empty.
        internal List<CatalogSlot> Slots { get; }

        /// Surgery recipes adding this implant, sorted by defName for
        /// deterministic recipe selection when scheduling operations.
        internal List<RecipeDef> SurgeryRecipes { get; }

        /// Index of the slot with this address, or -1 (a stale or
        /// unresolved goal slot). Allocation-free: tick path.
        internal int SlotIndexOf(PartAddress address)
        {
            for (int i = 0; i < Slots.Count; i++)
                if (Slots[i].Address.Equals(address)) return i;
            return -1;
        }

        internal CatalogSlot? SlotOf(PartAddress address)
        {
            int index = SlotIndexOf(address);
            return index >= 0 ? Slots[index] : null;
        }
    }

    /// The implant catalog discovered dynamically from vanilla, DLC, and
    /// modded content. Built in cache builders only, never during rendering.
    internal static class Catalogs
    {
        // Cache contract:
        // Owner: process.
        // Key: none (single snapshot).
        // Value: immutable sorted catalog list; observed defs are never
        //   mutated.
        // Dependencies: the loaded definition set (static per session) and
        //   the active language for labels and sort order.
        // Refresh policy: immediate rebuild on next read after the language
        //   revision moves.
        // Equality policy: rebuilt lists replace the reference; catalog
        //   consumers gate on the same language revision, so identity churn
        //   is bounded by language changes.
        // Teardown: Release clears the list (world teardown / game dispose).
        private static List<ImplantCatalogEntry>? implants;
        private static Dictionary<string, ImplantCatalogEntry>? implantsByDefName;
        private static int languageStamp = -1;

        internal static void Release()
        {
            plannableBodies = null;
            bodyKinships = null;
            offeredBodies = null;
            implants = null;
            implantsByDefName = null;
            languageStamp = -1;
            ImplantConflicts.Release();
            GeneHediffs.Release();
        }

        private static void EnsureCurrent()
        {
            if (languageStamp != UiRevision.LanguageCurrent)
            {
                implants = null;
                implantsByDefName = null;
                languageStamp = UiRevision.LanguageCurrent;
            }
        }

        internal static IReadOnlyList<ImplantCatalogEntry> Implants()
        {
            EnsureCurrent();
            return implants ?? (implants = BuildImplants());
        }

        /// Indexed lookup beside the sorted list (same lifetime and
        /// invalidation): hover tooltips and per-goal resolution hit this on
        /// every reconcile pass and hovered frame, so it must not scan.
        internal static ImplantCatalogEntry? ImplantByDefName(string defName)
        {
            IReadOnlyList<ImplantCatalogEntry> list = Implants();
            if (implantsByDefName == null)
            {
                var map = new Dictionary<string, ImplantCatalogEntry>(
                    list.Count, StringComparer.Ordinal);
                for (int i = 0; i < list.Count; i++)
                    map[list[i].Def.defName] = list[i];
                implantsByDefName = map;
            }
            return implantsByDefName.TryGetValue(defName, out ImplantCatalogEntry? entry)
                ? entry
                : null;
        }

        /// Whether the kind is the entry itself or sits anywhere below it
        /// in its upgrade chain (the base it upgrades, that base's base, and
        /// so on). Allocation-free: reached from the reconcile tick path.
        internal static bool UpgradeChainContains(ImplantCatalogEntry entry, string defName)
        {
            ImplantCatalogEntry? walk = entry;
            for (int guard = 0; walk != null && guard < 8; guard++)
            {
                if (string.Equals(walk.Def.defName, defName, StringComparison.Ordinal))
                    return true;
                walk = walk.UpgradesFrom != null ? ImplantByDefName(walk.UpgradesFrom) : null;
            }
            return false;
        }

        private static List<ImplantCatalogEntry> BuildImplants()
        {
            // Every implant a surgery recipe can add to a humanlike body part.
            var parts = new Dictionary<HediffDef, HashSet<BodyPartDef>>();
            var surgeries = new Dictionary<HediffDef, List<RecipeDef>>();
            // Implant items each surgery consumes, collected in the same
            // walk so the purchase-only test is a lookup rather than a
            // rescan of every recipe per candidate.
            var implantItems = new Dictionary<HediffDef, List<ThingDef>>();
            // In-place upgrades: a surgery that removes one implant kind and
            // adds another with no fixed parts of its own (the worker
            // targets wherever the removed kind sits).
            var upgrades = new Dictionary<HediffDef, List<RecipeDef>>();
            List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDef recipe = recipes[i];
                if (recipe.addsHediff == null || !recipe.IsSurgery) continue;
                CollectImplantItems(recipe, implantItems);
                if (recipe.appliedOnFixedBodyParts.NullOrEmpty())
                {
                    if (recipe.removesHediff != null && IsPlannable(recipe.addsHediff))
                    {
                        if (!upgrades.TryGetValue(recipe.addsHediff, out List<RecipeDef>? list))
                        {
                            list = new List<RecipeDef>();
                            upgrades.Add(recipe.addsHediff, list);
                        }
                        list.Add(recipe);
                    }
                    continue;
                }
                if (!IsPlannable(recipe.addsHediff)) continue;
                if (!parts.TryGetValue(recipe.addsHediff, out HashSet<BodyPartDef>? set))
                {
                    set = new HashSet<BodyPartDef>();
                    parts.Add(recipe.addsHediff, set);
                    surgeries.Add(recipe.addsHediff, new List<RecipeDef>());
                }
                for (int p = 0; p < recipe.appliedOnFixedBodyParts.Count; p++)
                    set.Add(recipe.appliedOnFixedBodyParts[p]);
                surgeries[recipe.addsHediff].Add(recipe);
            }

            HashSet<ThingDef> producible = BuildProducibleSet();
            List<BodyDef> bodies = PlannableBodies();
            var result = new List<ImplantCatalogEntry>();
            foreach (KeyValuePair<HediffDef, HashSet<BodyPartDef>> pair in parts)
            {
                bool purchaseOnly = IsPurchaseOnly(pair.Key, producible, implantItems);
                var fixedParts = new List<BodyPartDef>(pair.Value);
                fixedParts.Sort(ByDefName);
                bool replacement = IsAddedPart(pair.Key);
                // The game ignores addedPartProps on a hediff that is not an
                // added part: the part keeps its natural efficiency.
                float efficiency = replacement
                    ? pair.Key.addedPartProps?.partEfficiency ?? 1f
                    : 1f;
                List<RecipeDef> surgeryRecipes = surgeries[pair.Key];
                surgeryRecipes.Sort(ByRecipeDefName);
                List<CatalogSlot> slots = BuildSlots(bodies, pair.Value);
                // Parts no humanlike body has (an animal jaw) can never be
                // installed on a colonist.
                if (slots.Count == 0) continue;
                bool wipes = !replacement && ClearsPart(surgeryRecipes);
                bool mountsUnder = !replacement && !wipes
                    && MountsOnArtificialParts(surgeryRecipes);
                bool mounts = mountsUnder || (!replacement && !wipes
                    && AnyWorker(surgeryRecipes, SurgeryWorkerBehavior.MountsOnArtificialPart));
                result.Add(new ImplantCatalogEntry(
                    pair.Key, pair.Key.LabelCap.ToString(), efficiency,
                    fixedParts, GroupLabelOf(slots), slots,
                    surgeryRecipes, replacement,
                    ClassifyRegion(slots[0].Record), ClassifyLimb(slots[0].Record),
                    IncompatibleTagsOf(surgeryRecipes), mounts,
                    mountsUnder && !AnyVanillaImplantRecipe(surgeryRecipes), null,
                    purchaseOnly, wipes, mountsUnder,
                    RequiredMutantsOf(surgeryRecipes), BlacklistedMutantsOf(surgeryRecipes),
                    SurgeryItemOf(pair.Key, implantItems)));
            }
            AddUpgrades(result, upgrades, producible, implantItems);
            result.Sort(ByGroupThenLabel);
            return result;
        }

        /// One entry per in-place upgrade whose base kind is itself in the
        /// catalog (an upgrade of an excluded base is excluded with it, and
        /// an upgrade of a purchase-only base is purchase-only itself): the
        /// base's anatomy, the upgrade's own efficiency, and only the
        /// upgrade surgeries. Several upgrade surgeries for one kind take
        /// the base of the lowest recipe defName. Runs once per build after
        /// the base entries exist, in defName order, so the result is
        /// deterministic.
        private static void AddUpgrades(List<ImplantCatalogEntry> result,
            Dictionary<HediffDef, List<RecipeDef>> upgrades,
            HashSet<ThingDef> producible,
            Dictionary<HediffDef, List<ThingDef>> implantItems)
        {
            if (upgrades.Count == 0) return;
            var byDef = new Dictionary<HediffDef, ImplantCatalogEntry>();
            for (int i = 0; i < result.Count; i++)
                byDef[result[i].Def] = result[i];
            var kinds = new List<HediffDef>(upgrades.Keys);
            kinds.Sort((a, b) => string.CompareOrdinal(a.defName, b.defName));
            for (int k = 0; k < kinds.Count; k++)
            {
                HediffDef def = kinds[k];
                if (byDef.ContainsKey(def)) continue; // also installable directly
                List<RecipeDef> surgeryRecipes = upgrades[def];
                surgeryRecipes.Sort(ByRecipeDefName);
                if (!byDef.TryGetValue(surgeryRecipes[0].removesHediff,
                        out ImplantCatalogEntry? baseEntry))
                    continue;
                float efficiency = def.addedPartProps?.partEfficiency ?? 1f;
                result.Add(new ImplantCatalogEntry(
                    def, def.LabelCap.ToString(), efficiency,
                    baseEntry.FixedParts, baseEntry.GroupLabel, baseEntry.Slots,
                    surgeryRecipes, isReplacement: true,
                    baseEntry.Region, baseEntry.Limb,
                    IncompatibleTagsOf(surgeryRecipes),
                    mountsOnArtificialParts: false, requiresArtificialPart: false,
                    baseEntry.Def.defName,
                    baseEntry.PurchaseOnly || IsPurchaseOnly(def, producible, implantItems),
                    wipesPart: false, mountsUnderArtificialParts: false,
                    RequiredMutantsOf(surgeryRecipes), BlacklistedMutantsOf(surgeryRecipes),
                    SurgeryItemOf(def, implantItems)));
            }
        }

        private static readonly Comparison<RecipeDef> ByRecipeDefName =
            (a, b) => string.CompareOrdinal(a.defName, b.defName);

        /// Whether the hediff is an artificial part to the game.
        internal static bool IsAddedPart(HediffDef def) =>
            typeof(Hediff_AddedPart).IsAssignableFrom(def.hediffClass);

        /// Whether any surgery recipe's worker restores the part before
        /// adding its hediff (SurgeryWorkerDef.BehaviorOf).
        private static bool ClearsPart(List<RecipeDef> surgeryRecipes) =>
            AnyWorker(surgeryRecipes, SurgeryWorkerBehavior.ClearsPart);

        /// Whether any surgery recipe's worker carries no refusal of
        /// artificial parts, so the game lets it target one (verified for
        /// Bionic modularity's Recipe_InstallModule : Recipe_Surgery; any
        /// worker neither vanilla ancestry nor a SurgeryWorkerDef explains).
        private static bool MountsOnArtificialParts(List<RecipeDef> surgeryRecipes) =>
            AnyWorker(surgeryRecipes, SurgeryWorkerBehavior.MountsOnArtificialParts);

        /// Whether any surgery recipe's worker installs like vanilla's
        /// implant worker, which accepts natural parts.
        private static bool AnyVanillaImplantRecipe(List<RecipeDef> surgeryRecipes) =>
            AnyWorker(surgeryRecipes, SurgeryWorkerBehavior.InstallsImplant);

        private static bool AnyWorker(List<RecipeDef> surgeryRecipes,
            SurgeryWorkerBehavior behavior)
        {
            for (int i = 0; i < surgeryRecipes.Count; i++)
            {
                Type? worker = surgeryRecipes[i].workerClass;
                if (worker != null && SurgeryWorkerDef.BehaviorOf(worker) == behavior)
                    return true;
            }
            return false;
        }

        /// Implants excluded by name: not meaningful shared goals even though
        /// the data-driven rules keep them (DLCs add crafting recipes for
        /// several). Anything strictly better than natural stays in.
        private static readonly string[] NotSharedGoals =
        {
            "Joywire",        // consciousness penalty outweighs the mood
            "Painstopper",    // removes pain response — situational, not fleet-wide
            "PowerClaw",      // replaces a hand; melee-niche sidegrade
            "PilotAssistant", // Odyssey piloting niche
        };

        /// Goals must never make a pawn worse off: added parts are included
        /// unless they are strictly worse than the natural part. This drops
        /// peg legs, dentures, and simple prosthetics.
        ///
        /// The test is strictly-below-1, not at-or-below-1:
        /// AddedBodyPartProps.partEfficiency defaults to 1f, so an implant that
        /// declares addedPartProps without an efficiency (drill arms, elbow
        /// blades, bionic spines, revenant vertebrae, ghoul hearts) inherits
        /// the default and would otherwise be rejected despite being a pure
        /// upgrade. Anything at exactly 1.0 either restores a lost part or
        /// carries its own enhancement effect.
        private static bool IsPlannable(HediffDef def)
        {
            if (def.addedPartProps != null && def.addedPartProps.partEfficiency < 1f)
                return false;
            for (int i = 0; i < NotSharedGoals.Length; i++)
                if (def.defName == NotSharedGoals[i])
                    return false;
            return true;
        }

        /// Everything any non-surgery recipe can produce, regardless of
        /// current research or benches (those surface as blockers, not
        /// catalog holes).
        private static HashSet<ThingDef> BuildProducibleSet()
        {
            var producible = new HashSet<ThingDef>();
            List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDef recipe = recipes[i];
                if (!recipe.IsSurgery && recipe.ProducedThingDef != null)
                    producible.Add(recipe.ProducedThingDef);
            }
            return producible;
        }

        /// Records the tech-hediff items a surgery recipe's ingredient
        /// filters allow against the hediff it adds.
        private static void CollectImplantItems(RecipeDef recipe,
            Dictionary<HediffDef, List<ThingDef>> implantItems)
        {
            List<IngredientCount>? ingredients = recipe.ingredients;
            if (ingredients == null) return;
            for (int g = 0; g < ingredients.Count; g++)
                foreach (ThingDef ingredient in ingredients[g].filter.AllowedThingDefs)
                {
                    if (!ingredient.isTechHediff) continue;
                    if (!implantItems.TryGetValue(recipe.addsHediff, out List<ThingDef>? items))
                    {
                        items = new List<ThingDef>();
                        implantItems.Add(recipe.addsHediff, items);
                    }
                    items.Add(ingredient);
                }
        }

        /// The implant item a kind's surgery consumes (ImplantCatalogEntry
        /// .Item): spawnThingOnRemoved, else the lowest-defName tech-hediff
        /// item its surgeries' ingredient filters allow, else null.
        private static ThingDef? SurgeryItemOf(HediffDef def,
            Dictionary<HediffDef, List<ThingDef>> implantItems)
        {
            if (def.spawnThingOnRemoved != null) return def.spawnThingOnRemoved;
            if (!implantItems.TryGetValue(def, out List<ThingDef>? items) || items.Count == 0)
                return null;
            ThingDef item = items[0];
            for (int i = 1; i < items.Count; i++)
                if (string.CompareOrdinal(items[i].defName, item.defName) < 0)
                    item = items[i];
            return item;
        }

        /// The surgery item of a hediff kind (ImplantCatalogEntry.Item),
        /// falling back to spawnThingOnRemoved for kinds outside the
        /// catalog. Dictionary lookup: reached from the surgery patches.
        internal static ThingDef? ItemOf(HediffDef def) =>
            ImplantByDefName(def.defName)?.Item ?? def.spawnThingOnRemoved;

        /// Purchase-only implants (archotech parts, joywire-class trader
        /// goods, and modded equivalents): the implant item its surgery
        /// consumes cannot be produced by any recipe. Listed behind the
        /// ShowPurchaseOnly option and never able to hold a batch up.
        /// Implants whose surgeries consume no identifiable implant item
        /// are ordinary.
        private static bool IsPurchaseOnly(HediffDef def, HashSet<ThingDef> producible,
            Dictionary<HediffDef, List<ThingDef>> implantItems)
        {
            if (!implantItems.TryGetValue(def, out List<ThingDef>? items))
                return false;
            for (int i = 0; i < items.Count; i++)
                if (producible.Contains(items[i]))
                    return false;
            return true;
        }

        // Cache contract:
        // Owner: process.
        // Key: none (single list).
        // Value: immutable list of the distinct BodyDefs of humanlike races,
        //   the human body first, then by defName.
        // Dependencies: the loaded definition set (static per session).
        // Refresh policy: built on first query.
        // Equality policy: never changes within a session.
        // Teardown: Release clears it with the catalog.
        private static List<BodyDef>? plannableBodies;

        /// Every body a colonist can have: the distinct bodies of humanlike
        /// races (vanilla's human body, Humanoid Alien Races bodies such as
        /// Android Tiers' Android4Tier). Human first, then by defName, so
        /// slot order, labels and legacy migration are deterministic.
        internal static List<BodyDef> PlannableBodies()
        {
            if (plannableBodies != null) return plannableBodies;
            var bodies = new List<BodyDef>();
            List<ThingDef> things = DefDatabase<ThingDef>.AllDefsListForReading;
            for (int i = 0; i < things.Count; i++)
            {
                RaceProperties? race = things[i].race;
                if (race == null || !race.Humanlike || race.body == null) continue;
                if (!bodies.Contains(race.body)) bodies.Add(race.body);
            }
            bodies.Sort((a, b) => a == BodyDefOf.Human ? (b == BodyDefOf.Human ? 0 : -1)
                : b == BodyDefOf.Human ? 1
                : string.CompareOrdinal(a.defName, b.defName));
            return plannableBodies = bodies;
        }

        // Cache contract (body kinship):
        // Owner: process.
        // Key: PlannableBodies position.
        // Value: immutable BodyKinship per body (Core BodyKinships.Classify
        //   over the catalog slot keys each body carries, against the human
        //   body's), and the derived offered-body list.
        // Dependencies: the loaded definition set (static per session; slot
        //   addresses are label-free, so not language-gated).
        // Refresh policy: built on first query.
        // Equality policy: never changes within a session.
        // Teardown: Release clears both with the catalog.
        private static BodyKinship[]? bodyKinships;
        private static List<BodyDef>? offeredBodies;

        /// How the body at this PlannableBodies position relates to the
        /// human body by the catalog slots it carries (owner, 2026-11-01):
        /// a copy of the human anatomy folds under the human body, a body
        /// with every human slot and more joins human plans and keeps its
        /// own entry, anything missing a human slot is a body of its own.
        /// Allocation-free after the first query (tick path per pawn).
        internal static BodyKinship KinshipOf(int body)
        {
            BodyKinship[] kinships = bodyKinships ?? BuildKinships();
            return body >= 0 && body < kinships.Length ? kinships[body] : BodyKinship.Distinct;
        }

        /// The bodies offered as a body of their own in the body filter and
        /// the new-plan dialog: the plannable bodies less the human
        /// equivalents, in plannable-body order.
        internal static List<BodyDef> OfferedBodies()
        {
            if (offeredBodies != null) return offeredBodies;
            List<BodyDef> bodies = PlannableBodies();
            var offered = new List<BodyDef>(bodies.Count);
            for (int b = 0; b < bodies.Count; b++)
                if (BodyKinships.Offered(KinshipOf(b))) offered.Add(bodies[b]);
            return offeredBodies = offered;
        }

        private static BodyKinship[] BuildKinships()
        {
            List<BodyDef> bodies = PlannableBodies();
            IReadOnlyList<ImplantCatalogEntry> entries = Implants();
            var keys = new List<string>[bodies.Count];
            for (int b = 0; b < bodies.Count; b++)
            {
                var carried = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < entries.Count; i++)
                {
                    List<CatalogSlot> slots = entries[i].Slots;
                    for (int s = 0; s < slots.Count; s++)
                        if (slots[s].Address.Occurrence < bodies[b].GetPartsWithDef(slots[s].Part).Count)
                            carried.Add(slots[s].Address.Key);
                }
                keys[b] = new List<string>(carried);
            }
            int human = bodies.IndexOf(BodyDefOf.Human);
            var result = new BodyKinship[bodies.Count];
            for (int b = 0; b < bodies.Count; b++)
                result[b] = b == human ? BodyKinship.Human
                    : human < 0 ? BodyKinship.Distinct
                    : BodyKinships.Classify(keys[human], keys[b]);
            return bodyKinships = result;
        }

        /// Every anatomy instance of the given part kinds across the
        /// plannable bodies: one slot per part address (part kind +
        /// occurrence in that body's record order), labelled and located on
        /// the first body carrying it. Body order, then record order.
        private static List<CatalogSlot> BuildSlots(List<BodyDef> bodies,
            HashSet<BodyPartDef> fixedParts)
        {
            var slots = new List<CatalogSlot>();
            for (int b = 0; b < bodies.Count; b++)
            {
                List<BodyPartRecord> records = bodies[b].AllParts;
                for (int r = 0; r < records.Count; r++)
                {
                    BodyPartDef part = records[r].def;
                    if (!fixedParts.Contains(part)) continue;
                    var address = new PartAddress(part.defName,
                        bodies[b].GetPartsWithDef(part).IndexOf(records[r]));
                    bool known = false;
                    for (int s = 0; s < slots.Count && !known; s++)
                        known = slots[s].Address.Equals(address);
                    if (!known) slots.Add(new CatalogSlot(address, part, bodies[b], records[r]));
                }
            }
            return slots;
        }

        /// The picker group: the part kind when every slot is one kind
        /// ("Eye"); otherwise the most specific body part group every slot
        /// belongs to (Android Tiers' left and right sight sensors are both
        /// "Eyes"), else the first slot's part kind.
        private static string GroupLabelOf(List<CatalogSlot> slots)
        {
            BodyPartDef first = slots[0].Part;
            bool single = true;
            for (int i = 1; i < slots.Count && single; i++)
                single = slots[i].Part == first;
            if (single) return first.LabelCap.ToString();
            BodyPartGroupDef? best = null;
            int bestSize = int.MaxValue;
            List<BodyPartGroupDef> groups = slots[0].Record.groups;
            for (int g = 0; g < groups.Count; g++)
            {
                bool shared = true;
                for (int i = 1; i < slots.Count && shared; i++)
                    shared = slots[i].Record.groups.Contains(groups[g]);
                if (!shared) continue;
                int size = 0;
                List<BodyPartRecord> records = slots[0].Body.AllParts;
                for (int r = 0; r < records.Count; r++)
                    if (records[r].groups.Contains(groups[g])) size++;
                if (size < bestSize)
                {
                    best = groups[g];
                    bestSize = size;
                }
            }
            return best != null ? best.LabelCap.ToString() : first.LabelCap.ToString();
        }

        /// The part addresses a goal slot persisted as a bare ordinal
        /// (before part addresses) denotes. The ordinal indexed each pawn's
        /// OWN enumeration of the implant's part kinds (defName order, then
        /// record order), so it meant one part per body: ordinal 0 of a VX
        /// chip was the brain on a human and the artificial brain on an
        /// android, and ordinal 0 of an android light sensor the left one.
        /// Every plannable body's part at that position, in body order, so
        /// a migrated goal keeps acting where it acted before. A legacy
        /// address when the implant is not loaded (resolved on a later
        /// load); empty when the ordinal denotes nothing. Load and import
        /// paths only.
        internal static IReadOnlyList<PartAddress> LegacySlots(string defName, int ordinal)
        {
            ImplantCatalogEntry? entry = ImplantByDefName(defName);
            if (entry == null) return new[] { PartAddress.Legacy(ordinal) };
            var result = new List<PartAddress>();
            List<BodyDef> bodies = PlannableBodies();
            for (int b = 0; b < bodies.Count; b++)
            {
                int index = 0;
                for (int p = 0; p < entry.FixedParts.Count; p++)
                {
                    List<BodyPartRecord> records = bodies[b].GetPartsWithDef(entry.FixedParts[p]);
                    for (int r = 0; r < records.Count; r++, index++)
                    {
                        if (index != ordinal) continue;
                        var address = new PartAddress(entry.FixedParts[p].defName, r);
                        if (!result.Contains(address)) result.Add(address);
                    }
                }
            }
            return result;
        }

        /// LegacySlots as a cached delegate for the load and import paths.
        internal static readonly Func<string, int, IReadOnlyList<PartAddress>> LegacySlotMapper =
            LegacySlots;

        /// Region tags: limb parts carry the moving/manipulation limb tags,
        /// and everything hanging under the Head record (brain included) is
        /// Head; the rest of the body (organs, spine, skin-level torso
        /// parts) is Torso.
        private static readonly string[] LimbTags =
        {
            "MovingLimbCore", "MovingLimbSegment", "MovingLimbDigit",
            "ManipulationLimbCore", "ManipulationLimbSegment",
            "ManipulationLimbDigit",
        };

        private static ImplantRegion ClassifyRegion(BodyPartRecord record)
        {
            for (BodyPartRecord? walk = record; walk != null; walk = walk.parent)
                if (HasAnyLimbTag(walk.def))
                    return ImplantRegion.Limbs;
            for (BodyPartRecord? walk = record; walk != null; walk = walk.parent)
                if (walk.def.defName == "Head")
                    return ImplantRegion.Head;
            return ImplantRegion.Torso;
        }

        private static bool HasAnyLimbTag(BodyPartDef def)
        {
            for (int i = 0; i < LimbTags.Length; i++)
                if (HasTag(def, LimbTags[i]))
                    return true;
            return false;
        }

        /// Leg when the targeted part or an ancestor carries a Moving limb
        /// tag, arm for a Manipulation limb tag (the first three and last
        /// three entries of LimbTags); anything else has no limb family.
        private static LimbKind ClassifyLimb(BodyPartRecord record)
        {
            for (BodyPartRecord? walk = record; walk != null; walk = walk.parent)
            {
                for (int i = 0; i < LimbTags.Length; i++)
                    if (HasTag(walk.def, LimbTags[i]))
                        return i < 3 ? LimbKind.Leg : LimbKind.Arm;
            }
            return LimbKind.None;
        }

        private static bool HasTag(BodyPartDef def, string tagDefName)
        {
            List<BodyPartTagDef>? tags = def.tags;
            if (tags == null) return false;
            for (int i = 0; i < tags.Count; i++)
                if (tags[i]?.defName == tagDefName)
                    return true;
            return false;
        }

        /// The mutant kinds any surgery of the kind needs, or empty when at
        /// least one recipe applies to ordinary pawns (Recipe_Surgery
        /// .AvailableOnNow refuses a recipe with a mutantPrerequisite for
        /// non-mutants and for other mutants). Sorted by defName.
        private static List<string> RequiredMutantsOf(List<RecipeDef> surgeryRecipes)
        {
            var required = new List<string>();
            for (int i = 0; i < surgeryRecipes.Count; i++)
            {
                List<MutantDef>? prerequisite = surgeryRecipes[i].mutantPrerequisite;
                if (prerequisite.NullOrEmpty())
                {
                    required.Clear();
                    return required;
                }
                for (int m = 0; m < prerequisite!.Count; m++)
                    if (!required.Contains(prerequisite[m].defName))
                        required.Add(prerequisite[m].defName);
            }
            required.Sort(StringComparer.Ordinal);
            return required;
        }

        /// The mutant kinds every surgery of the kind refuses
        /// (mutantBlacklist on each recipe): a kind with one recipe that
        /// does not blacklist a mutant still reaches it. Sorted by defName.
        private static List<string> BlacklistedMutantsOf(List<RecipeDef> surgeryRecipes)
        {
            var blacklisted = new List<string>();
            for (int i = 0; i < surgeryRecipes.Count; i++)
            {
                List<MutantDef>? blacklist = surgeryRecipes[i].mutantBlacklist;
                if (blacklist.NullOrEmpty())
                {
                    blacklisted.Clear();
                    return blacklisted;
                }
                if (i == 0)
                {
                    for (int m = 0; m < blacklist!.Count; m++)
                        blacklisted.Add(blacklist[m].defName);
                    continue;
                }
                for (int b = blacklisted.Count - 1; b >= 0; b--)
                {
                    bool kept = false;
                    for (int m = 0; m < blacklist!.Count && !kept; m++)
                        kept = blacklist[m].defName == blacklisted[b];
                    if (!kept) blacklisted.RemoveAt(b);
                }
            }
            blacklisted.Sort(StringComparer.Ordinal);
            return blacklisted;
        }

        /// Whether any catalog kind is for ghouls (Anomaly active with its
        /// ghoul kit loaded): the new-plan prompt then offers a ghoul plan.
        /// Builder path (dialog construction), scans the catalog once.
        internal static bool GhoulsPlannable()
        {
            if (!ModsConfig.AnomalyActive) return false;
            IReadOnlyList<ImplantCatalogEntry> entries = Implants();
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].RequiredMutants.Contains(GhoulMutant))
                    return true;
            return false;
        }

        /// The MutantDef name of Anomaly's ghoul, the one mutant kind plans
        /// can be made for.
        internal const string GhoulMutant = "Ghoul";

        private static List<string> IncompatibleTagsOf(List<RecipeDef> surgeryRecipes)
        {
            var tags = new List<string>();
            for (int i = 0; i < surgeryRecipes.Count; i++)
            {
                List<string>? recipeTags = surgeryRecipes[i].incompatibleWithHediffTags;
                if (recipeTags == null) continue;
                for (int t = 0; t < recipeTags.Count; t++)
                    if (!tags.Contains(recipeTags[t]))
                        tags.Add(recipeTags[t]);
            }
            return tags;
        }

        private static readonly Comparison<BodyPartDef> ByDefName =
            (a, b) => string.CompareOrdinal(a.defName, b.defName);

        private static readonly Comparison<ImplantCatalogEntry> ByGroupThenLabel = (a, b) =>
        {
            int group = string.Compare(a.GroupLabel, b.GroupLabel, StringComparison.OrdinalIgnoreCase);
            if (group != 0) return group;
            int label = string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
            return label != 0 ? label : string.CompareOrdinal(a.Def.defName, b.Def.defName);
        };
    }
}
