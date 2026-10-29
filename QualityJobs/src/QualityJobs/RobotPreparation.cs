using QualityJobs.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace QualityJobs
{
    /// Read-only admission policy. Scanners and actual job reservations share
    /// these checks so a queued job cannot retain an obsolete permission.
    internal static class RobotPreparation
    {
        internal static bool AllowsBillWork(QualityJobsStore store, Pawn pawn,
            Bill bill, UnfinishedThing? uft = null)
        {
            if (store.allowRobotPreparation || !WorkerCapabilities.IsMechanical(pawn))
                return true;
            if (!(bill is Bill_ProductionWithUft)
                || !ManagedRecipes.IsManagedRecipe(bill.recipe))
                return true;

            WorkItemEntry? entry = uft != null ? store.FindByUft(uft) : null;
            bool managed = store.ConfigFor(bill).Managed;
            // A shared item keeps the policy of its live source bill. Otherwise
            // an unmanaged copy of the same recipe could bypass the restriction.
            Bill_ProductionWithUft? source = uft != null ? store.PreparationSourceFor(uft) : null;
            source ??= entry?.sourceBill;
            if (QualityJobsStore.IsLivePreparationSource(source))
                managed |= store.ConfigFor(source!).Managed;

            bool dispatched = false;
            if (entry != null)
                dispatched = IsAssignedFinisher(entry, pawn, bill);
            else
            {
                // Bill admission runs before a UFT has been selected. Only the
                // exact assigned finisher may use a dispatched one-shot bill.
                for (int i = 0; i < store.entries.Count; i++)
                    if (IsAssignedFinisher(store.entries[i], pawn, bill))
                    {
                        dispatched = true;
                        break;
                    }
            }

            return PreparationPolicy.Allows(store.allowRobotPreparation,
                mechanical: true, managedWork: managed,
                dispatchedFinisher: dispatched);
        }

        private static bool IsAssignedFinisher(WorkItemEntry entry, Pawn pawn,
            Bill bill)
            => entry.state == WorkItemState.Dispatched
               && ReferenceEquals(entry.finisher, pawn)
               && ReferenceEquals(entry.finishBill, bill);

        internal static bool AllowsFrameWork(QualityJobsStore store, Pawn pawn,
            Thing? target)
        {
            if (store.allowRobotPreparation || !WorkerCapabilities.IsMechanical(pawn)
                || target == null)
                return true;
            ConstructionPlan? plan = store.FindPlan(target);
            return PreparationPolicy.Allows(store.allowRobotPreparation,
                mechanical: true, managedWork: plan != null && !plan.restoreOnly,
                dispatchedFinisher: plan != null
                    && plan.state == ConstructionPlanState.Dispatched
                    && ReferenceEquals(plan.finisher, pawn));
        }

        internal static bool AllowsJob(QualityJobsStore store, Pawn pawn, Job? job)
        {
            if (job == null || store.allowRobotPreparation) return true;
            if (job.def == JobDefOf.FinishFrame)
                return AllowsFrameWork(store, pawn, job.targetA.Thing);
            if (job.def != JobDefOf.DoBill || job.bill == null) return true;

            UnfinishedThing? uft = job.targetB.Thing as UnfinishedThing;
            var queue = job.targetQueueB;
            if (uft == null && queue != null && queue.Count == 1)
                uft = queue[0].Thing as UnfinishedThing;
            return AllowsBillWork(store, pawn, job.bill, uft);
        }

        /// Called only by the synced setting change and deterministic load.
        /// Ongoing jobs cannot bypass a newly disabled setting, and restored
        /// current toils do not need a per-tick permission check.
        internal static void StopBlockedCurrentJobs(QualityJobsStore store)
        {
            if (store.allowRobotPreparation) return;
            var maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                var pawns = maps[m].mapPawns.AllPawnsSpawned;
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    if (!AllowsJob(store, pawn, pawn.CurJob))
                        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced,
                            startNewJob: false);
                }
            }
        }
    }
}
