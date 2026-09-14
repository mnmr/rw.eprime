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
    ///   least one Dispatched entry/plan with its own work category enabled.
    /// In normal-priority mode an unchanged giver retains its original scope.
    /// Moving it to another category opens that scope; the finished target
    /// still determines its own work requirements and access rules.
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
        // Frames require Touch / Deadly; bill benches require InteractionCell /
        // Some. Vanilla's scanner search has only one path mode for every target.
        // Bypass that uniform reachability filter and validate the exact target
        // in JobOnThing instead (also called by HasJobOnThing during the search).
        // Unreachable targets still never produce a job.
        public override bool AllowUnreachable => true;

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
        /// giver, independent of its category and list position. The caller
        /// applies pawn/capacity gates (JobGiver_Work.cs:268-295). Target
        /// work categories and reachability are checked here and in JobOnThing.
        /// Allocation-free until a job is built.
        internal Job? TryIssueDirectly(QualityJobsStore store, Pawn pawn)
        {
            if (!HasDispatchFor(store, pawn)) return null;

            List<WorkItemEntry> entries = store.entries;
            for (int i = 0; i < entries.Count; i++)
            {
                WorkItemEntry e = entries[i];
                if (!CanFinishEntry(e, pawn)) continue;
                if (!(e.finishBill?.billStack?.billGiver is Thing bench)) continue;
                Job? job = JobOnThing(pawn, bench);
                if (job != null) return job;
            }

            List<ConstructionPlan> plans = store.plans;
            for (int i = 0; i < plans.Count; i++)
            {
                ConstructionPlan p = plans[i];
                if (!CanFinishPlan(p, pawn)) continue;
                Job? job = JobOnThing(pawn, p.target!);
                if (job != null) return job;
            }

            return null;
        }

        /// True when this pawn has at least one Dispatched entry or plan for
        /// which its item work category is enabled. Allocation-free indexed loops.
        private bool HasDispatchFor(QualityJobsStore store, Pawn pawn)
        {
            List<WorkItemEntry> entries = store.entries;
            for (int i = 0; i < entries.Count; i++)
                if (CanFinishEntry(entries[i], pawn)) return true;

            List<ConstructionPlan> plans = store.plans;
            for (int i = 0; i < plans.Count; i++)
                if (CanFinishPlan(plans[i], pawn)) return true;

            return false;
        }

        private bool CanFinishWork(Pawn pawn, WorkTypeDef? itemCategory)
        {
            if (itemCategory == null || pawn.workSettings == null
                || pawn.WorkTypeIsDisabled(itemCategory)
                || !pawn.workSettings.WorkIsActive(itemCategory)) return false;

            // High-priority scheduling ignores the giver's category entirely.
            if (QualityJobsStore.Active?.highPriorityFinish == true) return true;

            // At normal priority, givers must not pull lower-priority work forward
            // while left in their original categories. Once regrouped, the receiving
            // category schedules any eligible dispatch. Never infer the target's
            // kind, recipe, or access rules from this scheduling comparison.
            FinisherWorkScope? scope = def.GetModExtension<FinisherWorkScope>();
            return scope == null || def.workType != scope.OriginalCategory
                || itemCategory == scope.OriginalCategory;
        }

        private bool CanFinishEntry(WorkItemEntry entry, Pawn pawn)
            => entry.state == WorkItemState.Dispatched && entry.finisher == pawn
                && entry.uft?.Recipe is RecipeDef recipe
                && CanFinishWork(pawn, Dispatcher.WorkTypeForRecipe(recipe));

        private bool CanFinishPlan(ConstructionPlan plan, Pawn pawn)
            => plan.state == ConstructionPlanState.Dispatched && plan.finisher == pawn
                && plan.target is Frame
                && CanFinishWork(pawn, WorkTypeDefOf.Construction);

        /// Returns the specific things this pawn should consider as finisher.
        /// Bench path: the bench Thing on which the finish bill sits.
        /// Frame path: the Frame thing itself.
        /// Iterator allocation per call is acceptable here (vanilla scanners
        /// allocate the same way); ShouldSkip gates the common case.
        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) yield break;

            List<WorkItemEntry> entries = store.entries;
            for (int i = 0; i < entries.Count; i++)
            {
                WorkItemEntry e = entries[i];
                if (!CanFinishEntry(e, pawn)) continue;
                // Yield the bench Thing (IBillGiver) where the finish bill lives.
                if (e.finishBill?.billStack?.billGiver is Thing bench)
                    yield return bench;
            }

            List<ConstructionPlan> plans = store.plans;
            for (int i = 0; i < plans.Count; i++)
            {
                ConstructionPlan p = plans[i];
                if (CanFinishPlan(p, pawn)) yield return p.target!;
            }
        }

        /// Produce the finish job for the thing.
        /// Belt-and-braces: re-checks the store; the lock patches also guard.
        public override Job? JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return null;
            if (!t.Spawned || t.Map != pawn.Map || t.IsForbidden(pawn)) return null;

            // ---- Frame path (Construction) -----------------------------------------------
            if (t is Frame frame)
            {
                ConstructionPlan? plan = store.FindPlan(t);
                if (plan == null || !CanFinishPlan(plan, pawn)) return null;
                if (!pawn.CanReach(frame, PathEndMode.Touch, Danger.Deadly)) return null;

                // Mirror WorkGiver_ConstructFinishFrames.JobOnThing checks.
                if (t.Faction != pawn.Faction) return null;
                if (!frame.IsCompleted()) return null;
                if (!GenConstruct.CanTouchTargetFromValidCell(frame, pawn)) return null;
                Thing? blocker = GenConstruct.FirstBlockingThing(frame, pawn);
                if (blocker != null)
                    return WithTargetWorkGiver(GenConstruct.HandleBlockingThingJob(frame, pawn, forced),
                        FinishWorkGiverDefOf.ConstructFinishFrames, store.highPriorityFinish);
                if (!GenConstruct.CanConstruct(frame, pawn, checkSkills: true, forced))
                    return null;
                return WithTargetWorkGiver(JobMaker.MakeJob(JobDefOf.FinishFrame, frame),
                    FinishWorkGiverDefOf.ConstructFinishFrames, store.highPriorityFinish);
            }

            // ---- Bench path (bill work) --------------------------------------------------
            // Mirror vanilla WorkGiver_DoBill.JobOnThing bench gates FIRST
            // (Decompiled\RimWorld\WorkGiver_DoBill.cs:141-157).
            // Without these checks an occupied/unpowered/burning bench causes the
            // top-priority giver to churn jobs that the driver immediately kills.
            if (!(t is IBillGiver giver)) return null;
            if (!pawn.CanReach(t, PathEndMode.InteractionCell, Danger.Some)) return null;
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
                if (!CanFinishEntry(e, pawn)) continue;

                Bill_ProductionWithUft? bill = e.finishBill;
                if (bill == null || bill.DeletedOrDereferenced) continue;
                if (bill.billStack?.billGiver as Thing != t) continue;

                // Work category comes from this item's recipe, never the giver.
                RecipeDef? recipe = e.uft?.Recipe;
                if (recipe == null) continue;

                // Mirror vanilla StartOrResumeBillJob skill/anew gates
                // (Decompiled\RimWorld\WorkGiver_DoBill.cs:194-203).
                if (!bill.ShouldDoNow()) continue;
                if (!bill.PawnAllowedToStartAnew(pawn)) continue;
                if (recipe.FirstSkillRequirementPawnDoesntSatisfy(pawn) != null) continue;

                UnfinishedThing? uft = e.uft;
                if (uft == null || !uft.Spawned) continue;
                if (uft.IsForbidden(pawn)) continue;
                if (!pawn.CanReserveAndReach(uft, PathEndMode.Touch, Danger.Deadly)) continue;

                return WithTargetWorkGiver(FinishUftJobHelper.BuildFinishUftJob(pawn, uft, bill),
                    Dispatcher.WorkGiverForRecipe(recipe), store.highPriorityFinish);
            }

            return null;
        }

        // Vanilla reads this metadata for active-job cancellation and work-watching
        // skills. High-priority jobs describe the target's work, independent of the
        // QJ giver's placement. Normal scheduling supplies its own giver as before.
        private static Job? WithTargetWorkGiver(Job? job, WorkGiverDef? source, bool highPriority)
        {
            if (job == null || !highPriority) return job;
            if (source == null)
            {
                JobMaker.ReturnToPool(job);
                return null;
            }
            job.workGiverDef = source;
            return job;
        }
    }
}
