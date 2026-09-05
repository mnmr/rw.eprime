using System;
using System.Collections.Generic;
using Implanner.Core;
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

    /// One selectable implant kind: a hediff added by a surgery recipe,
    /// with the anatomy it can target.
    internal sealed class ImplantCatalogEntry
    {
        internal ImplantCatalogEntry(HediffDef def, string label, float efficiency,
            List<BodyPartDef> fixedParts, string groupLabel, List<string> slotLabels,
            List<BodyPartRecord> slotRecords, List<RecipeDef> surgeryRecipes,
            bool isReplacement, ImplantRegion region, LimbKind limb,
            List<string> incompatibleTags, bool mountsOnArtificialParts,
            bool requiresArtificialPart, string? upgradesFrom, bool purchaseOnly)
        {
            RequiresArtificialPart = requiresArtificialPart;
            UpgradesFrom = upgradesFrom;
            PurchaseOnly = purchaseOnly;
            Def = def;
            Label = label;
            Efficiency = efficiency;
            FixedParts = fixedParts;
            GroupLabel = groupLabel;
            SlotLabels = slotLabels;
            SlotRecords = slotRecords;
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
        internal List<BodyPartDef> FixedParts { get; }
        internal string GroupLabel { get; }

        /// True when the implant is an artificial version of the part it
        /// occupies rather than something mounted on it. Read from the
        /// surgery's own worker class, which is the distinction RimWorld
        /// itself draws: Recipe_InstallArtificialBodyPart swaps the part out,
        /// Recipe_InstallImplant leaves it in place. A hediff with
        /// addedPartProps counts too whatever its worker: the game treats it
        /// as an added part once installed (in-place upgrades).
        internal bool IsReplacement { get; }

        /// True when at least one surgery recipe's worker inherits neither
        /// vanilla install worker, so it carries no refusal of parts that
        /// are or sit under an artificial part: modded module workers
        /// (Bionic modularity's Recipe_InstallModule derives straight from
        /// Recipe_Surgery) mount ON a bionic limb. Always false for
        /// replacements.
        internal bool MountsOnArtificialParts { get; }

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

        /// The anatomy region of the entry's targeted parts (classified on
        /// the reference body).
        internal ImplantRegion Region { get; }

        /// The limb family of the targeted parts (leg or arm by the body
        /// part tags the game's own Moving and Manipulation capacities
        /// read), for the ASAP candidate ranking.
        internal LimbKind Limb { get; }

        /// Union of the surgery recipes' incompatibleWithHediffTags: hediff
        /// tags that block this implant on a part carrying them (the vanilla
        /// skin-gland exclusivity mechanism).
        internal List<string> IncompatibleTags { get; }

        /// Reference-body anatomy instances, parallel to SlotLabels: ordinal
        /// i targets SlotRecords[i] (null only for the placeholder slot of a
        /// part missing from the reference body). Observed records, never
        /// mutated; used to derive conflict facts.
        internal List<BodyPartRecord> SlotRecords { get; }

        /// Surgery recipes adding this implant, sorted by defName for
        /// deterministic recipe selection when scheduling operations.
        internal List<RecipeDef> SurgeryRecipes { get; }

        /// Editor labels for the canonical slot enumeration on the reference
        /// human body (FixedParts order, then body record order): ordinal i is
        /// SlotLabels[i]. Never empty; bodies without the part get one
        /// unlabeled slot so the goal stays selectable (evaluation excludes
        /// impossible slots from the colonist's target).
        internal List<string> SlotLabels { get; }
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
            implants = null;
            implantsByDefName = null;
            languageStamp = -1;
            ImplantConflicts.Release();
        }

        private static void EnsureCurrent()
        {
            if (languageStamp != UiVersion.LanguageCurrent)
            {
                implants = null;
                implantsByDefName = null;
                languageStamp = UiVersion.LanguageCurrent;
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
            return implantsByDefName.TryGetValue(defName, out ImplantCatalogEntry entry)
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
                        if (!upgrades.TryGetValue(recipe.addsHediff, out List<RecipeDef> list))
                        {
                            list = new List<RecipeDef>();
                            upgrades.Add(recipe.addsHediff, list);
                        }
                        list.Add(recipe);
                    }
                    continue;
                }
                if (!IsPlannable(recipe.addsHediff)) continue;
                if (!parts.TryGetValue(recipe.addsHediff, out HashSet<BodyPartDef> set))
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
            BodyDef referenceBody = BodyDefOf.Human;
            var result = new List<ImplantCatalogEntry>();
            foreach (KeyValuePair<HediffDef, HashSet<BodyPartDef>> pair in parts)
            {
                bool purchaseOnly = IsPurchaseOnly(pair.Key, producible, implantItems);
                var fixedParts = new List<BodyPartDef>(pair.Value);
                fixedParts.Sort(ByDefName);
                float efficiency = pair.Key.addedPartProps?.partEfficiency ?? 1f;
                string groupLabel = fixedParts.Count > 0
                    ? fixedParts[0].LabelCap.ToString()
                    : "IMP_ImplantGroupOther".Translate().ToString();
                List<RecipeDef> surgeryRecipes = surgeries[pair.Key];
                surgeryRecipes.Sort(ByRecipeDefName);
                // Mutant-only implants (ghoul barbs and kin) are not plan
                // goals: only ordinary humanlikes are plannable.
                if (!AnyHumanRecipe(surgeryRecipes)) continue;
                BuildSlots(referenceBody, fixedParts,
                    out List<string> slotLabels, out List<BodyPartRecord> slotRecords);
                bool replacement = IsReplacement(surgeryRecipes)
                    || pair.Key.addedPartProps != null;
                bool mounts = !replacement && MountsOnArtificialParts(surgeryRecipes);
                result.Add(new ImplantCatalogEntry(
                    pair.Key, pair.Key.LabelCap.ToString(), efficiency,
                    fixedParts, groupLabel, slotLabels, slotRecords,
                    surgeryRecipes, replacement,
                    ClassifyRegion(slotRecords), ClassifyLimb(slotRecords),
                    IncompatibleTagsOf(surgeryRecipes), mounts,
                    mounts && !AnyVanillaImplantRecipe(surgeryRecipes), null,
                    purchaseOnly));
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
                if (!AnyHumanRecipe(surgeryRecipes)) continue;
                if (!byDef.TryGetValue(surgeryRecipes[0].removesHediff,
                        out ImplantCatalogEntry baseEntry))
                    continue;
                float efficiency = def.addedPartProps?.partEfficiency ?? 1f;
                result.Add(new ImplantCatalogEntry(
                    def, def.LabelCap.ToString(), efficiency,
                    baseEntry.FixedParts, baseEntry.GroupLabel,
                    baseEntry.SlotLabels, baseEntry.SlotRecords,
                    surgeryRecipes, isReplacement: true,
                    baseEntry.Region, baseEntry.Limb,
                    IncompatibleTagsOf(surgeryRecipes),
                    mountsOnArtificialParts: false, requiresArtificialPart: false,
                    baseEntry.Def.defName,
                    baseEntry.PurchaseOnly || IsPurchaseOnly(def, producible, implantItems)));
            }
        }

        private static readonly Comparison<RecipeDef> ByRecipeDefName =
            (a, b) => string.CompareOrdinal(a.defName, b.defName);

        private static bool IsReplacement(List<RecipeDef> surgeryRecipes)
        {
            for (int i = 0; i < surgeryRecipes.Count; i++)
            {
                Type? worker = surgeryRecipes[i].workerClass;
                if (worker != null
                    && typeof(Recipe_InstallArtificialBodyPart).IsAssignableFrom(worker))
                    return true;
            }
            return false;
        }

        /// Whether any surgery recipe's worker sits outside both vanilla
        /// install workers: such a worker never inherited
        /// Recipe_InstallImplant's added-part refusal, so the game lets it
        /// target an artificial part (verified for Bionic modularity's
        /// Recipe_InstallModule : Recipe_Surgery).
        private static bool MountsOnArtificialParts(List<RecipeDef> surgeryRecipes)
        {
            for (int i = 0; i < surgeryRecipes.Count; i++)
            {
                Type? worker = surgeryRecipes[i].workerClass;
                if (worker != null
                    && !typeof(Recipe_InstallImplant).IsAssignableFrom(worker)
                    && !typeof(Recipe_InstallArtificialBodyPart).IsAssignableFrom(worker))
                    return true;
            }
            return false;
        }

        /// Whether any surgery recipe uses vanilla's implant worker (or a
        /// derivative), which accepts natural parts.
        private static bool AnyVanillaImplantRecipe(List<RecipeDef> surgeryRecipes)
        {
            for (int i = 0; i < surgeryRecipes.Count; i++)
            {
                Type? worker = surgeryRecipes[i].workerClass;
                if (worker != null && typeof(Recipe_InstallImplant).IsAssignableFrom(worker))
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
                    if (!implantItems.TryGetValue(recipe.addsHediff, out List<ThingDef> items))
                    {
                        items = new List<ThingDef>();
                        implantItems.Add(recipe.addsHediff, items);
                    }
                    items.Add(ingredient);
                }
        }

        /// Purchase-only implants (archotech parts, joywire-class trader
        /// goods, and modded equivalents): the implant item its surgery
        /// consumes cannot be produced by any recipe. Listed behind the
        /// ShowPurchaseOnly option and never able to hold a batch up.
        /// Implants whose surgeries consume no identifiable implant item
        /// are ordinary.
        private static bool IsPurchaseOnly(HediffDef def, HashSet<ThingDef> producible,
            Dictionary<HediffDef, List<ThingDef>> implantItems)
        {
            if (!implantItems.TryGetValue(def, out List<ThingDef> items))
                return false;
            for (int i = 0; i < items.Count; i++)
                if (producible.Contains(items[i]))
                    return false;
            return true;
        }

        /// The canonical slot enumeration on the reference body: FixedParts
        /// order, then body record order. Must stay in lockstep with
        /// PawnProjection.BuildImplantContext, which enumerates the same way
        /// on the evaluated pawn's body.
        private static void BuildSlots(BodyDef body, List<BodyPartDef> fixedParts,
            out List<string> labels, out List<BodyPartRecord> records)
        {
            labels = new List<string>();
            records = new List<BodyPartRecord>();
            for (int p = 0; p < fixedParts.Count; p++)
            {
                List<BodyPartRecord> found = body.GetPartsWithDef(fixedParts[p]);
                if (found == null) continue;
                for (int r = 0; r < found.Count; r++)
                {
                    labels.Add(found[r].Label);
                    records.Add(found[r]);
                }
            }
            if (labels.Count == 0)
            {
                labels.Add("");
                records.Add(null!);
            }
        }

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

        private static ImplantRegion ClassifyRegion(List<BodyPartRecord> slotRecords)
        {
            BodyPartRecord? record = slotRecords.Count > 0 ? slotRecords[0] : null;
            if (record == null) return ImplantRegion.Torso;
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
        private static LimbKind ClassifyLimb(List<BodyPartRecord> slotRecords)
        {
            BodyPartRecord? record = slotRecords.Count > 0 ? slotRecords[0] : null;
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

        /// Whether any surgery recipe applies to ordinary humanlikes: a
        /// recipe with a mutant prerequisite is refused for non-mutants by
        /// Recipe_Surgery.AvailableOnNow.
        private static bool AnyHumanRecipe(List<RecipeDef> surgeryRecipes)
        {
            for (int i = 0; i < surgeryRecipes.Count; i++)
                if (surgeryRecipes[i].mutantPrerequisite.NullOrEmpty())
                    return true;
            return false;
        }

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
