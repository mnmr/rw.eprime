using System;
using HarmonyLib;
using QualityJobs.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace QualityJobs.Patches
{
    /// The sole enforcement point (spec §5). No unqualified pawn can create a
    /// managed quality product: product creation is strictly downstream of
    /// this toil.
    [HarmonyPatch(typeof(Toils_Recipe), nameof(Toils_Recipe.CheckIfRecipeCanFinishNow))]
    public static class Patch_CompletionGate
    {
        public static void Postfix(Toil __result)
        {
            Toil toil = __result;
            Action? vanilla = toil.initAction;
            toil.initAction = () =>
            {
                // toil.actor is Pawn from a nullable-oblivious assembly; it is
                // guaranteed non-null when the toil's initAction executes (the
                // job driver sets actor before the toil runs), so no null guard
                // is needed at the call site. TryPause accepts Pawn? defensively.
                if (TryPause(toil.actor)) return;
                vanilla?.Invoke();
            };
        }

        /// Returns true = paused (job ended by us); false = let vanilla proceed.
        private static bool TryPause(Pawn? actor)
        {
            // Early null guard: subsequent code uses actor non-null.
            if (actor == null) return false;

            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return false;

            Job? job = actor.jobs.curJob;
            if (!(job?.bill is Bill_ProductionWithUft bill)) return false;
            if (!(job.GetTarget(TargetIndex.B).Thing is UnfinishedThing uft)) return false;
            if (!ManagedRecipes.IsManagedRecipe(bill.recipe)) return false;

            BillConfig config = store.ConfigFor(bill);
            bool managed = config.Managed || store.IsFinishBill(bill);
            // Unmanaged bills complete vanilla-style; skip the def scans below.
            if (!managed) return false;

            // Finish bills inherit the source bill's condition (spec §7) —
            // registered in the store at dispatch, so ConfigFor covers them;
            // the entry-based path keeps the source bill authoritative while
            // it is alive.
            WorkItemEntry? entry = store.FindByUft(uft);
            ResumeCondition condition = entry != null
                ? Dispatcher.ConditionFor(store, entry)
                : config.Condition;

            CandidateFacts worker = Dispatcher.FactsFor(actor, bill.recipe,
                Dispatcher.WorkTypeForRecipe(bill.recipe));
            GateOutcome outcome = GateDecision.Decide(managed, uft.debugCompleted,
                worker, condition);
            if (outcome == GateOutcome.Complete) return false;

            // Pause (spec §5): end job, unbind, register, clear author.
            // actor is non-null (established above); used directly without ?.
            // EndCurrentJob(Succeeded) here is safe against synchronous re-take:
            // for a stationary pawn it starts a 1-tick Wait_MaintainPosture job
            // and returns, so the pawn re-thinks only after the lock below has
            // landed. Registering before clearing the author closes the window
            // where an exception could leave the item authorless and untracked.
            //
            // C1: when re-pausing via a finish bill, do not overwrite the entry's
            // existing sourceBill/snapshot — the entry already holds the real source
            // bill and the original style snapshot from the first pause. Passing the
            // finish bill here would corrupt originalCreator and lose the source bill.
            bool isFinishBill = store.IsFinishBill(bill);
            Pawn originalCreator = UftAuthor.Get(uft) ?? actor;
            StyleSnapshot? snapshot = isFinishBill ? null : StyleSnapshot.From(bill);
            actor.jobs.EndCurrentJob(JobCondition.Succeeded);
            uft.BoundBill = null;
            store.RegisterPaused(uft, originalCreator,
                isFinishBill ? null : bill,
                snapshot);
            UftAuthor.Clear(uft);
            return true;
        }
    }
}
