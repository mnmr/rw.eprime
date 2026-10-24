using System;
using System.Collections.Generic;
using HarmonyLib;
using Implanner.Core;
using RimShared.Common;
using RimShared.GameLib;
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
    ///   cover the earliest slots; pending output is counted in items using
    ///   each bill's actual recipe yield (ProductionSupply);
    /// - a craft is queued only when stock covers its cost plus the
    ///   materials promised to queued (not started) Implanner bills plus the
    ///   player's reserve (ProductionBudget); a craft that does not fit is
    ///   skipped and later crafts still proceed; an affordable craft
    ///   without a free bench still promises its materials, so rollout
    ///   order decides who gets them;
    /// - queued bills promise their materials oldest first, and one that no
    ///   longer fits (the player spent the stock below a reserve) is
    ///   cancelled until stock allows it again;
    /// - bench slots left over build the manufactured or implant ingredients the first
    ///   skipped crafts will need (BuildAhead), so advanced components are
    ///   ready when a missing raw resource arrives;
    /// - benches the player designated for Implanner take bills first; the
    ///   others serve only while OnlyDesignatedBenches is off
    ///   (PlannerModel.ChooseProductionBench);
    /// - with AllowIntermediaries, the first craft in rollout order blocked
    ///   by a manufactured or implant ingredient sets that ingredient's need: enough
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
        // Value: all non-surgery recipes producing the item with at least one
        //   worktable user, in ordinal defName order; array owned by the cache,
        //   never mutated or exposed for mutation. Availability is NOT cached.
        // Dependencies: the loaded definition set (static per session).
        // Refresh policy: built lazily per def on first demand.
        // Equality policy: entries never change within a session.
        // Teardown: Reset clears the map (world teardown; defensive only).
        private static readonly Dictionary<ThingDef, IReadOnlyList<RecipeDef>> productionRecipes =
            new Dictionary<ThingDef, IReadOnlyList<RecipeDef>>();

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

        internal readonly struct Decision
        {
            internal Decision(RecipeDef? recipe, ThingDef? blocker)
            { Recipe = recipe; Blocker = blocker; Published = true; }
            internal readonly RecipeDef? Recipe;
            internal readonly ThingDef? Blocker;
            internal readonly bool Published;
        }

        // Owner: current ImplannerStore; key: map identity then item def.
        // Value: immutable published recipe/blocker decisions.
        // Dependencies: synchronized rollout/goals, reservations, implant/resource
        // reserves, available stock, pending/started bills, recipe availability,
        // eligible colony benches, production options and installed definitions.
        // Refresh: production's approved 1020-tick boundary or pending production pass.
        // Equality: equal per-colony decisions preserve identity.
        // Teardown: Reset on world teardown; owner change clears all prior entries;
        // publication replaces the complete map set, releasing removed maps.
        private static ImplannerStore? decisionOwner;
        private static Dictionary<int, IReadOnlyDictionary<ThingDef, Decision>> decisions =
            new Dictionary<int, IReadOnlyDictionary<ThingDef, Decision>>();
        internal static object DecisionIdentity => decisions;
        internal static Decision DecisionFor(List<Map> maps, ThingDef item)
        {
            for (int i = 0; i < maps.Count; i++)
                if (decisions.TryGetValue(maps[i].uniqueID, out var byItem)
                    && byItem.TryGetValue(item, out Decision choice)) return choice;
            return default;
        }

        private static bool SameDecisions(IReadOnlyDictionary<ThingDef, Decision> a,
            IReadOnlyDictionary<ThingDef, Decision> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var pair in a)
                if (!b.TryGetValue(pair.Key, out Decision other)
                    || pair.Value.Recipe != other.Recipe || pair.Value.Blocker != other.Blocker)
                    return false;
            return true;
        }

        /// Depth cap for intermediary chains (modded recipe cycles).
        internal const int MaxIntermediaryDepth = 8;

        /// Bill.loadID is private; it increases with every bill the game
        /// creates, so it orders Implanner's bills oldest first.
        private static readonly AccessTools.FieldRef<Bill, int> LoadId =
            AccessTools.FieldRefAccess<Bill, int>("loadID");

        internal static void Reset()
        {
            productionRecipes.Clear();
            recipeUsers.Clear();
            implantBenches = null;
            decisionOwner = null;
            decisions = new Dictionary<int, IReadOnlyDictionary<ThingDef, Decision>>();
        }

        /// Recipe existence, independent of current research: the lowest
        /// defName among non-surgery recipes producing it, or null when the
        /// item cannot be crafted. Builder path only.
        internal static RecipeDef? ProductionRecipeFor(ThingDef itemDef)
        {
            IReadOnlyList<RecipeDef> candidates = ProductionRecipesFor(itemDef);
            return candidates.Count > 0 ? candidates[0] : null;
        }

        internal static IReadOnlyList<RecipeDef> ProductionRecipesFor(ThingDef itemDef)
        {
            if (productionRecipes.TryGetValue(itemDef, out var known))
                return known;
            var candidates = new List<RecipeDef>();
            List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefsListForReading;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDef recipe = recipes[i];
                if (recipe.IsSurgery || recipe.ProducedThingDef != itemDef
                    || RecipeUsersOf(recipe).Count == 0) continue;
                candidates.Add(recipe);
            }
            candidates.Sort(static (a, b) => string.CompareOrdinal(a.defName, b.defName));
            known = candidates.AsReadOnly();
            productionRecipes[itemDef] = known;
            return known;
        }

        /// The bench defs that can work the recipe, resolved once per
        /// recipe per session. Builder path only.
        private static HashSet<ThingDef> RecipeUsersOf(RecipeDef recipe)
        {
            if (recipeUsers.TryGetValue(recipe, out HashSet<ThingDef>? users))
                return users;
            users = new HashSet<ThingDef>();
            foreach (ThingDef user in recipe.AllRecipeUsers)
                if (user.thingClass != null && typeof(Building_WorkTable).IsAssignableFrom(user.thingClass))
                    users.Add(user);
            recipeUsers.Add(recipe, users);
            return users;
        }

        internal static PlannerChange Reconcile(
            ImplannerStore store, ReconcilePass pass)
        {
            PlannerModel model = store.Model;
            if (!ReferenceEquals(decisionOwner, store))
            {
                decisions = new Dictionary<int, IReadOnlyDictionary<ThingDef, Decision>>();
                decisionOwner = store;
            }
            ColonyIndex index = pass.Index;
            var change = PlannerChange.None;
            var colonies = new List<ColonyDispatch>(index.Colonies.Count);
            Dictionary<ThingDef, int> playerReserves = PlannerSurgery.ImplantItemReserves(model);
            var protection = new HashSet<int>(model.Reservations.Keys);
            // Protection changes at the existing synchronized reconciliation
            // gate, including the first tick after a plan or holdback edit.
            // It must not wait for the slower production dispatch boundary.
            for (int c = 0; c < index.Colonies.Count; c++)
            {
                var dispatch = new ColonyDispatch(model, pass, index.Colonies[c]);
                dispatch.AllocateStock(playerReserves);
                protection.UnionWith(dispatch.ProtectedItems);
                colonies.Add(dispatch);
            }
            change |= model.ReplaceProductionProtectedItems(protection);
            if (model.AutomationPaused || !model.AutoProduction
                || (!pass.BoundaryHit && !store.PendingProductionPass)) return change;
            store.ClearPendingProductionPass();

            // Resolve every owned bill once, per colony, in bench-id then
            // stack order.
            var live = new HashSet<string>(StringComparer.Ordinal);
            for (int c = 0; c < colonies.Count; c++) colonies[c].ResolveBenches(live);

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

            var published = new Dictionary<int, IReadOnlyDictionary<ThingDef, Decision>>();
            for (int c = 0; c < colonies.Count; c++)
            {
                change |= colonies[c].Run();
                colonies[c].PublishDecisions(published);
            }
            bool equal = published.Count == decisions.Count;
            foreach (var pair in published)
                if (!decisions.TryGetValue(pair.Key, out var previous)
                    || !ReferenceEquals(pair.Value, previous)) { equal = false; break; }
            if (!equal) decisions = published;
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
            private readonly ProductionSupply<ThingDef> supply = new ProductionSupply<ThingDef>();
            private readonly Dictionary<ThingDef, IReadOnlyList<ProductionRecipe<ThingDef>>> recipeFacts =
                new Dictionary<ThingDef, IReadOnlyList<ProductionRecipe<ThingDef>>>();
            private readonly HashSet<ThingDef> intermediaries = new HashSet<ThingDef>();
            private readonly Dictionary<ThingDef, Decision> choices = new Dictionary<ThingDef, Decision>();
            internal readonly HashSet<int> ProtectedItems = new HashSet<int>();
            private readonly List<CraftNeed<ThingDef>> missing = new List<CraftNeed<ThingDef>>();
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

            internal void AllocateStock(Dictionary<ThingDef, int> playerReserves)
            {
                List<CraftNeed<ThingDef>> rollout = Rollout();
                var stock = new Dictionary<ThingDef, List<Thing>>();
                var heldItems = new HashSet<int>();
                foreach (var pair in model.Reservations) ProtectedItems.Add(pair.Key);
                // Reserve the player's held-back parts even when that kind is
                // only an ingredient, not a final goal in this rollout.
                foreach (var pair in playerReserves)
                {
                    List<Thing> things = FreeThings(pair.Key);
                    things.Sort(ByQualityThenId);
                    int left = pair.Value;
                    for (int i = 0; i < things.Count && left > 0; i++)
                    {
                        int id = things[i].thingIDNumber;
                        if (model.Reservations.ContainsKey(id)) continue;
                        heldItems.Add(id);
                        ProtectedItems.Add(id);
                        left--;
                    }
                }
                // Allocate ALL final stocked implants before any ingredient
                // budget is read. An item can serve a surgery or an upgrade.
                for (int i = 0; i < rollout.Count; i++)
                {
                    var need = rollout[i];
                    if (!stock.TryGetValue(need.Item, out var things))
                    {
                        things = FreeThings(need.Item);
                        things.Sort(ByQualityThenId);
                        for (int k = things.Count - 1; k >= 0; k--)
                            if (heldItems.Contains(things[k].thingIDNumber)) things.RemoveAt(k);
                        stock.Add(need.Item, things);
                    }
                    int pick = things.Count - 1;
                    if (pick >= 0 && ImplantQualities.QualityOf(things[pick]) >= need.MinQuality)
                    {
                        ProtectedItems.Add(things[pick].thingIDNumber);
                        things.RemoveAt(pick);
                    }
                    else missing.Add(need);
                }
            }

            internal PlannerChange Run()
            {
                CollectInProgress(colony.Maps, inProgress);
                // Completed repeat-count bills otherwise retain a bench slot.
                for (int i = 0; i < owned.Count; i++)
                {
                    Bill_Production bill = owned[i].Bill;
                    if (bill.repeatCount > 0 || inProgress.Contains(bill)) continue;
                    bill.billStack.Delete(bill);
                    change |= model.RemoveOwnedProductionBill(pass.BillId(bill));
                }
                PruneDeleted();
                for (int i = 0; i < missing.Count; i++) AddRecipeFacts(missing[i].Item);

                // Each existing bill has its own actual output and quality.
                // Started work is retained. Queued work must still fit above
                // reserves and protected surgery stock before it can cover demand.
                owned.Sort(static (a, b) => LoadId(a.Bill).CompareTo(LoadId(b.Bill)));
                for (int round = 0; round < 2; round++)
                    for (int i = 0; i < owned.Count; i++)
                    {
                        Bill_Production bill = owned[i].Bill;
                        bool started = inProgress.Contains(bill);
                        if (started != (round == 0)) continue;
                        if (!started && (!bill.recipe.AvailableNow
                            || !budget.TryCommit(CostsOf(bill.recipe, Math.Max(bill.repeatCount, 1)))))
                        {
                            bill.billStack.Delete(bill);
                            change |= model.RemoveOwnedProductionBill(pass.BillId(bill));
                            continue;
                        }
                        TakeSlot(owned[i].Bench);
                        ThingDef? product = bill.recipe.ProducedThingDef;
                        if (product != null)
                            supply.Add(product, pass.BillId(bill), Math.Max(bill.repeatCount, 0)
                                * OutputCount(bill.recipe, product),
                                model.ProductionBillQualityOf(pass.BillId(bill)));
                    }
                PruneDeleted();
                var queue = new List<CraftNeed<ThingDef>>();
                for (int i = 0; i < missing.Count; i++)
                    if (supply.Take(missing[i].Item, 1, missing[i].MinQuality) > 0)
                        queue.Add(missing[i]);

                var skipped = new List<ThingDef>();
                for (int i = 0; i < queue.Count; i++)
                {
                    // A newly placed multi-output bill may cover later slots.
                    var need = queue[i];
                    if (supply.Take(need.Item, 1, need.MinQuality) == 0) continue;
                    Craft result = TryCraft(need.Item, 0, need.MinQuality);
                    if (result == Craft.Created) supply.Take(need.Item, 1, need.MinQuality);
                    else if (result == Craft.Blocked) skipped.Add(need.Item);
                }
                BuildAhead(skipped);
                // Final and prerequisite demand share this ledger. A bill
                // producing a goal item is still needed when an upgrade uses it.
                for (int i = 0; i < owned.Count; i++)
                {
                    Bill_Production bill = owned[i].Bill;
                    if (inProgress.Contains(bill) || supply.Uses(pass.BillId(bill))) continue;
                    bill.billStack.Delete(bill);
                    change |= model.RemoveOwnedProductionBill(pass.BillId(bill));
                }
                return change;
            }

            private static readonly Comparison<Thing> ByQualityThenId = static (a, b) =>
            {
                int quality = ImplantQualities.QualityOf(a).CompareTo(ImplantQualities.QualityOf(b));
                return quality != 0 ? quality : b.thingIDNumber.CompareTo(a.thingIDNumber);
            };

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
                    ProductionRoute<ThingDef> route = SelectRoute(skipped[i]);
                    if (route.Recipe == null) continue;
                    var costs = route.Recipe.Costs;
                    for (int k = 0; k < costs.Count; k++)
                    {
                        ThingDef ingredient = costs[k].Resource;
                        if (!intermediaries.Contains(ingredient)) continue;
                        if (!requirement.TryGetValue(ingredient, out int sum)) order.Add(ingredient);
                        requirement[ingredient] = sum + costs[k].Count;
                    }
                }
                for (int i = 0; i < order.Count; i++)
                    EnsureIngredient(order[i], budget.Shortfall(order[i], requirement[order[i]]), 1);
            }

            private void EnsureIngredient(ThingDef item, int wanted, int depth)
            {
                intermediaryNeed.TryGetValue(item, out int prior);
                if (wanted <= prior) return;
                intermediaryNeed[item] = wanted;
                int left = supply.Take(item, wanted - prior);
                while (left > 0)
                {
                    if (TryCraft(item, depth) != Craft.Created) break;
                    left = supply.Take(item, left);
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
            private List<Thing> FreeThings(ThingDef item)
            {
                var things = new List<Thing>();
                List<int>? ids = colony.ItemIdsOf(item);
                if (ids != null)
                    for (int i = 0; i < ids.Count; i++)
                    {
                        Thing thing = pass.Index.ItemsById[ids[i]];
                        if (!thing.IsForbidden(Faction.OfPlayer))
                            for (int n = 0; n < thing.stackCount; n++) things.Add(thing);
                    }
                Faction faction = PlayerFactions.AuthoritativeFaction;
                for (int m = 0; m < colony.Maps.Count; m++)
                {
                    IReadOnlyList<Pawn> pawns = colony.Maps[m].mapPawns.AllPawnsSpawned;
                    for (int p = 0; p < pawns.Count; p++)
                    {
                        Thing? carried = pawns[p].carryTracker?.CarriedThing;
                        if (carried != null && carried.def == item && pawns[p].Faction == faction)
                            for (int n = 0; n < carried.stackCount; n++) things.Add(carried);
                    }
                }
                return things;
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

            private enum Craft { Created, NoBench, Waiting, Blocked }

            /// One craft of the item. When it fits the budget its materials
            /// are promised, and a bill is created if a bench has room
            /// (NoBench otherwise). When it does not fit (Blocked), the
            /// first blocked craft in rollout order sets each short
            /// manufactured ingredient's need, once per pass: enough
            /// one-craft ingredient bills, pending ones counted, to cover
            /// the shortfall, queued right here in rollout order.
            private Craft TryCraft(ThingDef item, int depth, int minQuality = 0)
            {
                ProductionRoute<ThingDef> route = SelectRoute(item);
                RecipeDef? recipe = route.Recipe == null ? null
                    : DefDatabase<RecipeDef>.GetNamedSilentFail(route.Recipe.Id);
                if (!choices.ContainsKey(item))
                {
                    // A researched recipe with no eligible bench is still a
                    // crafting route. Report waiting for a bench, not no recipe.
                    RecipeDef? diagnostic = recipe;
                    if (diagnostic == null)
                    {
                        var candidates = ProductionRecipesFor(item);
                        for (int i = 0; i < candidates.Count; i++)
                            if (candidates[i].AvailableNow) { diagnostic = candidates[i]; break; }
                    }
                    choices[item] = new Decision(diagnostic,
                        recipe != null && route.Blocked ? route.Blocker : null);
                }
                if (recipe == null) return Craft.Blocked;
                var costs = CostsOf(recipe, 1);
                if (budget.TryCommit(costs))
                    return QueueCraft(recipe, item, minQuality);
                if (!model.AllowIntermediaries || depth >= MaxIntermediaryDepth
                    || !expanded.Add(item)) return Craft.Blocked;
                if (!route.Blocked)
                {
                    // Follow the feasible chain's actual recipes. Merely choosing
                    // the final recipe and reselecting each child independently
                    // can consume materials the final craft needs.
                    for (int i = 0; i < route.Crafts.Count; i++)
                    {
                        var craft = route.Crafts[i];
                        for (int k = 0; k < craft.Costs.Count; k++)
                        {
                            var cost = craft.Costs[k];
                            int shortfall = budget.Shortfall(cost.Resource, cost.Count);
                            if (shortfall <= 0) continue;
                            supply.Take(cost.Resource, shortfall);
                            intermediaryNeed.TryGetValue(cost.Resource, out int prior);
                            intermediaryNeed[cost.Resource] = Math.Max(prior, shortfall);
                        }
                        if (i == route.Crafts.Count - 1 || !budget.TryCommit(craft.Costs)) continue;
                        RecipeDef next = DefDatabase<RecipeDef>.GetNamed(craft.Id);
                        QueueCraft(next, craft.Item, 0);
                    }
                    return Craft.Waiting;
                }
                for (int k = 0; k < costs.Count; k++)
                {
                    ThingDef ingredient = costs[k].Item1;
                    if (!intermediaries.Contains(ingredient)) continue;
                    EnsureIngredient(ingredient, budget.Shortfall(ingredient, costs[k].Item2), depth + 1);
                }
                return Craft.Blocked;
            }

            private Craft QueueCraft(RecipeDef recipe, ThingDef item, int minQuality)
            {
                Building_WorkTable? bench = FindFreeBench(recipe);
                if (bench == null) return Craft.NoBench;
                Bill_Production bill = MakeBill(recipe, 1, model.ProductionSkill);
                bench.BillStack.AddBill(bill);
                TakeSlot(bench);
                string billId = pass.BillId(bill);
                change |= model.SetOwnedProductionBill(billId, item.defName);
                int quality = PromiseQuality(bill, item, minQuality);
                change |= model.SetProductionBillQuality(billId, quality);
                supply.Add(item, billId, OutputCount(recipe, item), quality);
                return Craft.Created;
            }

            private ProductionRoute<ThingDef> SelectRoute(ThingDef item)
            {
                AddRecipeFacts(item);
                ProductionBudget<ThingDef> future = budget.Copy();
                supply.AddAvailableTo(future);
                return ProductionRoute<ThingDef>.Select(item, recipeFacts, budget, intermediaries,
                    MaxIntermediaryDepth, future);
            }

            private void AddRecipeFacts(ThingDef item)
            {
                if (recipeFacts.ContainsKey(item)) return;
                var facts = new List<ProductionRecipe<ThingDef>>();
                recipeFacts[item] = facts;
                var candidates = ProductionRecipesFor(item);
                for (int i = 0; i < candidates.Count; i++)
                {
                    RecipeDef recipe = candidates[i];
                    var costs = CostsOf(recipe, 1);
                    facts.Add(new ProductionRecipe<ThingDef>(recipe.defName, item,
                        OutputCount(recipe, item), costs, recipe.AvailableNow && HasEligibleBench(recipe),
                        recipe.WorkAmountTotal(null)));
                    for (int k = 0; k < costs.Count; k++)
                    {
                        ThingDef ingredient = costs[k].Item1;
                        if (!model.AllowIntermediaries || !CanMakeIntermediate(ingredient)) continue;
                        intermediaries.Add(ingredient);
                        AddRecipeFacts(ingredient);
                    }
                }
            }

            private bool HasEligibleBench(RecipeDef recipe)
            {
                var users = RecipeUsersOf(recipe);
                for (int i = 0; i < benches.Count; i++)
                {
                    Building_WorkTable bench = benches[i];
                    if (!users.Contains(bench.def)) continue;
                    if (model.IsBenchDesignated(bench.thingIDNumber)) return true;
                    if (!model.OnlyDesignatedBenches && (!model.OnlyIdleBenches || !HasOtherWork(bench)))
                        return true;
                }
                return false;
            }

            internal void PublishDecisions(Dictionary<int, IReadOnlyDictionary<ThingDef, Decision>> published)
            {
                IReadOnlyDictionary<ThingDef, Decision> value = choices;
                if (colony.Maps.Count > 0 && decisions.TryGetValue(colony.Maps[0].uniqueID, out var prior)
                    && SameDecisions(prior, choices)) value = prior;
                for (int i = 0; i < colony.Maps.Count; i++) published[colony.Maps[i].uniqueID] = value;
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
                        budget.Track(def, IngredientStock(def), model.ResourceReserveOf(def.defName));
                    costs.Add((def, ingredient.CountRequiredOfFor(def, recipe) * crafts));
                }
                return costs;
            }
            private int IngredientStock(ThingDef item)
            {
                if (!item.isTechHediff) return ColonyResourceCount(colony.Maps, item);
                int count = 0;
                List<Thing> things = FreeThings(item);
                for (int i = 0; i < things.Count; i++)
                    if (!ProtectedItems.Contains(things[i].thingIDNumber)) count++;
                return count;
            }

        }

        /// Bills somebody is working on right now at these maps: a bound
        /// unfinished thing holds the ingredients already, and a colony
        /// pawn or mech whose current job is the bill has them in hand.
        /// Such bills promise no stock and are never withdrawn.
        internal static void CollectInProgress(List<Map> maps, HashSet<Bill> into)
        {
            Faction faction = PlayerFactions.AuthoritativeFaction;
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

        internal static bool CanMakeIntermediate(ThingDef def) =>
            (IsManufactured(def) || def.isTechHediff) && ProductionRecipesFor(def).Count > 0;

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

        internal static int ColonyResourceCount(List<Map> maps, ThingDef def)
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
                    if (item == null) continue;
                    IReadOnlyList<RecipeDef> candidates = ProductionRecipesFor(item);
                    for (int r = 0; r < candidates.Count; r++)
                        benches.UnionWith(RecipeUsersOf(candidates[r]));
                }
                implantBenches = benches;
            }
            return implantBenches.Contains(benchDef);
        }
    }
}
