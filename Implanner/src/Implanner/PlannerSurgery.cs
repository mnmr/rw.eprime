using System;
using System.Collections.Generic;
using Implanner.Core;
using RimShared.Common;
using RimWorld;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner
{
    /// Surgery automation inside the deterministic reconciliation pass:
    /// traversal-ordered implant-item allocation, batch-gated operation
    /// scheduling after player bills, the automatic doctor-skill floor, and
    /// owned-bill lifecycle. Runs only from PlannerReconciler's synchronized
    /// tick path, consumes only authoritative synchronized state, and takes
    /// all colony structure and per-pawn evaluations from the pass — no map
    /// or faction resolution happens here.
    internal static class PlannerSurgery
    {
        internal static PlannerChange Reconcile(
            ImplannerStore store, ReconcilePass pass)
        {
            PlannerModel model = store.Model;
            ColonyIndex index = pass.Index;
            var change = PlannerChange.None;

            // Lifecycle hygiene runs even while paused: floors of locations
            // that no longer exist are dropped.
            var liveLocations = new HashSet<string>(StringComparer.Ordinal);
            for (int c = 0; c < index.Colonies.Count; c++)
                liveLocations.Add(index.Colonies[c].LocationId);
            change |= model.PruneDoctorFloors(liveLocations);

            if (model.AutomationPaused) return change;

            change |= EvaluateDoctorFloors(model, pass);
            change |= AllocateImplantItems(model, pass);
            change |= ScheduleOperations(model, pass);
            return change;
        }

        /// The pawn's Medical skill when they are an active doctor; -1 when
        /// they are not. The single eligibility rule shared by the automatic
        /// floor, the floor-blocker display, and manual-floor seeding.
        internal static int EligibleDoctorSkill(Pawn pawn) =>
            pawn.workSettings != null
                && pawn.workSettings.WorkIsActive(WorkTypeDefOf.Doctor)
                ? pawn.skills?.GetSkill(SkillDefOf.Medicine)?.Level ?? 0
                : -1;

        /// Automatic doctor floor: on the approved 1020-tick boundary
        /// (PlannerReconciler.BoundaryTicks, pure tick arithmetic), publish
        /// each colony's CURRENT best eligible Medical skill — up, down, or
        /// cleared when the colony has no eligible doctor left.
        private static PlannerChange EvaluateDoctorFloors(
            PlannerModel model, ReconcilePass pass)
        {
            if (!model.AutoDoctorFloor || !pass.BoundaryHit)
                return PlannerChange.None;
            ColonyIndex index = pass.Index;

            var change = PlannerChange.None;
            var best = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int c = 0; c < index.Colonies.Count; c++)
            {
                Colony colony = index.Colonies[c];
                for (int i = 0; i < colony.PawnIds.Count; i++)
                {
                    int skill = EligibleDoctorSkill(
                        index.PawnsById[colony.PawnIds[i]]);
                    if (skill < 0) continue;
                    if (!best.TryGetValue(colony.LocationId, out int current)
                        || skill > current)
                        best[colony.LocationId] = skill;
                }
            }
            // Colonies that lost every eligible doctor clear their entry.
            var stale = new List<string>();
            foreach (KeyValuePair<string, int> pair in model.DoctorFloors)
                if (!best.ContainsKey(pair.Key))
                    stale.Add(pair.Key);
            stale.Sort(StringComparer.Ordinal);
            for (int i = 0; i < stale.Count; i++)
                change |= model.SetDoctorFloor(stale[i], 0);
            var locations = new List<string>(best.Keys);
            locations.Sort(StringComparer.Ordinal);
            for (int i = 0; i < locations.Count; i++)
                change |= model.SetDoctorFloor(locations[i], best[locations[i]]);
            return change;
        }

        /// The player's implant reservations projected onto item kinds: how
        /// many of each implant item automation must leave for manual use.
        /// Max wins when several implants share an item, so the result is
        /// independent of dictionary iteration order.
        internal static Dictionary<ThingDef, int> ImplantItemReserves(
            PlannerModel model)
        {
            var result = new Dictionary<ThingDef, int>();
            foreach (KeyValuePair<string, int> pair in model.ImplantReserves)
            {
                ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(pair.Key);
                ThingDef? item = entry?.Def.spawnThingOnRemoved;
                if (item == null) continue;
                result.TryGetValue(item, out int existing);
                result[item] = Math.Max(existing, pair.Value);
            }
            return result;
        }

        /// Reserves stored implant items for the missing goal slots of each
        /// pawn's ACTIVE batch only, ordered by the configured traversal
        /// (family order and batching, per-pawn priority). Slots outside the
        /// active batch never allocate — a stock-starved batch must not let
        /// later tiers hoover up reservations the player still has free use
        /// of (they would read "Awaiting batch" while the real work is
        /// elsewhere). Each colony is visited exactly once (the index folds a
        /// map stack into one colony), one colony never spends another's
        /// stock, and stock the player reserved for manual use is never
        /// touched: automation holds (and releases excess holdings) until
        /// enough items exist.
        private static PlannerChange AllocateImplantItems(
            PlannerModel model, ReconcilePass pass)
        {
            ColonyIndex index = pass.Index;
            var change = PlannerChange.None;

            var reservedGoals = new HashSet<(int, string)>();
            var reservedItems = new HashSet<int>();
            foreach (KeyValuePair<int, ItemReservation> pair in model.Reservations)
            {
                reservedGoals.Add((pair.Value.PawnId, pair.Value.GoalKey));
                reservedItems.Add(pair.Key);
            }

            Dictionary<ThingDef, int> playerReserves = ImplantItemReserves(model);

            for (int c = 0; c < index.Colonies.Count; c++)
            {
                Colony colony = index.Colonies[c];

                // Per-kind allowance under the player's implant reserves:
                // present stock minus the held-back count, less what we
                // already hold. Holdings beyond the allowance (stock shrank
                // or the reserve grew) are released, newest item id first.
                var allowance = new Dictionary<ThingDef, int>();
                if (playerReserves.Count > 0)
                    change |= EnforcePlayerReserves(model, colony, index,
                        playerReserves, reservedGoals, reservedItems,
                        allowance);

                // Pending work on this colony's pawns, traversal-ordered.
                // ASAP ranks candidates on live pawn facts (move speed,
                // weapon, skills), sampled once per pawn with pending work;
                // the batch strategies never read them.
                bool asap = model.Iteration == IterationStrategy.Asap;
                var work = new List<SurgeryWorkItem>();
                var requiredItemDef = new Dictionary<(int, string), ThingDef>();
                for (int i = 0; i < colony.PawnIds.Count; i++)
                {
                    int pawnId = colony.PawnIds[i];
                    PawnEvaluation? evaluation = pass.Evaluate(pawnId);
                    if (evaluation == null) continue;
                    IReadOnlyList<ImplantGoal> goals = evaluation.Goals;
                    List<string> batch = evaluation.Batch;
                    int priority = model.PriorityOf(pawnId);
                    SurgeryCandidate candidate = default;
                    bool sampled = !asap;
                    for (int k = 0; k < batch.Count; k++)
                    {
                        string key = batch[k];
                        if (reservedGoals.Contains((pawnId, key))) continue;
                        if (!GoalKeys.TryResolveImplantSlot(
                                goals, key, out ImplantGoal goal, out _))
                            continue;
                        ImplantCatalogEntry? entry =
                            Catalogs.ImplantByDefName(goal.ImplantDefName);
                        ThingDef? item = entry?.Def.spawnThingOnRemoved;
                        if (item == null) continue;
                        if (!sampled)
                        {
                            candidate = PawnProjection.CandidateOf(index.PawnsById[pawnId]);
                            sampled = true;
                        }
                        requiredItemDef[(pawnId, key)] = item;
                        work.Add(new SurgeryWorkItem(pawnId, priority,
                            StarRanking.TierOf(model.ImplantStarsOf(goal.ImplantDefName)),
                            key, goal.ImplantDefName, entry!.Limb, candidate));
                    }
                }
                if (work.Count == 0) continue;
                SurgeryPlanner.Order(work, model.Iteration);

                // This colony's unreserved implant stock per kind, resolved
                // on first demand; the index's ids are pre-sorted, so
                // allocation stays lowest-id first, and each kind advances
                // its own cursor as items are taken.
                var stockByDef = new Dictionary<ThingDef, List<Thing>>();
                var cursor = new Dictionary<ThingDef, int>();

                for (int i = 0; i < work.Count; i++)
                {
                    SurgeryWorkItem unit = work[i];
                    ThingDef required = requiredItemDef[(unit.PawnId, unit.GoalKey)];
                    // A kind under a player reserve allocates only inside its
                    // allowance; surgery holds until more items exist.
                    bool capped = allowance.TryGetValue(required, out int left);
                    if (capped && left <= 0) continue;
                    if (!stockByDef.TryGetValue(required, out List<Thing> stock))
                    {
                        stock = FreeStock(colony, index, required, reservedItems);
                        stockByDef.Add(required, stock);
                    }
                    cursor.TryGetValue(required, out int at);
                    if (at >= stock.Count) continue;
                    Thing thing = stock[at];
                    cursor[required] = at + 1;
                    change |= model.Reserve(
                        thing.thingIDNumber, unit.PawnId, unit.GoalKey);
                    reservedItems.Add(thing.thingIDNumber);
                    reservedGoals.Add((unit.PawnId, unit.GoalKey));
                    if (capped) allowance[required] = left - 1;
                }
            }
            return change;
        }

        /// The colony's unreserved, unforbidden items of one kind, ids
        /// ascending.
        private static List<Thing> FreeStock(Colony colony, ColonyIndex index,
            ThingDef def, HashSet<int> reservedItems)
        {
            var stock = new List<Thing>();
            List<int>? ids = colony.ItemIdsOf(def);
            if (ids == null) return stock;
            for (int i = 0; i < ids.Count; i++)
            {
                int itemId = ids[i];
                if (reservedItems.Contains(itemId)) continue;
                Thing thing = index.ItemsById[itemId];
                if (thing.IsForbidden(Faction.OfPlayer)) continue;
                stock.Add(thing);
            }
            return stock;
        }

        /// Computes the per-kind allocation allowance at one colony under the
        /// player's implant reserves, releasing excess holdings first.
        private static PlannerChange EnforcePlayerReserves(PlannerModel model,
            Colony colony, ColonyIndex index,
            Dictionary<ThingDef, int> playerReserves,
            HashSet<(int, string)> reservedGoals, HashSet<int> reservedItems,
            Dictionary<ThingDef, int> allowance)
        {
            var change = PlannerChange.None;
            // Kinds are independent of one another, so the reserve map's
            // iteration order cannot change the outcome.
            foreach (KeyValuePair<ThingDef, int> pair in playerReserves)
            {
                int total = 0;
                List<int>? held = null;
                List<int>? ids = colony.ItemIdsOf(pair.Key);
                if (ids != null)
                    for (int i = 0; i < ids.Count; i++)
                    {
                        int itemId = ids[i];
                        Thing thing = index.ItemsById[itemId];
                        if (thing.IsForbidden(Faction.OfPlayer)) continue;
                        total += thing.stackCount;
                        if (model.Reservations.ContainsKey(itemId))
                            (held ??= new List<int>()).Add(itemId);
                    }
                int cap = Math.Max(0, total - pair.Value);
                int heldCount = held?.Count ?? 0;
                if (held != null && heldCount > cap)
                {
                    // Ids arrive ascending from the index: newest first.
                    for (int i = held.Count - 1; i >= 0 && heldCount > cap; i--, heldCount--)
                    {
                        model.TryGetReservation(held[i], out ItemReservation reservation);
                        change |= model.ReleaseReservation(held[i]);
                        reservedItems.Remove(held[i]);
                        reservedGoals.Remove(
                            (reservation.PawnId, reservation.GoalKey));
                    }
                }
                allowance[pair.Key] = cap - heldCount;
            }
            return change;
        }

        /// One implant slot's position in the surgery pipeline, for the
        /// details panel. Ordered by precedence: higher values override.
        internal enum SlotStatus
        {
            None = 0,
            AwaitingBatch = 1,
            Recovering = 2,
            Scheduled = 3,
            BlockedByFloor = 4,
        }

        /// Presentation projection of one pawn's surgery pipeline, derived
        /// from the same batch, health-gate, floor, and readiness logic the
        /// reconciler executes (the UI never reconstructs the queue). Builder
        /// path only. reservationReadiness carries goal key → the reserved
        /// item is collectable (spawned on the pawn's colony stack and
        /// unforbidden), built by the overview snapshot's single-pass
        /// reservation resolution — the same gate ScheduleOperations applies,
        /// so a reservation stranded at another colony reads as merely
        /// Reserved, never Recovering or AwaitingBatch. Returns slot goal
        /// key → status for every missing slot that is at least ready or
        /// scheduled.
        internal static Dictionary<string, SlotStatus> PresentationFor(
            PlannerModel model, Pawn pawn, Plan plan,
            Dictionary<string, bool> reservationReadiness,
            out int effectiveFloor)
        {
            var statuses = new Dictionary<string, SlotStatus>(StringComparer.Ordinal);
            PawnPlace place = ColonyScope.PlaceOf(pawn);
            effectiveFloor = model.EffectiveDoctorFloor(place.LocationId ?? "");
            IReadOnlyList<ImplantGoal> goals = model.EffectiveImplants(plan);
            if (goals.Count == 0) return statuses;

            List<string> missing = PawnProjection.MissingImplantSlotKeys(
                pawn, goals);
            if (missing.Count == 0) return statuses;
            List<string> batch = SurgeryPlanner.ComputeBatch(
                missing, model, goals, model.Iteration);

            var ready = new bool[batch.Count];
            for (int i = 0; i < batch.Count; i++)
                ready[i] = reservationReadiness.TryGetValue(batch[i], out bool keyReady)
                    && keyReady;
            List<string> releasable = SurgeryPlanner.Releasable(
                batch, ready, model.Iteration);
            bool gated = releasable.Count > 0 && !HealthGate(pawn);
            bool floorBlocked = effectiveFloor > BestMedicalSkill(pawn.MapHeld);

            for (int i = 0; i < missing.Count; i++)
            {
                string key = missing[i];
                if (model.OwnedBill(pawn.thingIDNumber, key) != null)
                {
                    statuses[key] = floorBlocked
                        ? SlotStatus.BlockedByFloor
                        : SlotStatus.Scheduled;
                    continue;
                }
                if (!reservationReadiness.TryGetValue(key, out bool keyReady)
                    || !keyReady)
                    continue;
                statuses[key] = gated && releasable.Contains(key)
                    ? SlotStatus.Recovering
                    : SlotStatus.AwaitingBatch;
            }
            return statuses;
        }

        /// The best Medical skill among doctors on the pawn's map; what the
        /// effective floor is compared against when naming a floor blocker.
        private static int BestMedicalSkill(Map? map)
        {
            map = FloorMaps.Canonical(map);
            if (map == null) return 0;
            int best = 0;
            // Spawned only: a doctor sealed in a casket or pod cannot
            // operate and must not raise the floor the UI reports.
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                int skill = EligibleDoctorSkill(colonists[i]);
                if (skill > best) best = skill;
            }
            return best;
        }

        /// Batch-gated operation scheduling: once every implant in the pawn's
        /// active batch is physically reserved at the pawn's colony and the
        /// pawn is eligible, all still-missing Implanner operations are
        /// appended as one contiguous block after existing bills (ASAP
        /// appends whatever is reserved on site right away). Matching
        /// user operations count as scheduled; deleted Implanner operations
        /// are recreated while goal and reservation remain valid.
        private static PlannerChange ScheduleOperations(
            PlannerModel model, ReconcilePass pass)
        {
            ColonyIndex index = pass.Index;
            var change = PlannerChange.None;

            var pawnIds = new List<int>(model.Assignments.Keys);
            pawnIds.Sort();

            // Retract owned operations whose goal is no longer pursued
            // (delivered, removed, or blocked) BEFORE counting the
            // concurrency cap, so a pawn whose last record just went stale
            // frees its slot in this pass. Away pawns keep any
            // already-scheduled operations untouched.
            for (int i = 0; i < pawnIds.Count; i++)
            {
                int pawnId = pawnIds[i];
                if (index.ColonyOfPawn(pawnId) == null) continue;
                PawnEvaluation? evaluation = pass.Evaluate(pawnId);
                if (evaluation == null) continue;
                IReadOnlyDictionary<string, string>? owned = model.OwnedBillsFor(pawnId);
                if (owned == null) continue;
                List<string>? stale = null;
                foreach (KeyValuePair<string, string> pair in owned)
                    if (!evaluation.Missing.Contains(pair.Key))
                        (stale ??= new List<string>()).Add(pair.Key);
                if (stale == null) continue;
                Pawn pawn = index.PawnsById[pawnId];
                stale.Sort(StringComparer.Ordinal);
                for (int k = 0; k < stale.Count; k++)
                {
                    Bill? bill = pass.FindBill(pawn.BillStack, owned[stale[k]]);
                    if (bill != null) pawn.BillStack.Delete(bill);
                    change |= model.RemoveOwnedBill(pawnId, stale[k]);
                }
            }

            var reservedItemByGoal = new Dictionary<(int, string), int>();
            foreach (KeyValuePair<int, ItemReservation> pair in model.Reservations)
                reservedItemByGoal[(pair.Value.PawnId, pair.Value.GoalKey)] = pair.Key;

            // Concurrent-surgeries cap: colonists per colony that hold live
            // Implanner operations after retraction. New colonists only
            // start while the colony stays under SurgeryConcurrency; one
            // already scheduled keeps completing its batch regardless.
            var plannedByColony = new Dictionary<string, int>();
            var countedPawns = new HashSet<int>();
            foreach (KeyValuePair<int, IReadOnlyDictionary<string, string>> pair
                in model.OwnedBills)
            {
                if (pair.Value.Count == 0) continue;
                Colony? billColony = index.ColonyOfPawn(pair.Key);
                if (billColony == null) continue;
                plannedByColony.TryGetValue(billColony.LocationId, out int n);
                plannedByColony[billColony.LocationId] = n + 1;
                countedPawns.Add(pair.Key);
            }

            // Hospitalized pawns occupy slots too when the option is on:
            // anyone humanlike lying in a medical bed, or downed and
            // needing medical rest, keeps the hospital and its doctors
            // busy — new Implanner surgeries wait for room.
            if (model.CountHospitalized)
                for (int c = 0; c < index.Colonies.Count; c++)
                {
                    Colony hospitalColony = index.Colonies[c];
                    for (int m = 0; m < hospitalColony.Maps.Count; m++)
                    {
                        IReadOnlyList<Pawn> mapPawns =
                            hospitalColony.Maps[m].mapPawns.AllPawnsSpawned;
                        for (int p = 0; p < mapPawns.Count; p++)
                        {
                            Pawn occupant = mapPawns[p];
                            if (!occupant.RaceProps.Humanlike) continue;
                            if (countedPawns.Contains(occupant.thingIDNumber))
                                continue;
                            if (!IsHospitalized(occupant)) continue;
                            plannedByColony.TryGetValue(
                                hospitalColony.LocationId, out int n);
                            plannedByColony[hospitalColony.LocationId] = n + 1;
                        }
                    }
                }

            for (int i = 0; i < pawnIds.Count; i++)
            {
                int pawnId = pawnIds[i];
                Colony? colony = index.ColonyOfPawn(pawnId);
                // Away pawns receive no new surgery automation.
                if (colony == null) continue;
                PawnEvaluation? evaluation = pass.Evaluate(pawnId);
                if (evaluation == null) continue;
                Pawn pawn = index.PawnsById[pawnId];
                List<string> batch = evaluation.Batch;
                if (batch.Count == 0) continue;

                // A key is ready when its reserved item exists at the pawn's
                // colony. The batch strategies release the whole batch only
                // once every key is ready; ASAP releases every ready key.
                var ready = new bool[batch.Count];
                for (int k = 0; k < batch.Count; k++)
                {
                    ready[k] = reservedItemByGoal.TryGetValue((pawnId, batch[k]), out int itemId)
                        && index.ItemsById.ContainsKey(itemId)
                        && index.SameColony(pawnId, itemId);
                }
                List<string> releasable = SurgeryPlanner.Releasable(
                    batch, ready, model.Iteration);

                // Batch membership and health gate the RELEASE of new
                // operations — an already-scheduled valid operation is
                // never pulled back because the pawn got wounded or the
                // batch grew.
                if (releasable.Count == 0 || !HealthGate(pawn)) continue;
                int floor = model.EffectiveDoctorFloor(colony.LocationId);

                // The cap gates only colonists without scheduled operations.
                IReadOnlyDictionary<string, string>? owned = model.OwnedBillsFor(pawnId);
                if (owned == null || owned.Count == 0)
                {
                    plannedByColony.TryGetValue(
                        colony.LocationId, out int planned);
                    if (planned >= model.SurgeryConcurrency) continue;
                    plannedByColony[colony.LocationId] = planned + 1;
                }

                for (int k = 0; k < releasable.Count; k++)
                    change |= EnsureOperation(model, pass, pawn, pawnId,
                        evaluation.Goals, releasable[k], floor);
            }

            // Sweep records of pawns that lost their assignment or left
            // play entirely (no longer alive anywhere as our colonist); a
            // still-present pawn also loses the orphaned bills themselves.
            // A pawn merely away (caravan, transporter or gravship in
            // flight, held in a casket) is present and keeps its records.
            var billPawns = new List<int>(model.OwnedBills.Keys);
            billPawns.Sort();
            for (int i = 0; i < billPawns.Count; i++)
            {
                int pawnId = billPawns[i];
                bool present = index.PawnsById.TryGetValue(pawnId, out Pawn pawn);
                if (model.AssignedPlan(pawnId) != null && present) continue;
                IReadOnlyDictionary<string, string>? owned = model.OwnedBillsFor(pawnId);
                if (owned == null) continue;
                var keys = new List<string>(owned.Keys);
                keys.Sort(StringComparer.Ordinal);
                for (int k = 0; k < keys.Count; k++)
                {
                    if (present)
                    {
                        Bill? bill = pass.FindBill(pawn.BillStack, owned[keys[k]]);
                        if (bill != null) pawn.BillStack.Delete(bill);
                    }
                    change |= model.RemoveOwnedBill(pawnId, keys[k]);
                }
            }
            return change;
        }

        /// Lying in a medical bed, or downed and needing medical rest: the
        /// pawn occupies the hospital (and usually a doctor).
        private static bool IsHospitalized(Pawn pawn)
        {
            Building_Bed? bed = pawn.CurrentBed();
            if (bed != null && bed.Medical) return true;
            return pawn.Downed && HealthAIUtility.ShouldSeekMedicalRest(pawn);
        }

        private static PlannerChange EnsureOperation(PlannerModel model,
            ReconcilePass pass, Pawn pawn, int pawnId,
            IReadOnlyList<ImplantGoal> goals, string goalKey, int floor)
        {
            if (!GoalKeys.TryResolveImplantSlot(
                    goals, goalKey, out ImplantGoal goal, out int ordinal))
                return PlannerChange.None;
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(goal.ImplantDefName);
            if (entry == null) return PlannerChange.None;
            BodyPartRecord? part = PawnProjection.ResolveSlotPart(pawn, entry, ordinal);
            if (part == null) return PlannerChange.None;
            RecipeDef? recipe = SelectRecipe(entry, pawn, part);
            if (recipe == null) return PlannerChange.None;

            // Our recorded operation still stands: keep it, tracking the
            // effective doctor floor.
            string? recordedId = model.OwnedBill(pawnId, goalKey);
            Bill? recorded = recordedId != null
                ? pass.FindBill(pawn.BillStack, recordedId)
                : null;
            if (recorded is Bill_Medical mine
                && mine.recipe == recipe && mine.Part == part)
            {
                if (mine.allowedSkillRange.min != floor)
                    mine.allowedSkillRange = new IntRange(
                        floor, mine.allowedSkillRange.max);
                return PlannerChange.None;
            }
            var change = PlannerChange.None;
            if (recordedId != null)
            {
                // The recorded operation no longer matches the selected
                // recipe or part (research or content changes moved the
                // deterministic recipe choice): the stale bill goes with its
                // record, exactly like the stale-goal sweep — dropping only
                // the record would leave a live duplicate surgery queued on
                // the pawn with nothing able to retract it.
                if (recorded != null) pawn.BillStack.Delete(recorded);
                change |= model.RemoveOwnedBill(pawnId, goalKey);
            }

            // A matching user-created operation counts as already scheduled:
            // never duplicated, never adopted.
            BillStack bills = pawn.BillStack;
            for (int b = 0; b < bills.Count; b++)
                if (bills[b] is Bill_Medical existing
                    && existing.recipe == recipe && existing.Part == part)
                    return change;

            var bill = new Bill_Medical(recipe, null);
            bills.AddBill(bill);
            bill.Part = part;
            bill.allowedSkillRange = new IntRange(floor, PlannerModel.DoctorFloorMax);
            return change | model.SetOwnedBill(pawnId, goalKey, pass.BillId(bill));
        }

        /// The deterministic surgery recipe for an implant at a part: lowest
        /// defName among the currently available candidates.
        private static RecipeDef? SelectRecipe(
            ImplantCatalogEntry entry, Pawn pawn, BodyPartRecord part)
        {
            List<RecipeDef> recipes = entry.SurgeryRecipes;
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDef recipe = recipes[i];
                if (recipe.appliedOnFixedBodyParts == null
                    || !recipe.appliedOnFixedBodyParts.Contains(part.def))
                    continue;
                if (!recipe.AvailableNow || !recipe.AvailableOnNow(pawn, part))
                    continue;
                return recipe;
            }
            return null;
        }

        /// Retry and release gating: surgery is released only when the pawn
        /// has no bleeding and no untreated tendable injuries. Anesthesia is
        /// the deliberate exception, so a retry can join the same sedation
        /// window; unconsciousness from any other cause suspends scheduling
        /// until the pawn recovers.
        private static bool HealthGate(Pawn pawn)
        {
            if (pawn.health.hediffSet.HasHediff(HediffDefOf.Anesthetic))
                return true;
            if (pawn.Downed) return false;
            if (pawn.health.hediffSet.BleedRateTotal > 0f) return false;
            return !pawn.health.HasHediffsNeedingTend();
        }
    }
}
