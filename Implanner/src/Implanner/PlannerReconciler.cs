using System;
using System.Collections.Generic;
using Implanner.Core;
using RimShared.Common;
using RimWorld;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner
{
    /// The deterministic tick-boundary reconciliation pass (the store's third
    /// mutation class): delivery latches, reservation lifecycle, and implant
    /// surgery automation. Runs inside the synchronized tick path only,
    /// consumes only authoritative synchronized state and deterministic tick
    /// arithmetic, so every multiplayer client derives the identical
    /// mutations from the same tick.
    ///
    /// Cadence: the canonical 204-game-tick boundary for game-derived drift
    /// (implant and stock changes), plus an immediate next-tick pass after
    /// any synced store mutation (user edits stay correctness-fresh,
    /// including while paused). One pass at most per tick.
    internal static class PlannerReconciler
    {
        private static readonly FixedTickBoundaryGate boundary =
            new FixedTickBoundaryGate(204);
        private static int observedStoreVersion = -1;
        private static bool stoodDown;

        internal static void Reset()
        {
            boundary.Reset();
            observedStoreVersion = -1;
            stoodDown = false;
            PlannerSurgery.Reset();
            PlannerProduction.Reset();
        }

        internal static void Tick(ImplannerStore store)
        {
            // A level mod is active: the assistant half of Implanner cannot
            // deliver across a level boundary, so it stands down entirely
            // (PlannerAutomation). One cleanup pass releases whatever a
            // previous session reserved or scheduled; after that this is a
            // plain early return on every tick.
            if (!PlannerAutomation.Available)
            {
                if (!stoodDown) StandDown(store);
                return;
            }

            bool boundaryHit = boundary.Observe(Find.TickManager.TicksGame);
            bool storeDirty = store.Version != observedStoreVersion;
            if (!boundaryHit && !storeDirty) return;
            Reconcile(store);
            // Include this pass's own mutations so the next tick is clean.
            observedStoreVersion = store.Version;
        }

        /// Hands the colony back to the player: every reservation is released
        /// so no stock stays locked to a pawn that can never collect it, and
        /// Implanner forgets which operations it owned. The bills themselves
        /// are left on their pawns — they are real game objects the player may
        /// now be relying on, and a matching bill still counts as scheduled if
        /// the level mod is ever removed. Deterministic order, so every
        /// multiplayer client stands down identically.
        private static void StandDown(ImplannerStore store)
        {
            stoodDown = true;
            PlannerModel model = store.Model;
            var change = PlannerChange.None;

            var itemIds = new List<int>(model.Reservations.Keys);
            itemIds.Sort();
            for (int i = 0; i < itemIds.Count; i++)
                change |= model.ReleaseReservation(itemIds[i]);

            var billPawnIds = new List<int>(model.OwnedBills.Keys);
            billPawnIds.Sort();
            for (int i = 0; i < billPawnIds.Count; i++)
            {
                Dictionary<string, string>? owned =
                    model.OwnedBillsFor(billPawnIds[i]);
                if (owned == null) continue;
                var goalKeys = new List<string>(owned.Keys);
                goalKeys.Sort(StringComparer.Ordinal);
                for (int k = 0; k < goalKeys.Count; k++)
                    change |= model.RemoveOwnedBill(billPawnIds[i], goalKeys[k]);
            }

            store.Bump(change);
            if (change != PlannerChange.None)
                Log.Message("[Implanner] Released " + itemIds.Count
                    + " reservation(s) and dropped " + billPawnIds.Count
                    + " owned-operation record(s): automation is unavailable "
                    + "with " + PlannerAutomation.BlockedBy + " active.");
        }

        private static void Reconcile(ImplannerStore store)
        {
            PlannerModel model = store.Model;
            var change = PlannerChange.None;

            // The pass's single source of colony structure, colonists, and
            // items — canonicalization and faction resolution happen only
            // inside the index.
            ColonyIndex index = ColonyIndex.Build();

            var assignedPawnIds = new List<int>(model.Assignments.Keys);
            assignedPawnIds.Sort();

            // Effective goal lists resolved once per plan for the whole pass.
            var effectiveByPlan = new Dictionary<int, List<ImplantGoal>>();

            // Evaluate every assigned, present pawn; latch deliveries.
            for (int i = 0; i < assignedPawnIds.Count; i++)
            {
                int pawnId = assignedPawnIds[i];
                if (!index.PawnsById.TryGetValue(pawnId, out Pawn pawn)) continue;
                Plan? plan = model.AssignedPlan(pawnId);
                if (plan == null) continue;
                List<ImplantGoal> goals = EffectiveGoals(model, effectiveByPlan, plan);
                change |= model.PruneLatches(pawnId, goals);
                bool away = index.ColonyOfPawn(pawnId) == null;
                PlanEvaluation evaluation = PawnProjection.Evaluate(
                    pawn, goals, away, model.LatchesFor(pawnId));
                List<string> satisfied = evaluation.SatisfiedGoalKeys;
                for (int k = 0; k < satisfied.Count; k++)
                    change |= model.Latch(pawnId, satisfied[k]);
            }

            // Reservation lifecycle: release-and-report. A reservation exists
            // only while its goal is actively pursued AND the pawn can still
            // collect the item; anything else — delivered (latched), goal
            // removed, pawn gone, item destroyed, forbidden, taken, or the
            // pawn now settled at a different colony — releases it. A pawn
            // merely away (caravan, mission) keeps its reservations: it may
            // return, and re-allocation on return is automatic either way.
            var reservationIds = new List<int>(model.Reservations.Keys);
            reservationIds.Sort();
            for (int i = 0; i < reservationIds.Count; i++)
            {
                int itemId = reservationIds[i];
                model.TryGetReservation(itemId, out ItemReservation reservation);
                Plan? plan = model.AssignedPlan(reservation.PawnId);
                bool valid = plan != null
                    && index.PawnsById.ContainsKey(reservation.PawnId)
                    && GoalKeys.Contains(
                        EffectiveGoals(model, effectiveByPlan, plan!), reservation.GoalKey)
                    && !model.IsLatched(reservation.PawnId, reservation.GoalKey)
                    && index.ItemsById.TryGetValue(itemId, out Thing item)
                    && !item.IsForbidden(Faction.OfPlayer)
                    && index.PawnMayCollect(reservation.PawnId, itemId);
                if (!valid)
                    change |= model.ReleaseReservation(itemId);
            }

            change |= PlannerSurgery.Reconcile(store, index);
            change |= PlannerProduction.Reconcile(store, index);

            store.Bump(change);
            // The production boundary tracks its own domain revision; fold
            // this pass's just-bumped bill bookkeeping into its observation
            // so the next reconcile trigger does not read the pass's own
            // mutations as a player edit and re-dispatch early.
            PlannerProduction.NotePassCompleted(store);
        }

        private static List<ImplantGoal> EffectiveGoals(PlannerModel model,
            Dictionary<int, List<ImplantGoal>> memo, Plan plan)
        {
            if (!memo.TryGetValue(plan.Id, out List<ImplantGoal> goals))
            {
                goals = model.EffectiveImplants(plan);
                memo.Add(plan.Id, goals);
            }
            return goals;
        }
    }
}
