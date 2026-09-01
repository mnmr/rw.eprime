using System;
using System.Collections.Generic;
using Implanner.Core;
using RimShared.Common;
using RimWorld;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner
{
    /// Production automation inside the deterministic reconciliation pass:
    /// crafting bills for implant items the colony still needs. Runs only
    /// from PlannerReconciler's synchronized tick path, consumes only
    /// authoritative synchronized state, and takes all colony structure from
    /// the pass's ColonyIndex, so every multiplayer client derives identical
    /// bills.
    ///
    /// Dispatch rules:
    /// - demand = missing (unblocked) implant slots on the
    ///   colony's assigned pawns needing the item, minus unforbidden stock,
    ///   minus the pending output of Implanner-owned production bills;
    ///   demand and stock are items, bills are crafts, and
    ///   ProductionMath.CraftsNeeded is the single conversion point;
    /// - a bill is created only when, for every fixed ingredient, stock minus
    ///   the bill's full cost stays at or above the player's reserve;
    /// - at most ProductionConcurrency benches per colony hold Implanner
    ///   bills, one bill per bench, ordered by star tier, then the
    ///   player-arranged tier position, then defName;
    /// - the bench cap is also the planning horizon: only the first
    ///   ProductionConcurrency deficit kinds per colony (in that order) are
    ///   planned per pass — production may build ahead of the surgery
    ///   batch, but never computes further ahead than the bill cap, so
    ///   intermediary expansion and reserve checks stay bounded;
    /// - a bill whose item is no longer demanded at its colony is deleted.
    ///
    /// Cadence: the owner-approved 1020-game-tick boundary for resource-gated
    /// production dispatch, plus an immediate pass after a production-domain
    /// mutation (options edited). The pass's own bill bookkeeping is folded
    /// into the observed revision by NotePassCompleted, so it never counts
    /// as an option edit. Bill objects belong to the game; the model only
    /// records which bills Implanner created.
    internal static class PlannerProduction
    {
        /// The production dispatch boundary approved in AGENTS.md.
        private static readonly FixedTickBoundaryGate boundary =
            new FixedTickBoundaryGate(1020);
        private static int observedProductionVersion = -1;
        private static bool ranThisPass;

        // Cache contract:
        // Owner: process/loaded def set.
        // Key: implant item ThingDef identity.
        // Value: the deterministic production recipe (lowest defName among
        //   non-surgery recipes producing the item), or null when the item
        //   cannot be crafted; observed defs are never mutated.
        // Dependencies: the loaded definition set (static per session).
        // Refresh policy: built lazily per def on first demand.
        // Equality policy: entries never change within a session.
        // Teardown: Reset clears the map (world teardown; defensive only).
        private static readonly Dictionary<ThingDef, RecipeDef?> productionRecipes =
            new Dictionary<ThingDef, RecipeDef?>();

        internal static void Reset()
        {
            boundary.Reset();
            observedProductionVersion = -1;
            ranThisPass = false;
            productionRecipes.Clear();
        }

        /// Called by the reconciler AFTER it bumps the pass's aggregated
        /// change: folds this pass's own bill bookkeeping into the observed
        /// production revision so the next reconcile trigger does not
        /// mistake it for a player option edit and re-run the full dispatch
        /// scan ahead of the 1020-tick boundary. Nothing else can mutate the
        /// store between the pass and this call — both run on the same tick
        /// inside the same synchronized call stack.
        internal static void NotePassCompleted(ImplannerStore store)
        {
            if (!ranThisPass) return;
            ranThisPass = false;
            observedProductionVersion = store.ProductionVersion;
        }

        /// The deterministic crafting recipe for an implant item: the lowest
        /// defName among non-surgery recipes producing it, or null when the
        /// item cannot be crafted. Builder path only.
        internal static RecipeDef? ProductionRecipeFor(ThingDef itemDef)
        {
            if (productionRecipes.TryGetValue(itemDef, out RecipeDef? known))
                return known;
            RecipeDef? best = null;
            List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDef recipe = recipes[i];
                if (recipe.IsSurgery || recipe.ProducedThingDef != itemDef) continue;
                if (best == null
                    || string.CompareOrdinal(recipe.defName, best.defName) < 0)
                    best = recipe;
            }
            productionRecipes[itemDef] = best;
            return best;
        }

        internal static PlannerChange Reconcile(
            ImplannerStore store, ColonyIndex index)
        {
            PlannerModel model = store.Model;
            if (model.AutomationPaused || !model.AutoProduction)
                return PlannerChange.None;

            bool boundaryHit = boundary.Observe(Find.TickManager.TicksGame);
            bool optionsDirty = store.ProductionVersion != observedProductionVersion;
            if (!boundaryHit && !optionsDirty) return PlannerChange.None;
            ranThisPass = true;

            var change = PlannerChange.None;

            // Resolve every owned bill once: benches scanned per colony.
            var resolvedBills =
                new Dictionary<string, (Bill_Production Bill, Map Colony)>(
                    StringComparer.Ordinal);
            var benchesByColony = new Dictionary<Map, List<Building_WorkTable>>();
            for (int c = 0; c < index.Colonies.Count; c++)
            {
                Colony colony = index.Colonies[c];
                for (int m = 0; m < colony.Maps.Count; m++)
                {
                    List<Building> benches =
                        colony.Maps[m].listerBuildings.allBuildingsColonist;
                    for (int b = 0; b < benches.Count; b++)
                    {
                        if (!(benches[b] is Building_WorkTable bench)) continue;
                        if (!benchesByColony.TryGetValue(
                                colony.CanonicalMap, out var list))
                        {
                            list = new List<Building_WorkTable>();
                            benchesByColony.Add(colony.CanonicalMap, list);
                        }
                        list.Add(bench);
                        BillStack bills = bench.BillStack;
                        for (int i = 0; i < bills.Count; i++)
                            if (bills[i] is Bill_Production production
                                && model.OwnedProductionBills.ContainsKey(
                                    production.GetUniqueLoadID()))
                                resolvedBills[production.GetUniqueLoadID()] =
                                    (production, colony.CanonicalMap);
                    }
                }
            }
            foreach (var list in benchesByColony.Values)
                list.Sort(static (a, b) =>
                    a.thingIDNumber.CompareTo(b.thingIDNumber));

            // Records whose bill no longer exists anywhere (completed bills
            // delete themselves at repeat count zero) are forgotten.
            var recordIds = new List<string>(model.OwnedProductionBills.Keys);
            recordIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < recordIds.Count; i++)
                if (!resolvedBills.ContainsKey(recordIds[i]))
                    change |= model.RemoveOwnedProductionBill(recordIds[i]);

            // Demand per colony per item def, in items: missing slots
            // wanting the item.
            var demand = new Dictionary<(Map, ThingDef), int>();
            var pawnIds = new List<int>(model.Assignments.Keys);
            pawnIds.Sort();
            for (int i = 0; i < pawnIds.Count; i++)
            {
                int pawnId = pawnIds[i];
                if (!index.PawnsById.TryGetValue(pawnId, out Pawn pawn)) continue;
                Colony? colony = index.ColonyOfPawn(pawnId);
                if (colony == null) continue;
                Plan? plan = model.AssignedPlan(pawnId);
                if (plan == null) continue;
                List<ImplantGoal> goals = model.EffectiveImplants(plan);
                if (goals.Count == 0) continue;
                List<string> missing = PawnProjection.MissingImplantSlotKeys(
                    pawn, goals);
                for (int k = 0; k < missing.Count; k++)
                {
                    if (!GoalKeys.TryResolveImplantSlot(
                            goals, missing[k], out ImplantGoal goal, out _))
                        continue;
                    ImplantCatalogEntry? entry =
                        Catalogs.ImplantByDefName(goal.ImplantDefName);
                    ThingDef? item = entry?.Def.spawnThingOnRemoved;
                    if (item == null || ProductionRecipeFor(item) == null) continue;
                    var key = (colony.CanonicalMap, item);
                    demand.TryGetValue(key, out int count);
                    demand[key] = count + 1;
                }
            }

            // Unforbidden stock per colony per def, in items. Items the
            // player holds back from surgery automation (implant
            // reservations) do not count as available: production keeps
            // building until surgery can proceed without dipping into the
            // held-back stock.
            var stock = new Dictionary<(Map, ThingDef), int>();
            for (int c = 0; c < index.Colonies.Count; c++)
            {
                Colony colony = index.Colonies[c];
                for (int i = 0; i < colony.ItemIds.Count; i++)
                {
                    Thing thing = index.ItemsById[colony.ItemIds[i]];
                    if (thing.IsForbidden(Faction.OfPlayer)) continue;
                    var key = (colony.CanonicalMap, thing.def);
                    stock.TryGetValue(key, out int count);
                    stock[key] = count + thing.stackCount;
                }
            }
            Dictionary<ThingDef, int> playerReserves =
                PlannerSurgery.ImplantItemReserves(model);
            if (playerReserves.Count > 0)
            {
                var stockKeys = new List<(Map, ThingDef)>(stock.Keys);
                for (int i = 0; i < stockKeys.Count; i++)
                    if (playerReserves.TryGetValue(stockKeys[i].Item2, out int held))
                        stock[stockKeys[i]] =
                            Math.Max(0, stock[stockKeys[i]] - held);
            }

            // Pending crafts of live owned bills, and per-colony bench usage.
            var pending = new Dictionary<(Map, ThingDef), int>();
            var busyBenches = new Dictionary<Map, int>();
            foreach (KeyValuePair<string, (Bill_Production Bill, Map Colony)>
                pair in resolvedBills)
            {
                Bill_Production bill = pair.Value.Bill;
                ThingDef? produced = bill.recipe.ProducedThingDef;
                if (produced != null)
                {
                    var key = (pair.Value.Colony, produced);
                    pending.TryGetValue(key, out int count);
                    pending[key] = count + Math.Max(bill.repeatCount, 0);
                }
                busyBenches.TryGetValue(pair.Value.Colony, out int busy);
                busyBenches[pair.Value.Colony] = busy + 1;
            }

            // Deficits per colony per def, converted to CRAFTS here — the
            // only place items meet crafts — and ordered by the implant's
            // tier placement (star tier, then the player-arranged position)
            // so the panel's arrangement drives what gets built first.
            var deficits = new List<(Map Colony, ThingDef Item, int Count)>();
            foreach (KeyValuePair<(Map, ThingDef), int> pair in demand)
            {
                stock.TryGetValue(pair.Key, out int held);
                pending.TryGetValue(pair.Key, out int queued);
                RecipeDef recipe = ProductionRecipeFor(pair.Key.Item2)!;
                int crafts = ProductionMath.CraftsNeeded(pair.Value, held,
                    queued, OutputCount(recipe, pair.Key.Item2));
                if (crafts > 0)
                    deficits.Add((pair.Key.Item1, pair.Key.Item2, crafts));
            }
            var rankByItem = new Dictionary<ThingDef, (int Tier, int Order)>();
            IReadOnlyList<ImplantCatalogEntry> catalogEntries = Catalogs.Implants();
            for (int i = 0; i < catalogEntries.Count; i++)
            {
                ThingDef? produced = catalogEntries[i].Def.spawnThingOnRemoved;
                if (produced == null) continue;
                string hediff = catalogEntries[i].Def.defName;
                var rank = (StarRanking.TierOf(model.ImplantStarsOf(hediff)),
                    model.ImplantOrderOf(hediff));
                if (!rankByItem.TryGetValue(produced, out var existing)
                    || rank.CompareTo(existing) < 0)
                    rankByItem[produced] = rank;
            }
            deficits.Sort((a, b) =>
            {
                int colony = a.Colony.uniqueID.CompareTo(b.Colony.uniqueID);
                if (colony != 0) return colony;
                var maxRank = (int.MaxValue, int.MaxValue);
                if (!rankByItem.TryGetValue(a.Item, out var rankA)) rankA = maxRank;
                if (!rankByItem.TryGetValue(b.Item, out var rankB)) rankB = maxRank;
                int rank = rankA.CompareTo(rankB);
                if (rank != 0) return rank;
                return string.CompareOrdinal(a.Item.defName, b.Item.defName);
            });

            // Planning horizon: everything past the first ProductionConcurrency
            // kinds per colony cannot receive a bench this pass, so it is not
            // planned at all — later kinds enter as earlier ones complete.
            // Demand stays complete above: bill cancellation must still see
            // every wanted kind, and repeat counts cover the full demand of
            // the kinds that ARE planned.
            var plannedPerColony = new Dictionary<Map, int>();
            int kept = 0;
            for (int i = 0; i < deficits.Count; i++)
            {
                plannedPerColony.TryGetValue(deficits[i].Colony, out int taken);
                if (taken >= model.ProductionConcurrency) continue;
                plannedPerColony[deficits[i].Colony] = taken + 1;
                deficits[kept++] = deficits[i];
            }
            deficits.RemoveRange(kept, deficits.Count - kept);

            // Intermediaries: a deficit blocked by an ingredient shortfall
            // may spawn bills for the missing craftable ingredients, walked
            // to full depth (the vanilla tree is shallow — components and
            // advanced components — and a cap bounds modded recipe cycles).
            var intermediaryNeeds = new Dictionary<(Map, ThingDef), int>();
            if (model.AllowIntermediaries)
                ExpandIntermediaryNeeds(model, index, deficits, pending,
                    intermediaryNeeds);

            // Cancel owned bills whose item is no longer needed at their
            // colony (plan edits or delivery removed the demand, or the
            // intermediary shortfall resolved).
            for (int i = 0; i < recordIds.Count; i++)
            {
                string billId = recordIds[i];
                if (!resolvedBills.TryGetValue(billId, out var resolved)) continue;
                ThingDef? produced = resolved.Bill.recipe.ProducedThingDef;
                if (produced != null)
                {
                    var key = (resolved.Colony, produced);
                    if ((demand.TryGetValue(key, out int wanted) && wanted > 0)
                        || intermediaryNeeds.ContainsKey(key))
                        continue;
                }
                resolved.Bill.billStack.Delete(resolved.Bill);
                busyBenches.TryGetValue(resolved.Colony, out int busy);
                busyBenches[resolved.Colony] = busy - 1;
                change |= model.RemoveOwnedProductionBill(billId);
            }

            // Create bills — implant items first, then intermediaries — in
            // defName order, while the colony has free bench slots under the
            // concurrency cap. Every Count is crafts by now.
            var intermediaries = new List<(Map Colony, ThingDef Item, int Count)>();
            foreach (KeyValuePair<(Map, ThingDef), int> pair in intermediaryNeeds)
                intermediaries.Add((pair.Key.Item1, pair.Key.Item2, pair.Value));
            intermediaries.Sort(ByColonyThenDef);
            deficits.AddRange(intermediaries);

            for (int i = 0; i < deficits.Count; i++)
            {
                (Map colony, ThingDef item, int count) = deficits[i];
                busyBenches.TryGetValue(colony, out int busy);
                if (busy >= model.ProductionConcurrency) continue;
                RecipeDef? recipe = ProductionRecipeFor(item);
                if (recipe == null || !recipe.AvailableNow) continue;
                if (!ReservesAllow(model, index, colony, recipe, count)) continue;
                Building_WorkTable? bench = FindFreeBench(
                    benchesByColony, model, colony, recipe);
                if (bench == null) continue;

                var bill = new Bill_Production(recipe)
                {
                    repeatMode = BillRepeatModeDefOf.RepeatCount,
                    repeatCount = count,
                    allowedSkillRange = new IntRange(
                        model.ProductionSkill, PlannerModel.DoctorFloorMax),
                };
                bench.BillStack.AddBill(bill);
                busyBenches[colony] = busy + 1;
                change |= model.SetOwnedProductionBill(
                    bill.GetUniqueLoadID(), item.defName);
            }

            return change;
        }

        private static readonly Comparison<(Map Colony, ThingDef Item, int Count)>
            ByColonyThenDef = static (a, b) =>
            {
                int colony = a.Colony.uniqueID.CompareTo(b.Colony.uniqueID);
                if (colony != 0) return colony;
                return string.CompareOrdinal(a.Item.defName, b.Item.defName);
            };

        /// Whether every fixed ingredient keeps at least its configured
        /// reserve in stock after the bill's full cost (crafts × per-craft
        /// cost) is deducted. Stock is summed across the colony's maps via
        /// the per-map resource counters.
        private static bool ReservesAllow(PlannerModel model,
            ColonyIndex index, Map colony, RecipeDef recipe, int crafts)
        {
            List<IngredientCount>? ingredients = recipe.ingredients;
            if (ingredients == null) return true;
            for (int i = 0; i < ingredients.Count; i++)
            {
                IngredientCount ingredient = ingredients[i];
                if (!ingredient.IsFixedIngredient) continue;
                ThingDef def = ingredient.FixedIngredient;
                int needed = (int)Math.Ceiling(
                    ingredient.GetBaseCount() * crafts);
                int available = ColonyResourceCount(index, colony, def);
                if (available - needed < model.ResourceReserveOf(def.defName))
                    return false;
            }
            return true;
        }

        /// Walks the production tree to full depth: shortfall amounts
        /// accumulate per colony and resource, each resource expands once,
        /// and the depth cap bounds pathological modded recipe cycles. The
        /// needed crafts subtract what our bills already have pending;
        /// second-parent contributions arriving after a resource expanded
        /// are corrected by the next boundary pass.
        private static void ExpandIntermediaryNeeds(PlannerModel model,
            ColonyIndex index,
            List<(Map Colony, ThingDef Item, int Count)> deficits,
            Dictionary<(Map, ThingDef), int> pending,
            Dictionary<(Map, ThingDef), int> needs)
        {
            const int MaxDepth = 8;
            var amounts = new Dictionary<(Map, ThingDef), int>();
            var frontier = new List<(Map, ThingDef)>();
            for (int i = 0; i < deficits.Count; i++)
                AccumulateShortfalls(model, index, deficits[i].Colony,
                    ProductionRecipeFor(deficits[i].Item), deficits[i].Count,
                    amounts, frontier);

            var expanded = new HashSet<(Map, ThingDef)>();
            for (int depth = 0; depth < MaxDepth && frontier.Count > 0; depth++)
            {
                frontier.Sort(ByColonyThenDefKey);
                var next = new List<(Map, ThingDef)>();
                for (int i = 0; i < frontier.Count; i++)
                {
                    (Map, ThingDef) key = frontier[i];
                    if (!expanded.Add(key)) continue;
                    RecipeDef? recipe = ProductionRecipeFor(key.Item2);
                    if (recipe == null || !recipe.AvailableNow) continue;
                    int output = OutputCount(recipe, key.Item2);
                    if (output <= 0) continue;
                    pending.TryGetValue(key, out int queued);
                    int units = (amounts[key] + output - 1) / output - queued;
                    if (units <= 0) continue;
                    needs[key] = units;
                    AccumulateShortfalls(model, index, key.Item1, recipe, units,
                        amounts, next);
                }
                frontier = next;
            }
        }

        /// Whether intermediary production may craft this ingredient:
        /// manufactured items only (the Manufactured thing category —
        /// components, advanced components, and modded kin). Raw resources
        /// never receive bills, no matter what recycling or smelting recipe
        /// could produce them: a steel shortfall is the player's to mine or
        /// trade, never a slag-smelting bill.
        internal static bool IsManufactured(ThingDef def)
        {
            List<ThingCategoryDef>? categories = def.thingCategories;
            if (categories == null) return false;
            for (int i = 0; i < categories.Count; i++)
                for (ThingCategoryDef? c = categories[i]; c != null; c = c.parent)
                    if (c == ThingCategoryDefOf.Manufactured)
                        return true;
            return false;
        }

        /// Adds each fixed-ingredient shortfall (the bill's cost plus the
        /// resource's reserve beyond current stock) to the per-resource
        /// amounts, queueing craftable MANUFACTURED resources for expansion.
        private static void AccumulateShortfalls(PlannerModel model,
            ColonyIndex index, Map colony, RecipeDef? recipe, int crafts,
            Dictionary<(Map, ThingDef), int> amounts,
            List<(Map, ThingDef)> frontier)
        {
            List<IngredientCount>? ingredients = recipe?.ingredients;
            if (ingredients == null) return;
            for (int i = 0; i < ingredients.Count; i++)
            {
                IngredientCount ingredient = ingredients[i];
                if (!ingredient.IsFixedIngredient) continue;
                ThingDef def = ingredient.FixedIngredient;
                int needed = (int)Math.Ceiling(ingredient.GetBaseCount() * crafts);
                int shortfall = needed + model.ResourceReserveOf(def.defName)
                    - ColonyResourceCount(index, colony, def);
                if (shortfall <= 0) continue;
                if (!IsManufactured(def) || ProductionRecipeFor(def) == null)
                    continue;
                var key = (colony, def);
                amounts.TryGetValue(key, out int existing);
                amounts[key] = existing + shortfall;
                frontier.Add(key);
            }
        }

        private static readonly Comparison<(Map, ThingDef)> ByColonyThenDefKey =
            static (a, b) =>
            {
                int colony = a.Item1.uniqueID.CompareTo(b.Item1.uniqueID);
                if (colony != 0) return colony;
                return string.CompareOrdinal(a.Item2.defName, b.Item2.defName);
            };

        /// Items of def one craft of the recipe yields. Builder path only.
        internal static int OutputCount(RecipeDef recipe, ThingDef def)
        {
            List<ThingDefCountClass>? products = recipe.products;
            if (products == null) return 0;
            for (int i = 0; i < products.Count; i++)
                if (products[i].thingDef == def)
                    return products[i].count;
            return 0;
        }

        private static int ColonyResourceCount(
            ColonyIndex index, Map canonical, ThingDef def)
        {
            Colony? colony = index.ByCanonicalMap(canonical);
            if (colony == null) return 0;
            int total = 0;
            for (int m = 0; m < colony.Maps.Count; m++)
                total += colony.Maps[m].resourceCounter.GetCount(def);
            return total;
        }

        /// The lowest-id spawned bench at the colony that can work the recipe,
        /// has bill-stack room, and holds no Implanner bill yet (one owned
        /// bill per bench keeps the concurrency cap meaningful). With the
        /// idle-benches option, a bench also qualifies only when none of its
        /// bills currently wants work (Bill.ShouldDoNow) — suspended bills
        /// and satisfied do-until-X bills leave the bench idle.
        private static Building_WorkTable? FindFreeBench(
            Dictionary<Map, List<Building_WorkTable>> benchesByColony,
            PlannerModel model, Map colony, RecipeDef recipe)
        {
            if (!benchesByColony.TryGetValue(colony, out var benches)) return null;
            // AllRecipeUsers covers both recipeUsers on the recipe and
            // recipes listed on the bench def.
            var users = new HashSet<ThingDef>();
            foreach (ThingDef user in recipe.AllRecipeUsers)
                users.Add(user);
            for (int i = 0; i < benches.Count; i++)
            {
                Building_WorkTable bench = benches[i];
                if (!users.Contains(bench.def))
                    continue;
                BillStack bills = bench.BillStack;
                if (bills.Count >= 15) continue;
                bool blocked = false;
                for (int b = 0; b < bills.Count && !blocked; b++)
                    blocked = model.OwnedProductionBills.ContainsKey(
                            bills[b].GetUniqueLoadID())
                        || (model.OnlyIdleBenches && bills[b].ShouldDoNow());
                if (!blocked) return bench;
            }
            return null;
        }
    }
}
