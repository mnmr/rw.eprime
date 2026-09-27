using System;
using System.Collections.Generic;
using HarmonyLib;
using Implanner.Core;
using RimShared.Common;
using RimWorld;
using Verse;

namespace Implanner
{
    /// Production automation inside the deterministic reconciliation pass:
    /// crafting bills for implant items the colony still needs. Runs only
    /// from PlannerReconciler's synchronized tick path, consumes only
    /// authoritative synchronized state, and takes all colony structure and
    /// per-pawn evaluations from the pass, so every multiplayer client
    /// derives identical bills.
    ///
    /// Dispatch rules (owner, 2026-09-26), per colony:
    /// - every bill is ONE craft; a bench holds at most
    ///   PlannerModel.BillsPerBench Implanner bills (the next craft is queued
    ///   when the first completes), and at most ProductionConcurrency benches
    ///   hold any;
    /// - crafts follow the surgery rollout order (SurgeryPlanner.Order over
    ///   every missing slot: tier batching tier by tier, full sets colonist
    ///   by colonist, each in plan-ranked order); stock, then pending bills,
    ///   cover the earliest slots and every slot left is one craft
    ///   (ProductionQueue.UncoveredCrafts);
    /// - a craft is queued only when stock covers its cost plus the
    ///   materials promised to queued (not started) Implanner bills plus the
    ///   player's reserve (ProductionBudget); a craft that does not fit is
    ///   skipped and later crafts still proceed; an affordable craft
    ///   without a free bench still promises its materials, so rollout
    ///   order decides who gets them;
    /// - queued bills promise their materials oldest first, and one that no
    ///   longer fits (the player spent the stock below a reserve) is
    ///   cancelled until stock allows it again;
    /// - bench slots left over build the manufactured ingredients the first
    ///   skipped crafts will need (BuildAhead), so advanced components are
    ///   ready when a missing raw resource arrives;
    /// - benches the player designated for Implanner take bills first; the
    ///   others serve only while OnlyDesignatedBenches is off
    ///   (PlannerModel.ChooseProductionBench);
    /// - with AllowIntermediaries, the first craft in rollout order blocked
    ///   by a manufactured ingredient sets that ingredient's need: enough
    ///   one-craft bills to cover the shortfall, recursively (depth-capped);
    /// - queued bills nobody needs any more are withdrawn; started ones
    ///   finish.
    ///
    /// Cadence: the owner-approved 1020-game-tick boundary for resource-gated
    /// production dispatch (the pass's BoundaryHit, pure tick arithmetic),
    /// plus an early dispatch on the pass after a production-domain
    /// mutation (options edited) via the store's scribed
    /// PendingProductionPass flag, which only the synced command path sets
    /// and this phase clears — the pass's own bill bookkeeping publishes
    /// without setting it. Bill objects belong to the game; the model only
    /// records which bills Implanner created.
    internal static class PlannerProduction
    {
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

        // Cache contract:
        // Owner: process/loaded def set.
        // Key: RecipeDef identity.
        // Value: the set of bench ThingDefs that can work the recipe (the
        //   recipe's own recipeUsers plus every ThingDef listing it —
        //   RecipeDef.AllRecipeUsers, which walks the whole ThingDef
        //   database per call); immutable once built, observed defs never
        //   mutated.
        // Dependencies: the loaded definition set (static per session).
        // Refresh policy: built lazily per recipe on first demand.
        // Equality policy: entries never change within a session.
        // Teardown: Reset clears the map (world teardown; defensive only).
        private static readonly Dictionary<RecipeDef, HashSet<ThingDef>> recipeUsers =
            new Dictionary<RecipeDef, HashSet<ThingDef>>();

        /// Depth cap for intermediary chains (modded recipe cycles).
        private const int MaxIntermediaryDepth = 8;

        /// Bill.loadID is private; it increases with every bill the game
        /// creates, so it orders Implanner's bills oldest first.
        private static readonly AccessTools.FieldRef<Bill, int> LoadId =
            AccessTools.FieldRefAccess<Bill, int>("loadID");

        internal static void Reset()
        {
            productionRecipes.Clear();
            recipeUsers.Clear();
            implantBenches = null;
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

        /// The bench defs that can work the recipe, resolved once per
        /// recipe per session. Builder path only.
        private static HashSet<ThingDef> RecipeUsersOf(RecipeDef recipe)
        {
            if (recipeUsers.TryGetValue(recipe, out HashSet<ThingDef> users))
                return users;
            users = new HashSet<ThingDef>();
            foreach (ThingDef user in recipe.AllRecipeUsers)
                users.Add(user);
            recipeUsers.Add(recipe, users);
            return users;
        }

        internal static PlannerChange Reconcile(
            ImplannerStore store, ReconcilePass pass)
        {
            PlannerModel model = store.Model;
            if (model.AutomationPaused || !model.AutoProduction)
                return PlannerChange.None;

            if (!pass.BoundaryHit && !store.PendingProductionPass)
                return PlannerChange.None;
            store.ClearPendingProductionPass();
            ColonyIndex index = pass.Index;
            var change = PlannerChange.None;

            // Resolve every owned bill once, per colony, in bench-id then
            // stack order.
            var colonies = new List<ColonyDispatch>(index.Colonies.Count);
            var live = new HashSet<string>(StringComparer.Ordinal);
            for (int c = 0; c < index.Colonies.Count; c++)
            {
                var dispatch = new ColonyDispatch(model, pass, index.Colonies[c]);
                dispatch.ResolveBenches(live);
                colonies.Add(dispatch);
            }

            // Records whose bill no longer exists anywhere (completed bills
            // delete themselves at repeat count zero) are forgotten — but
            // only when every bench is where it can be seen. While a
            // gravship is in flight its benches are unspawned and held by
            // the ship, so nothing is forgotten until it lands and the
            // bills are visible again (a forgotten record would let demand
            // queue a second bill on landing).
            if (Find.CurrentGravship == null)
            {
                var recordIds = new List<string>(model.OwnedProductionBills.Keys);
                recordIds.Sort(StringComparer.Ordinal);
                for (int i = 0; i < recordIds.Count; i++)
                    if (!live.Contains(recordIds[i]))
                        change |= model.RemoveOwnedProductionBill(recordIds[i]);
            }

            Dictionary<ThingDef, int> playerReserves =
                PlannerSurgery.ImplantItemReserves(model);
            for (int c = 0; c < colonies.Count; c++)
                change |= colonies[c].Run(playerReserves);
            return change;
        }

        /// One colony's dispatch for one pass: its benches and Implanner
        /// bills, the rollout-ordered crafts, and the material budget.
        /// Pass-scoped, discarded with the pass.
        private sealed class ColonyDispatch
        {
            private readonly PlannerModel model;
            private readonly ReconcilePass pass;
            private readonly Colony colony;

            /// Worktables at the colony, thing-id order.
            private readonly List<Building_WorkTable> benches = new List<Building_WorkTable>();

            /// Live Implanner bills with their bench, bench-id then stack order.
            private readonly List<(Bill_Production Bill, Building_WorkTable Bench)> owned =
                new List<(Bill_Production, Building_WorkTable)>();

            private readonly HashSet<Bill> inProgress =
                new HashSet<Bill>(ReferenceIdentityComparer<Bill>.Instance);
            private readonly Dictionary<Building_WorkTable, int> billsOnBench =
                new Dictionary<Building_WorkTable, int>();
            private readonly Dictionary<Building_WorkTable, bool> otherWork =
                new Dictionary<Building_WorkTable, bool>();
            private readonly ProductionBudget<ThingDef> budget = new ProductionBudget<ThingDef>();
            private readonly Dictionary<ThingDef, int> intermediaryNeed =
                new Dictionary<ThingDef, int>();
            private readonly HashSet<ThingDef> expanded = new HashSet<ThingDef>();
            private readonly Dictionary<ThingDef, int> pendingCrafts =
                new Dictionary<ThingDef, int>();
            private int startedBenches;
            private PlannerChange change;

            internal ColonyDispatch(PlannerModel model, ReconcilePass pass, Colony colony)
            {
                this.model = model;
                this.pass = pass;
                this.colony = colony;
            }

            internal void ResolveBenches(HashSet<string> live)
            {
                for (int m = 0; m < colony.Maps.Count; m++)
                {
                    List<Building> buildings =
                        colony.Maps[m].listerBuildings.allBuildingsColonist;
                    for (int b = 0; b < buildings.Count; b++)
                        if (buildings[b] is Building_WorkTable bench)
                            benches.Add(bench);
                }
                benches.Sort(static (a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
                for (int b = 0; b < benches.Count; b++)
                {
                    BillStack bills = benches[b].BillStack;
                    for (int i = 0; i < bills.Count; i++)
                    {
                        if (!(bills[i] is Bill_Production production)) continue;
                        string billId = pass.BillId(production);
                        if (!model.OwnedProductionBills.ContainsKey(billId)) continue;
                        ModCompatibility.RepairBillSkillMaximum(production);
                        owned.Add((production, benches[b]));
                        live.Add(billId);
                    }
                }
            }

            internal PlannerChange Run(Dictionary<ThingDef, int> playerReserves)
            {
                CollectInProgress(colony.Maps, inProgress);

                // Completed bills: vanilla leaves a finished "do X times"
                // bill on the bench at 0 (Bill_Production.Notify_IterationCompleted),
                // where it would hold one of the bench's Implanner slots.
                for (int i = 0; i < owned.Count; i++)
                {
                    Bill_Production bill = owned[i].Bill;
                    if (bill.repeatCount > 0 || inProgress.Contains(bill)) continue;
                    bill.billStack.Delete(bill);
                    change |= model.RemoveOwnedProductionBill(pass.BillId(bill));
                }
                PruneDeleted();

                // Rollout-ordered demand: one entry per missing slot whose
                // item the colony can craft, with the lowest item quality
                // the slot accepts.
                List<CraftNeed<ThingDef>> rollout = Rollout();
                var demanded = new HashSet<ThingDef>();
                for (int i = 0; i < rollout.Count; i++) demanded.Add(rollout[i].Item);

                // Stock as one quality per item (unforbidden, minus the
                // player's implant reservations: production keeps building
                // until surgery can proceed without dipping into held-back
                // stock; allocation takes the best items, so the held-back
                // ones are the worst), the quality each pending craft
                // promises, and output per craft.
                var stock = new Dictionary<ThingDef, List<int>>();
                var promises = new Dictionary<ThingDef, List<int>>();
                var output = new Dictionary<ThingDef, int>();
                foreach (ThingDef item in demanded)
                {
                    List<int> qualities = FreeStock(item);
                    qualities.Sort();
                    if (playerReserves.TryGetValue(item, out int held))
                        qualities.RemoveRange(0, Math.Min(held, qualities.Count));
                    if (qualities.Count > 0) stock[item] = qualities;
                    RecipeDef? recipe = ProductionRecipeFor(item);
                    output[item] = recipe != null ? OutputCount(recipe, item) : 0;
                }
                for (int i = 0; i < owned.Count; i++)
                {
                    ThingDef? produced = owned[i].Bill.recipe.ProducedThingDef;
                    if (produced == null) continue;
                    int crafts = Math.Max(owned[i].Bill.repeatCount, 0);
                    pendingCrafts.TryGetValue(produced, out int pending);
                    pendingCrafts[produced] = pending + crafts;
                    if (!promises.TryGetValue(produced, out List<int> list))
                        promises[produced] = list = new List<int>();
                    int promise = model.ProductionBillQualityOf(pass.BillId(owned[i].Bill));
                    for (int n = 0; n < crafts; n++) list.Add(promise);
                }

                var used = new Dictionary<ThingDef, List<int>>();
                List<CraftNeed<ThingDef>> queue = ProductionQueue.UncoveredCrafts(
                    rollout, stock, promises, output, used);

                // Implant bills beyond what the demand uses are withdrawn
                // before anything is promised or placed.
                WithdrawImplants(demanded, used);

                // Started bills keep their bench slot and promise nothing:
                // their materials are already in hand. Queued bills promise
                // their materials oldest first; one whose materials no
                // longer fit above the reserves (the player spent them) is
                // cancelled, and returns once stock allows again.
                var queued = new List<(Bill_Production Bill, Building_WorkTable Bench)>();
                for (int i = 0; i < owned.Count; i++)
                {
                    if (inProgress.Contains(owned[i].Bill)) TakeSlot(owned[i].Bench);
                    else queued.Add(owned[i]);
                }
                queued.Sort(static (a, b) => LoadId(a.Bill).CompareTo(LoadId(b.Bill)));
                for (int i = 0; i < queued.Count; i++)
                {
                    Bill_Production bill = queued[i].Bill;
                    if (budget.TryCommit(CostsOf(bill.recipe, Math.Max(bill.repeatCount, 1))))
                    {
                        TakeSlot(queued[i].Bench);
                        continue;
                    }
                    ThingDef? produced = bill.recipe.ProducedThingDef;
                    if (produced != null && pendingCrafts.TryGetValue(produced, out int left))
                        pendingCrafts[produced] = left - Math.Max(bill.repeatCount, 0);
                    bill.billStack.Delete(bill);
                    change |= model.RemoveOwnedProductionBill(pass.BillId(bill));
                }
                PruneDeleted();

                // Dispatch in rollout order. Every craft is examined even
                // when the benches are full, and an affordable craft
                // promises its materials even without a free bench, so what
                // counts as skipped (and every intermediary need) depends
                // on stock and rollout alone, never on bench capacity.
                var skipped = new List<ThingDef>();
                for (int i = 0; i < queue.Count; i++)
                    if (TryCraft(queue[i].Item, 0, queue[i].MinQuality) == Craft.Blocked)
                        skipped.Add(queue[i].Item);

                BuildAhead(skipped);
                Withdraw(demanded, intermediaryNeed, implants: false);
                return change;
            }

            private void TakeSlot(Building_WorkTable bench)
            {
                billsOnBench.TryGetValue(bench, out int n);
                billsOnBench[bench] = n + 1;
                if (n == 0) startedBenches++;
            }

            private void PruneDeleted()
            {
                for (int i = owned.Count - 1; i >= 0; i--)
                    if (owned[i].Bill.DeletedOrDereferenced) owned.RemoveAt(i);
            }

            /// Fallback for crafts skipped over a missing raw resource
            /// (owner, 2026-09-26): bench slots left after the rollout build
            /// the manufactured ingredients those crafts will need, in
            /// rollout order, so the slow ones (advanced components) are
            /// ready when the raw resource arrives. Bounded to the first
            /// ProductionConcurrency skipped crafts; each ingredient bill is
            /// one craft and must fit the budget like any other.
            private void BuildAhead(List<ThingDef> skipped)
            {
                if (!model.AllowIntermediaries || skipped.Count == 0) return;
                var requirement = new Dictionary<ThingDef, int>();
                var order = new List<ThingDef>();
                int horizon = Math.Min(skipped.Count, model.ProductionConcurrency);
                for (int i = 0; i < horizon; i++)
                {
                    RecipeDef? recipe = ProductionRecipeFor(skipped[i]);
                    if (recipe == null) continue;
                    List<(ThingDef, int)> costs = CostsOf(recipe, 1);
                    for (int k = 0; k < costs.Count; k++)
                    {
                        ThingDef ingredient = costs[k].Item1;
                        if (!IsManufactured(ingredient)
                            || ProductionRecipeFor(ingredient) == null) continue;
                        if (!requirement.TryGetValue(ingredient, out int sum))
                            order.Add(ingredient);
                        requirement[ingredient] = sum + costs[k].Item2;
                    }
                }
                for (int i = 0; i < order.Count; i++)
                {
                    ThingDef ingredient = order[i];
                    int perCraft = OutputCount(ProductionRecipeFor(ingredient)!, ingredient);
                    if (perCraft <= 0) continue;
                    int shortfall = budget.Shortfall(ingredient, requirement[ingredient]);
                    int wanted = (shortfall + perCraft - 1) / perCraft;
                    intermediaryNeed.TryGetValue(ingredient, out int need);
                    if (wanted > need) intermediaryNeed[ingredient] = wanted;
                    pendingCrafts.TryGetValue(ingredient, out int queued);
                    for (int n = queued; n < wanted; n++)
                        if (TryCraft(ingredient, 1) != Craft.Created) break;
                }
            }

            /// Every missing craftable slot of the colony's assigned
            /// colonists, as its item and the lowest quality it accepts, in
            /// surgery rollout order. A slot no quality can satisfy (the
            /// part is already better) asks for nothing.
            private List<CraftNeed<ThingDef>> Rollout()
            {
                bool asap = model.Iteration == IterationStrategy.Asap;
                var work = new List<SurgeryWorkItem>();
                var itemOf = new Dictionary<(int, string), CraftNeed<ThingDef>>();
                ColonyIndex index = pass.Index;
                for (int p = 0; p < colony.PawnIds.Count; p++)
                {
                    int pawnId = colony.PawnIds[p];
                    PawnEvaluation? evaluation = pass.Evaluate(pawnId);
                    if (evaluation == null) continue;
                    Pawn pawn = index.PawnsById[pawnId];
                    int priority = model.PriorityOf(pawnId);
                    SurgeryCandidate candidate = default;
                    bool sampled = !asap;
                    List<string> missing = evaluation.Missing;
                    for (int k = 0; k < missing.Count; k++)
                    {
                        if (!GoalKeys.TryResolveImplantSlot(evaluation.Goals,
                                missing[k], out ImplantGoal goal, out int ordinal))
                            continue;
                        ImplantCatalogEntry? entry =
                            Catalogs.ImplantByDefName(goal.ImplantDefName);
                        // An upgrade over its installed base wants no item.
                        ThingDef? item = entry != null
                            ? PawnProjection.RequiredItem(pawn, entry, ordinal)
                            : null;
                        if (item == null || ProductionRecipeFor(item) == null) continue;
                        BodyPartRecord? part =
                            PawnProjection.ResolveSlotPart(pawn, entry!, ordinal);
                        if (part == null) continue;
                        int minimum = PawnProjection.MinimumAcceptableQuality(
                            pawn, entry!, part, evaluation.Plan.MinQuality);
                        if (minimum == ImplantQuality.None) continue;
                        if (!sampled)
                        {
                            candidate = PawnProjection.CandidateOf(pawn);
                            sampled = true;
                        }
                        itemOf[(pawnId, missing[k])] = new CraftNeed<ThingDef>(item, minimum);
                        work.Add(new SurgeryWorkItem(pawnId, priority,
                            StarRanking.TierOf(model.ImplantStarsOf(goal.ImplantDefName)),
                            missing[k], goal.ImplantDefName, entry!.Limb, candidate,
                            model.ImplantOrderOf(goal.ImplantDefName)));
                    }
                }
                SurgeryPlanner.Order(work, model.Iteration);
                var rollout = new List<CraftNeed<ThingDef>>(work.Count);
                for (int i = 0; i < work.Count; i++)
                    rollout.Add(itemOf[(work[i].PawnId, work[i].GoalKey)]);
                return rollout;
            }

            /// Unforbidden items of the kind at the colony, one quality per
            /// item (0 without quality): spawned stacks, plus any a colony
            /// pawn is carrying. A crafter carries a finished implant to
            /// storage after its bill completes; counting only spawned items
            /// let that window queue a duplicate bill (observed in-game
            /// 2026-09-26: a second bionic arm crafted).
            private List<int> FreeStock(ThingDef item)
            {
                var qualities = new List<int>();
                List<int>? ids = colony.ItemIdsOf(item);
                if (ids != null)
                    for (int i = 0; i < ids.Count; i++)
                    {
                        Thing thing = pass.Index.ItemsById[ids[i]];
                        if (!thing.IsForbidden(Faction.OfPlayer)) AddItems(qualities, thing);
                    }
                Faction faction = ColonyScope.AuthoritativeFaction;
                for (int m = 0; m < colony.Maps.Count; m++)
                {
                    IReadOnlyList<Pawn> pawns = colony.Maps[m].mapPawns.AllPawnsSpawned;
                    for (int p = 0; p < pawns.Count; p++)
                    {
                        Thing? carried = pawns[p].carryTracker?.CarriedThing;
                        if (carried != null && carried.def == item && pawns[p].Faction == faction)
                            AddItems(qualities, carried);
                    }
                }
                return qualities;
            }

            private static void AddItems(List<int> qualities, Thing thing)
            {
                int quality = ImplantQualities.QualityOf(thing);
                for (int n = 0; n < thing.stackCount; n++) qualities.Add(quality);
            }

            /// Makes the new bill deliver at least the slot's minimum
            /// quality where that can be arranged, and returns the promise
            /// (ProductionQueue.UnknownQuality when none): an ingredient that
            /// decides quality (Vanilla Genetics Expanded's genoframes) is
            /// limited to that tier and up; otherwise Quality Jobs, when
            /// loaded, manages the bill at that target. Without either the
            /// crafter's roll decides, and a result below the minimum is
            /// simply crafted again.
            private static int PromiseQuality(Bill_Production bill, ThingDef item,
                int minQuality)
            {
                if (!ImplantQualities.HasQuality(item)) return ProductionQueue.UnknownQuality;
                if (IngredientQuality.Restrict(bill, minQuality)) return minQuality;
                if (minQuality <= ImplantQuality.Lowest) return ProductionQueue.UnknownQuality;
                return bill is Bill_ProductionWithUft uft
                    && QualityJobsBridge.ManageBill(uft, minQuality)
                    ? minQuality
                    : ProductionQueue.UnknownQuality;
            }

            private enum Craft { Created, NoBench, Blocked }

            /// One craft of the item. When it fits the budget its materials
            /// are promised, and a bill is created if a bench has room
            /// (NoBench otherwise). When it does not fit (Blocked), the
            /// first blocked craft in rollout order sets each short
            /// manufactured ingredient's need, once per pass: enough
            /// one-craft ingredient bills, pending ones counted, to cover
            /// the shortfall, queued right here in rollout order.
            private Craft TryCraft(ThingDef item, int depth, int minQuality = 0)
            {
                RecipeDef? recipe = ProductionRecipeFor(item);
                if (recipe == null || !recipe.AvailableNow) return Craft.Blocked;
                List<(ThingDef, int)> costs = CostsOf(recipe, 1);
                if (budget.TryCommit(costs))
                {
                    Building_WorkTable? bench = FindFreeBench(recipe);
                    if (bench == null) return Craft.NoBench;
                    Bill_Production bill = MakeBill(recipe, 1, model.ProductionSkill);
                    bench.BillStack.AddBill(bill);
                    TakeSlot(bench);
                    pendingCrafts.TryGetValue(item, out int pending);
                    pendingCrafts[item] = pending + 1;
                    string billId = pass.BillId(bill);
                    change |= model.SetOwnedProductionBill(billId, item.defName);
                    change |= model.SetProductionBillQuality(billId,
                        PromiseQuality(bill, item, minQuality));
                    return Craft.Created;
                }
                if (!model.AllowIntermediaries || depth >= MaxIntermediaryDepth)
                    return Craft.Blocked;
                for (int k = 0; k < costs.Count; k++)
                {
                    ThingDef ingredient = costs[k].Item1;
                    int shortfall = budget.Shortfall(ingredient, costs[k].Item2);
                    if (shortfall <= 0 || !IsManufactured(ingredient)) continue;
                    RecipeDef? sub = ProductionRecipeFor(ingredient);
                    if (sub == null || !expanded.Add(ingredient)) continue;
                    int perCraft = OutputCount(sub, ingredient);
                    if (perCraft <= 0) continue;
                    int wanted = (shortfall + perCraft - 1) / perCraft;
                    intermediaryNeed[ingredient] = wanted;
                    pendingCrafts.TryGetValue(ingredient, out int queued);
                    for (int n = queued; n < wanted; n++)
                        if (TryCraft(ingredient, depth + 1) != Craft.Created) break;
                }
                return Craft.Blocked;
            }

            /// Withdraws queued implant bills the rollout did not use: each
            /// bill is matched by its item and quality promise against the
            /// pending crafts ProductionQueue.UncoveredCrafts took, started
            /// bills first. Started bills always finish.
            private void WithdrawImplants(HashSet<ThingDef> demanded,
                Dictionary<ThingDef, List<int>> used)
            {
                var remaining = new Dictionary<ThingDef, List<int>>();
                foreach (KeyValuePair<ThingDef, List<int>> pair in used)
                    remaining[pair.Key] = new List<int>(pair.Value);
                for (int round = 0; round < 2; round++)
                    for (int i = 0; i < owned.Count; i++)
                    {
                        Bill_Production bill = owned[i].Bill;
                        bool started = inProgress.Contains(bill);
                        if (started != (round == 0)) continue;
                        ThingDef? produced = bill.recipe.ProducedThingDef;
                        if (produced == null || !demanded.Contains(produced)) continue;
                        int promise = model.ProductionBillQualityOf(pass.BillId(bill));
                        remaining.TryGetValue(produced, out List<int>? promises);
                        int crafts = Math.Max(bill.repeatCount, 0);
                        bool needed = true;
                        for (int n = 0; n < crafts && needed; n++)
                            needed = promises != null && promises.Remove(promise);
                        if (started || needed) continue;
                        bill.billStack.Delete(bill);
                        change |= model.RemoveOwnedProductionBill(pass.BillId(bill));
                    }
                PruneDeleted();
            }

            /// Withdraws queued Implanner bills beyond what is needed:
            /// implant items against the crafts the rollout used,
            /// intermediaries (items nobody plans to implant) against their
            /// need this pass. Started bills always finish.
            private void Withdraw(HashSet<ThingDef> demanded,
                Dictionary<ThingDef, int> needed, bool implants)
            {
                var remaining = new Dictionary<ThingDef, int>(needed);
                // Started bills count first, so only queued ones go.
                for (int round = 0; round < 2; round++)
                    for (int i = 0; i < owned.Count; i++)
                    {
                        Bill_Production bill = owned[i].Bill;
                        bool started = inProgress.Contains(bill);
                        if (started != (round == 0)) continue;
                        ThingDef? produced = bill.recipe.ProducedThingDef;
                        if (produced == null || demanded.Contains(produced) != implants)
                            continue;
                        remaining.TryGetValue(produced, out int left);
                        int crafts = Math.Max(bill.repeatCount, 0);
                        if (started || left >= crafts)
                        {
                            remaining[produced] = left - crafts;
                            continue;
                        }
                        bill.billStack.Delete(bill);
                        change |= model.RemoveOwnedProductionBill(pass.BillId(bill));
                    }
                PruneDeleted();
            }

            /// The bench at the colony that takes a new one-craft bill for
            /// the recipe (PlannerModel.ChooseProductionBench) among the
            /// benches that can work it and have bill-stack room.
            private Building_WorkTable? FindFreeBench(RecipeDef recipe)
            {
                // Every craft of the rollout is examined each pass; skip
                // the bench scan once no bench can take another bill.
                if (startedBenches >= model.ProductionConcurrency)
                {
                    bool anySecond = false;
                    foreach (int bills in billsOnBench.Values)
                        if (bills > 0 && bills < PlannerModel.BillsPerBench)
                        {
                            anySecond = true;
                            break;
                        }
                    if (!anySecond) return null;
                }
                HashSet<ThingDef> users = RecipeUsersOf(recipe);
                var candidates = new List<BenchCandidate>();
                var byId = new Dictionary<int, Building_WorkTable>();
                for (int i = 0; i < benches.Count; i++)
                {
                    Building_WorkTable bench = benches[i];
                    if (!users.Contains(bench.def)) continue;
                    if (bench.BillStack.Count >= BillStack.MaxCount) continue;
                    billsOnBench.TryGetValue(bench, out int mine);
                    if (mine >= PlannerModel.BillsPerBench) continue;
                    candidates.Add(new BenchCandidate(bench.thingIDNumber, mine,
                        HasOtherWork(bench)));
                    byId[bench.thingIDNumber] = bench;
                }
                if (candidates.Count == 0) return null;
                int chosen = model.ChooseProductionBench(candidates,
                    startedBenches < model.ProductionConcurrency);
                return chosen >= 0 ? byId[chosen] : null;
            }

            /// Whether any bill on the bench that Implanner does not own
            /// currently wants work (Bill.ShouldDoNow): suspended bills and
            /// satisfied do-until-X bills leave the bench idle.
            private bool HasOtherWork(Building_WorkTable bench)
            {
                if (otherWork.TryGetValue(bench, out bool known)) return known;
                bool any = false;
                BillStack bills = bench.BillStack;
                for (int b = 0; b < bills.Count && !any; b++)
                    any = !model.OwnedProductionBills.ContainsKey(pass.BillId(bills[b]))
                        && bills[b].ShouldDoNow();
                otherWork[bench] = any;
                return any;
            }

            /// Fixed-ingredient costs of crafts of the recipe in item units,
            /// as the game counts them (IngredientCount.CountRequiredOfFor:
            /// small-volume resources such as gold are listed per 10, so an
            /// advanced component's "0.3" gold is 3); each resource is
            /// tracked in the budget with the colony's stock and the
            /// player's reserve on first use.
            private List<(ThingDef, int)> CostsOf(RecipeDef recipe, int crafts)
            {
                var costs = new List<(ThingDef, int)>();
                List<IngredientCount>? ingredients = recipe.ingredients;
                if (ingredients == null) return costs;
                for (int i = 0; i < ingredients.Count; i++)
                {
                    IngredientCount ingredient = ingredients[i];
                    if (!ingredient.IsFixedIngredient) continue;
                    ThingDef def = ingredient.FixedIngredient;
                    if (!budget.Tracks(def))
                        budget.Track(def, ColonyResourceCount(colony.Maps, def),
                            model.ResourceReserveOf(def.defName));
                    costs.Add((def, ingredient.CountRequiredOfFor(def, recipe) * crafts));
                }
                return costs;
            }
        }

        /// Bills somebody is working on right now at these maps: a bound
        /// unfinished thing holds the ingredients already, and a colony
        /// pawn or mech whose current job is the bill has them in hand.
        /// Such bills promise no stock and are never withdrawn.
        internal static void CollectInProgress(List<Map> maps, HashSet<Bill> into)
        {
            Faction faction = ColonyScope.AuthoritativeFaction;
            for (int m = 0; m < maps.Count; m++)
            {
                IReadOnlyList<Pawn> pawns = maps[m].mapPawns.AllPawnsSpawned;
                for (int p = 0; p < pawns.Count; p++)
                {
                    Bill? bill = pawns[p].CurJob?.bill;
                    if (bill != null && pawns[p].Faction == faction)
                        into.Add(bill);
                }
                List<Building> buildings = maps[m].listerBuildings.allBuildingsColonist;
                for (int b = 0; b < buildings.Count; b++)
                {
                    if (!(buildings[b] is Building_WorkTable bench)) continue;
                    BillStack bills = bench.BillStack;
                    for (int i = 0; i < bills.Count; i++)
                        if (bills[i] is Bill_ProductionWithUft uft && uft.BoundUft != null)
                            into.Add(uft);
                }
            }
        }

        /// The materials Implanner's queued (not started) bills at these
        /// maps have promised, per resource: what the Overview's blocker
        /// text subtracts from stock, the same way the dispatcher does.
        internal static Dictionary<ThingDef, int> PromisedMaterials(
            PlannerModel model, List<Map> maps)
        {
            var promised = new Dictionary<ThingDef, int>();
            if (model.OwnedProductionBills.Count == 0) return promised;
            var started = new HashSet<Bill>(ReferenceIdentityComparer<Bill>.Instance);
            CollectInProgress(maps, started);
            for (int m = 0; m < maps.Count; m++)
            {
                List<Building> buildings = maps[m].listerBuildings.allBuildingsColonist;
                for (int b = 0; b < buildings.Count; b++)
                {
                    if (!(buildings[b] is Building_WorkTable bench)) continue;
                    BillStack bills = bench.BillStack;
                    for (int i = 0; i < bills.Count; i++)
                    {
                        Bill bill = bills[i];
                        if (started.Contains(bill)
                            || !model.OwnedProductionBills.ContainsKey(bill.GetUniqueLoadID()))
                            continue;
                        int crafts = bill is Bill_Production production
                            ? Math.Max(production.repeatCount, 1)
                            : 1;
                        List<IngredientCount>? ingredients = bill.recipe.ingredients;
                        if (ingredients == null) continue;
                        for (int k = 0; k < ingredients.Count; k++)
                        {
                            if (!ingredients[k].IsFixedIngredient) continue;
                            ThingDef def = ingredients[k].FixedIngredient;
                            promised.TryGetValue(def, out int sum);
                            promised[def] = sum
                                + ingredients[k].CountRequiredOfFor(def, bill.recipe) * crafts;
                        }
                    }
                }
            }
            return promised;
        }

        /// The game's bill object for one Implanner production bill: a
        /// fixed number of crafts at the configured minimum skill. The class
        /// comes from the game's own factory (BillUtility.MakeNewBill, what
        /// the bench UI uses): a recipe with an unfinishedThingDef — every
        /// vanilla bionic through BodyPartBionicBase's recipeMaker, both
        /// component recipes, most modded bionics — needs a
        /// Bill_ProductionWithUft, because Toils_Recipe.MakeUnfinishedThingIfNeeded
        /// casts the job's bill to it the moment the crafter starts; a plain
        /// Bill_Production throws InvalidCastException there and the job
        /// ends (reported 2026-09-07, reproduced in-game on Make_BionicArm).
        /// Every class the factory returns derives from Bill_Production.
        internal static Bill_Production MakeBill(
            RecipeDef recipe, int crafts, int minimumSkill)
        {
            var bill = (Bill_Production)recipe.MakeNewBill();
            bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
            bill.repeatCount = crafts;
            bill.allowedSkillRange.min = minimumSkill;
            return bill;
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

        private static int ColonyResourceCount(List<Map> maps, ThingDef def)
        {
            int total = 0;
            for (int m = 0; m < maps.Count; m++)
                total += maps[m].resourceCounter.GetCount(def);
            return total;
        }

        // Cache contract:
        // Owner: process/loaded def set.
        // Key: none (one set).
        // Value: the bench ThingDefs that can craft at least one catalog
        //   implant item (RecipeUsersOf its production recipe); immutable
        //   once built.
        // Dependencies: the loaded definition set (static per session; the
        //   catalog's language gate changes labels, never its kinds).
        // Refresh policy: built once on first query.
        // Equality policy: never changes within a session.
        // Teardown: Reset drops it (world teardown).
        private static HashSet<ThingDef>? implantBenches;

        /// Whether benches of this def can craft any catalog implant item —
        /// only those offer the Implanner bench toggle.
        internal static bool CraftsImplants(ThingDef benchDef)
        {
            if (implantBenches == null)
            {
                var benches = new HashSet<ThingDef>();
                IReadOnlyList<ImplantCatalogEntry> catalog = Catalogs.Implants();
                for (int i = 0; i < catalog.Count; i++)
                {
                    ThingDef? item = catalog[i].Def.spawnThingOnRemoved;
                    RecipeDef? recipe = item != null ? ProductionRecipeFor(item) : null;
                    if (recipe != null) benches.UnionWith(RecipeUsersOf(recipe));
                }
                implantBenches = benches;
            }
            return implantBenches.Contains(benchDef);
        }
    }
}
