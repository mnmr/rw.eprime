using System;
using System.Collections.Generic;
using Implanner.Core;
using RimWorld;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner
{
    /// The deterministic tick-boundary reconciliation pass (the store's third
    /// mutation class): reservation lifecycle and implant surgery
    /// automation. Runs inside the synchronized tick path only,
    /// consumes only authoritative synchronized state and deterministic tick
    /// arithmetic, so every multiplayer client derives the identical
    /// mutations from the same tick.
    ///
    /// Cadence: the owner-approved 1020-game-tick boundary for game-derived
    /// drift (implant and stock changes), plus a pass on the next simulated
    /// tick after any synced store mutation (the store's scribed
    /// PendingReconcile flag, set by the command path and cleared here).
    /// Both triggers derive only from synced state — tick arithmetic and a
    /// flag that travels with the save — so a late-joining client never
    /// runs a pass the host does not. Ticks do not advance while the game
    /// is paused, so a paused edit reconciles on the first tick after
    /// unpausing. One pass at most per tick.
    internal static class PlannerReconciler
    {
        /// The reconciliation boundary approved in AGENTS.md, shared by the
        /// doctor-floor publish and production dispatch.
        internal const int BoundaryTicks = 1020;

        private static bool stoodDown;

        internal static void Reset()
        {
            stoodDown = false;
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

            bool boundaryHit = Find.TickManager.TicksGame % BoundaryTicks == 0;
            if (!boundaryHit && !store.PendingReconcile) return;
            Reconcile(store, boundaryHit);
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
                IReadOnlyDictionary<string, string>? owned =
                    model.OwnedBillsFor(billPawnIds[i]);
                if (owned == null) continue;
                var goalKeys = new List<string>(owned.Keys);
                goalKeys.Sort(StringComparer.Ordinal);
                for (int k = 0; k < goalKeys.Count; k++)
                    change |= model.RemoveOwnedBill(billPawnIds[i], goalKeys[k]);
            }

            // Production records drop the same way: the bill objects stay on
            // their benches for the player, but nothing owns them anymore.
            var productionBillIds =
                new List<string>(model.OwnedProductionBills.Keys);
            productionBillIds.Sort(StringComparer.Ordinal);
            for (int i = 0; i < productionBillIds.Count; i++)
                change |= model.RemoveOwnedProductionBill(productionBillIds[i]);

            store.PublishPass(change);
            store.ClearPendingReconcile();
            store.ClearPendingProductionPass();
            if (change != PlannerChange.None)
                Log.Message("[Implanner] Released " + itemIds.Count
                    + " reservation(s) and dropped " + billPawnIds.Count
                    + " owned-operation and " + productionBillIds.Count
                    + " production-bill record(s): automation is unavailable "
                    + "with " + PlannerAutomation.BlockedBy + " active.");
        }

        private static void Reconcile(ImplannerStore store, bool boundaryHit)
        {
            PlannerModel model = store.Model;
            var change = PlannerChange.None;

            // Converge first: state of pawns that no longer exist anywhere
            // is dropped before anything reads the model, so a client that
            // loaded the host's save derives the same model the host holds.
            change |= store.CleanupMissingPawns();

            // The pass's single source of colony structure, colonists, and
            // items — canonicalization and faction resolution happen only
            // inside the index — plus the per-pawn evaluations every phase
            // shares.
            ColonyIndex index = ColonyIndex.Build();
            var pass = new ReconcilePass(model, index, boundaryHit);

            change |= store.SeedSurgeryConcurrency(index.PawnsById.Count);

            // Reservation lifecycle: release-and-report. A reservation exists
            // only while its goal is in the pawn's ACTIVE batch AND the pawn
            // can still collect the item; anything else — delivered (the
            // slot is no longer missing), outside the batch being worked
            // (tier iteration moved on, or a stale cross-tier holding from
            // an older version), goal removed, pawn gone, item destroyed,
            // forbidden, taken, or the pawn now settled at a different
            // colony — releases it. A pawn merely away (caravan, mission,
            // in flight) keeps its reservations: it may return, and
            // re-allocation on return is automatic either way.
            var reservationIds = new List<int>(model.Reservations.Keys);
            reservationIds.Sort();
            for (int i = 0; i < reservationIds.Count; i++)
            {
                int itemId = reservationIds[i];
                model.TryGetReservation(itemId, out ItemReservation reservation);
                PawnEvaluation? evaluation = pass.Evaluate(reservation.PawnId);
                bool valid = evaluation != null
                    && evaluation.BatchKeys.Contains(reservation.GoalKey)
                    && index.ItemsById.TryGetValue(itemId, out Thing item)
                    && !item.IsForbidden(Faction.OfPlayer)
                    && index.PawnMayCollect(reservation.PawnId, itemId);
                if (!valid)
                    change |= model.ReleaseReservation(itemId);
            }

            change |= PlannerSurgery.Reconcile(store, pass);
            change |= PlannerProduction.Reconcile(store, pass);

            // The pass's own mutations publish without requesting another
            // pass, and the request that triggered this one is consumed.
            store.PublishPass(change);
            store.ClearPendingReconcile();
        }
    }
}
