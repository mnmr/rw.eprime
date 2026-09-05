using System.Collections.Generic;
using QualityJobs.Core;
using QualityJobs.Patches;
using RimWorld;
using Verse;
using Verse.AI;

namespace QualityJobs
{
    /// WorkGiver_Scanner for dispatched finishers. Generated WorkGiverDefs
    /// (one per relevant work type) give this scanner a priorityInType above
    /// all vanilla peers so the dispatched finisher prefers the finish job over
    /// anything else in the same work type.
    ///
    /// ShouldSkip: allocation-free indexed loops and reference compares.
    ///   Returns true (skip) unless the store is active AND this pawn has at
    ///   least one Dispatched entry/plan whose work type matches def.workType.
    ///
    /// PotentialWorkThingsGlobal: returns only the specific bench or frame for
    ///   each matching dispatched entry/plan. Allocation per call is acceptable
    ///   here — vanilla scanners allocate the same way — and ShouldSkip gates
    ///   the common case (non-dispatched pawns never reach this).
    ///
    /// JobOnThing: re-checks the store state (belt-and-braces alongside the
    ///   lock patches) then produces the appropriate job via either FinishFrame
    ///   or the shared FinishUftJobHelper.
    public class WorkGiver_FinishQualityWork : WorkGiver_Scanner
    {
        // PathEndMode and MaxPathDanger are per-work-type:
        //   Construction (frame path): PathEndMode.Touch / Danger.Deadly
        //     — mirrors WorkGiver_ConstructFinishFrames.
        //   Bill work (bench path): PathEndMode.InteractionCell / Danger.Some
        //     — mirrors WorkGiver_DoBill (Decompiled\RimWorld\WorkGiver_DoBill.cs).
        // Because one scanner class serves both work types (distinct WorkGiverDef
        // instances are generated per work type), we check def.workType at runtime.
        public override PathEndMode PathEndMode
            => def.workType == WorkTypeDefOf.Construction
                ? PathEndMode.Touch
                : PathEndMode.InteractionCell;

        public override Danger MaxPathDanger(Pawn pawn)
            => def.workType == WorkTypeDefOf.Construction
                ? Danger.Deadly
                : Danger.Some;

        // PotentialWorkThingRequest is not used when PotentialWorkThingsGlobal
        // returns non-null (JobGiver_Work.cs:150 passes enumerable!=null to
        // GenClosest which uses the explicit set). Return Undefined as a safe
        // no-op for the rare case where the engine falls back to it.
        public override ThingRequest PotentialWorkThingRequest
            => ThingRequest.ForGroup(ThingRequestGroup.Undefined);

        /// Cheap pre-filter: skip if store is inactive or this pawn has no
        /// matching dispatched entry or plan. Allocation-free. With the
        /// high-priority option on, Patch_FinisherPriority issues this giver's
        /// job ahead of the list walk, so the list position is never used.
        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return true;
            if (store.highPriorityFinish) return true;
            return !HasDispatchFor(store, pawn);
        }

        /// High-priority path: the same work the scanner walk does for this
        /// giver, minus the list position. Mirrors the gates JobGiver_Work puts
        /// around a scanner: PawnCanUseWorkGiver (JobGiver_Work.cs:268-295), the
        /// work-type priority gate of Pawn_WorkSettings.CacheWorkGiversInOrder,
        /// and the forbidden/reachability validator of the closest-thing search.
        /// Allocation-free until a job is built.
        internal Job? TryIssueDirectly(QualityJobsStore store, Pawn pawn)
        {
            if (!HasDispatchFor(store, pawn)) return null;
            if (!pawn.workSettings.WorkIsActive(def.workType)) return null;
            // ShouldSkip is the list-walk gate and reports skip in this mode;
            // HasDispatchFor above is its equivalent here.
            if (!WorkGiverGates.PawnCanUseWorkGiver(pawn, this, checkShouldSkip: false))
                return null;

            PathEndMode pathEnd = PathEndMode;
            Danger danger = MaxPathDanger(pawn);
            bool isConstruction = def.workType == WorkTypeDefOf.Construction;

            if (!isConstruction)
            {
                List<WorkItemEntry> entries = store.entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    WorkItemEntry e = entries[i];
                    if (e.state != WorkItemState.Dispatched) continue;
                    if (e.finisher != pawn) continue;
                    RecipeDef? recipe = e.uft?.Recipe;
                    if (recipe == null) continue;
                    if (Dispatcher.WorkTypeForRecipe(recipe) != def.workType) continue;
                    if (!(e.finishBill?.billStack?.billGiver is Thing bench)) continue;
                    Job? job = TryJobOn(pawn, bench, pathEnd, danger);
                    if (job != null) return job;
                }
            }

            if (isConstruction)
            {
                List<ConstructionPlan> plans = store.plans;
                for (int i = 0; i < plans.Count; i++)
                {
                    ConstructionPlan p = plans[i];
                    if (p.state != ConstructionPlanState.Dispatched) continue;
                    if (p.finisher != pawn) continue;
                    if (!(p.target is Thing frame)) continue;
                    Job? job = TryJobOn(pawn, frame, pathEnd, danger);
                    if (job != null) return job;
                }
            }

            return null;
        }

        private Job? TryJobOn(Pawn pawn, Thing t, PathEndMode pathEnd, Danger danger)
        {
            if (t.IsForbidden(pawn)) return null;
            if (!pawn.CanReach(t, pathEnd, danger)) return null;
            return JobOnThing(pawn, t);
        }

        /// True when this pawn has at least one Dispatched entry or plan for
        /// this giver's work type. Allocation-free indexed loops.
        private bool HasDispatchFor(QualityJobsStore store, Pawn pawn)
        {
            bool isConstruction = def.workType == WorkTypeDefOf.Construction;

            // Check bill entries (bench work).
            if (!isConstruction)
            {
                List<WorkItemEntry> entries = store.entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    WorkItemEntry e = entries[i];
                    if (e.state != WorkItemState.Dispatched) continue;
                    if (e.finisher != pawn) continue;
                    // Check that this entry's recipe maps to our work type.
                    RecipeDef? recipe = e.uft?.Recipe;
                    if (recipe == null) continue;
                    WorkTypeDef? wt = Dispatcher.WorkTypeForRecipe(recipe);
                    if (wt == def.workType) return true;
                }
            }

            // Check construction plans.
            if (isConstruction)
            {
                List<ConstructionPlan> plans = store.plans;
                for (int i = 0; i < plans.Count; i++)
                {
                    ConstructionPlan p = plans[i];
                    if (p.state != ConstructionPlanState.Dispatched) continue;
                    if (p.finisher == pawn) return true;
                }
            }

            return false;
        }

        /// Returns the specific things this pawn should consider as finisher.
        /// Bench path: the bench Thing on which the finish bill sits.
        /// Frame path: the Frame thing itself.
        /// Iterator allocation per call is acceptable here (vanilla scanners
        /// allocate the same way); ShouldSkip gates the common case.
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) yield break;

            bool isConstruction = def.workType == WorkTypeDefOf.Construction;

            if (!isConstruction)
            {
                List<WorkItemEntry> entries = store.entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    WorkItemEntry e = entries[i];
                    if (e.state != WorkItemState.Dispatched) continue;
                    if (e.finisher != pawn) continue;
                    RecipeDef? recipe = e.uft?.Recipe;
                    if (recipe == null) continue;
                    if (Dispatcher.WorkTypeForRecipe(recipe) != def.workType) continue;
                    // Yield the bench Thing (IBillGiver) where the finish bill lives.
                    if (e.finishBill?.billStack?.billGiver is Thing bench)
                        yield return bench;
                }
            }

            if (isConstruction)
            {
                List<ConstructionPlan> plans = store.plans;
                for (int i = 0; i < plans.Count; i++)
                {
                    ConstructionPlan p = plans[i];
                    if (p.state != ConstructionPlanState.Dispatched) continue;
                    if (p.finisher != pawn) continue;
                    if (p.target is Thing frame) yield return frame;
                }
            }
        }

        /// Produce the finish job for the thing.
        /// Belt-and-braces: re-checks the store; the lock patches also guard.
        public override Job? JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return null;

            // ---- Frame path (Construction) -----------------------------------------------
            if (t is Frame frame)
            {
                ConstructionPlan? plan = store.FindPlan(t);
                if (plan == null || plan.state != ConstructionPlanState.Dispatched
                    || plan.finisher != pawn)
                    return null;

                // Mirror WorkGiver_ConstructFinishFrames.JobOnThing checks.
                if (t.Faction != pawn.Faction) return null;
                if (!frame.IsCompleted()) return null;
                if (!GenConstruct.CanTouchTargetFromValidCell(frame, pawn)) return null;
                Thing? blocker = GenConstruct.FirstBlockingThing(frame, pawn);
                if (blocker != null)
                    return GenConstruct.HandleBlockingThingJob(frame, pawn, forced);
                if (!GenConstruct.CanConstruct(frame, pawn, checkSkills: true, forced))
                    return null;
                return JobMaker.MakeJob(JobDefOf.FinishFrame, frame);
            }

            // ---- Bench path (bill work) --------------------------------------------------
            // Mirror vanilla WorkGiver_DoBill.JobOnThing bench gates FIRST
            // (Decompiled\RimWorld\WorkGiver_DoBill.cs:141-157).
            // Without these checks an occupied/unpowered/burning bench causes the
            // top-priority giver to churn jobs that the driver immediately kills.
            if (!(t is IBillGiver giver)) return null;
            // CurrentlyUsableForBills: power, temperature, and similar gate.
            // UsableForBillsAfterFueling: also covers fuel — when false the bench
            //   needs refueling. We are not a refueling giver; return null so the
            //   vanilla refueling flow handles it.
            // Mirror WorkGiver_DoBill.JobOnThing:141-157.
            if (!giver.CurrentlyUsableForBills()) return null;
            if (!giver.UsableForBillsAfterFueling()) return null;
            if (!pawn.CanReserve(t, 1, -1, null, forced)) return null;
            if (t.IsBurning()) return null;
            if (t.def.hasInteractionCell
                && !pawn.CanReserveSittableOrSpot(t.InteractionCell, t, forced))
                return null;

            // Find the matching dispatched entry whose finish bill sits on this bench.
            List<WorkItemEntry> entries = store.entries;
            for (int i = 0; i < entries.Count; i++)
            {
                WorkItemEntry e = entries[i];
                if (e.state != WorkItemState.Dispatched) continue;
                if (e.finisher != pawn) continue;

                Bill_ProductionWithUft? bill = e.finishBill;
                if (bill == null || bill.DeletedOrDereferenced) continue;
                if (bill.billStack?.billGiver as Thing != t) continue;

                // Validate: match work type.
                RecipeDef? recipe = e.uft?.Recipe;
                if (recipe == null) continue;
                if (Dispatcher.WorkTypeForRecipe(recipe) != def.workType) continue;

                // Mirror vanilla StartOrResumeBillJob skill/anew gates
                // (Decompiled\RimWorld\WorkGiver_DoBill.cs:194-203).
                if (!bill.ShouldDoNow()) continue;
                if (!bill.PawnAllowedToStartAnew(pawn)) continue;
                if (recipe.FirstSkillRequirementPawnDoesntSatisfy(pawn) != null) continue;

                UnfinishedThing? uft = e.uft;
                if (uft == null || !uft.Spawned) continue;
                if (uft.IsForbidden(pawn)) continue;
                if (!pawn.CanReserveAndReach(uft, PathEndMode.Touch, Danger.Deadly)) continue;

                return FinishUftJobHelper.BuildFinishUftJob(pawn, uft, bill);
            }

            return null;
        }
    }
}
