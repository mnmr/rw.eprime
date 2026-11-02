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

        internal static void Reset()
        {
            PlannerProduction.Reset();
        }

        internal static void Tick(ImplannerStore store)
        {
            bool boundaryHit = Find.TickManager.TicksGame % BoundaryTicks == 0;
            if (!boundaryHit && !store.PendingReconcile) return;
            Reconcile(store, boundaryHit);
        }

        /// Reservations must still improve the current part, including after
        /// a wound heals while another slot holds the batch back.
        private static bool MeetsPlanMinimum(Pawn pawn, PawnEvaluation evaluation,
            string goalKey, Thing item)
        {
            if (!GoalKeys.TryResolveImplantSlot(evaluation.Goals, goalKey,
                    out ImplantGoal goal, out PartAddress slot))
                return true;
            ImplantCatalogEntry? entry = Catalogs.ImplantByDefName(goal.ImplantDefName);
            BodyPartRecord? part = entry != null
                ? PawnProjection.ResolveSlotPart(pawn, entry, slot) : null;
            if (entry == null || part == null) return false;
            int accepted = PawnProjection.AcceptableQualities(
                pawn, entry, part, evaluation.Plan.MinQuality);
            return ImplantQuality.Accepts(accepted, ImplantQualities.QualityOf(item));
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
            ColonyIndex index = ColonyIndex.Build(model);
            var pass = new ReconcilePass(model, index, boundaryHit);

            change |= store.SeedSurgeryConcurrency(index.PawnsById.Count);

            // Reservation lifecycle: release-and-report. A reservation exists
            // while its missing goal is in the active batch or has a live
            // owned operation, AND the pawn can still collect the item.
            // Delivered goals, abandoned batches without an operation,
            // goals removed, pawn gone, item destroyed,
            // forbidden, taken, or the pawn now settled at a different
            // colony — releases it. A pawn merely away (caravan, mission,
            // in flight) keeps its reservations: it may return, and
            // re-allocation on return is automatic either way. An item below
            // the plan's minimum quality or the current part's efficiency
            // floor (including healing) is released too. A pushed-out
            // implant's reservation, or an upgrade's item,
            // lives as long as its reinstall record, whatever the plan says.
            var reservationIds = new List<int>(model.Reservations.Keys);
            reservationIds.Sort();
            for (int i = 0; i < reservationIds.Count; i++)
            {
                int itemId = reservationIds[i];
                model.TryGetReservation(itemId, out ItemReservation reservation);
                bool reinstall = model.HasReinstall(
                    reservation.PawnId, reservation.GoalKey);
                PawnEvaluation? evaluation = reinstall
                    ? null
                    : pass.Evaluate(reservation.PawnId);
                bool scheduled = false;
                if (evaluation != null && evaluation.Missing.Contains(reservation.GoalKey)
                    && index.PawnsById.TryGetValue(reservation.PawnId, out Pawn? patient))
                {
                    string? billId = model.OwnedBill(reservation.PawnId, reservation.GoalKey);
                    scheduled = billId != null && pass.FindBill(patient.BillStack, billId) is Bill_Medical;
                }
                bool valid = (reinstall
                        || (evaluation != null
                            && (evaluation.BatchKeys.Contains(reservation.GoalKey) || scheduled)))
                    && index.ItemsById.TryGetValue(itemId, out Thing? item)
                    && !item.IsForbidden(Faction.OfPlayer)
                    && index.PawnMayCollect(reservation.PawnId, itemId)
                    && (reinstall
                        ? PlannerReinstall.AcceptsReservedItem(model, index.PawnsById[reservation.PawnId], reservation.GoalKey, item)
                        : MeetsPlanMinimum(index.PawnsById[reservation.PawnId], evaluation!, reservation.GoalKey, item));
                if (!valid)
                    change |= model.ReleaseReservation(itemId);
            }

            change |= PlannerSurgery.Reconcile(store, pass);
            change |= PlannerBenches.Reconcile(store, pass);
            change |= PlannerProduction.Reconcile(store, pass);

            // The pass's own mutations publish without requesting another
            // pass, and the request that triggered this one is consumed.
            store.PublishPass(change);
            store.ClearPendingReconcile();
        }
    }
}
