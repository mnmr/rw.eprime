using System;
using System.Collections.Generic;
using Implanner.Core;
using RimShared.Common;
using RimWorld;
using Verse;
using Plan = Implanner.Core.Plan;

namespace Implanner
{
    /// One pawn's evaluation for the current pass: the assigned plan, its
    /// effective goals, the slots still missing, and the active batch. Built
    /// once per pawn per pass; nothing inside a pass changes goals,
    /// assignments, rankings, or installed implants, so every phase reads
    /// the same projection.
    internal sealed class PawnEvaluation
    {
        private HashSet<string>? batchKeys;

        internal PawnEvaluation(Plan plan, IReadOnlyList<ImplantGoal> goals,
            List<string> missing, List<string> batch)
        {
            Plan = plan;
            Goals = goals;
            Missing = missing;
            Batch = batch;
        }

        internal Plan Plan { get; }
        internal IReadOnlyList<ImplantGoal> Goals { get; }
        internal List<string> Missing { get; }
        internal List<string> Batch { get; }

        /// Batch membership implies missing, so this is the single
        /// reservation-validity set.
        internal HashSet<string> BatchKeys =>
            batchKeys ??= new HashSet<string>(Batch, StringComparer.Ordinal);
    }

    /// Pass-scoped context shared by every phase of one reconcile pass:
    /// the colony index, per-pawn evaluations memoized on first demand,
    /// and bill load ids resolved once per bill (GetUniqueLoadID allocates
    /// a string per call). Created at the top of a pass and discarded with
    /// it, never retained.
    internal sealed class ReconcilePass
    {
        private readonly Dictionary<int, IReadOnlyList<ImplantGoal>> goalsByPlan =
            new Dictionary<int, IReadOnlyList<ImplantGoal>>();
        private readonly Dictionary<int, PawnEvaluation?> evaluations =
            new Dictionary<int, PawnEvaluation?>();
        private readonly Dictionary<Bill, string> billIds =
            new Dictionary<Bill, string>(ReferenceIdentityComparer<Bill>.Instance);

        internal ReconcilePass(PlannerModel model, ColonyIndex index, bool boundaryHit)
        {
            Model = model;
            Index = index;
            BoundaryHit = boundaryHit;
        }

        internal PlannerModel Model { get; }
        internal ColonyIndex Index { get; }

        /// Whether this pass sits on the approved 1020-tick boundary (pure
        /// tick arithmetic, identical on every client); the tick-driven
        /// phases run only then, a pending-flag pass runs the correctness
        /// phases only.
        internal bool BoundaryHit { get; }

        /// The pawn's evaluation, or null when the pawn is not a live
        /// planable colonist or has no assigned plan.
        internal PawnEvaluation? Evaluate(int pawnId)
        {
            if (evaluations.TryGetValue(pawnId, out PawnEvaluation? known))
                return known;
            PawnEvaluation? built = null;
            if (Index.PawnsById.TryGetValue(pawnId, out Pawn pawn))
            {
                Plan? plan = Model.AssignedPlan(pawnId);
                if (plan != null)
                {
                    IReadOnlyList<ImplantGoal> goals = EffectiveGoals(plan);
                    List<string> missing = PawnProjection.MissingImplantSlotKeys(
                        pawn, goals);
                    List<string> batch = SurgeryPlanner.ComputeBatch(
                        missing, Model, goals, Model.Iteration);
                    built = new PawnEvaluation(plan, goals, missing, batch);
                }
            }
            evaluations.Add(pawnId, built);
            return built;
        }

        private IReadOnlyList<ImplantGoal> EffectiveGoals(Plan plan)
        {
            if (!goalsByPlan.TryGetValue(plan.Id, out IReadOnlyList<ImplantGoal> goals))
            {
                goals = Model.EffectiveImplants(plan);
                goalsByPlan.Add(plan.Id, goals);
            }
            return goals;
        }

        /// The bill's load id, resolved once per pass.
        internal string BillId(Bill bill)
        {
            if (!billIds.TryGetValue(bill, out string id))
            {
                id = bill.GetUniqueLoadID();
                billIds.Add(bill, id);
            }
            return id;
        }

        /// The bill with the given load id on the stack, or null.
        internal Bill? FindBill(BillStack? stack, string billId)
        {
            if (stack == null) return null;
            for (int i = 0; i < stack.Count; i++)
                if (string.Equals(BillId(stack[i]), billId, StringComparison.Ordinal))
                    return stack[i];
            return null;
        }
    }
}
