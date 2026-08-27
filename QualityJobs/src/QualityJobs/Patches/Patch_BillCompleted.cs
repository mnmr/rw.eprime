using System.Collections.Generic;
using HarmonyLib;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Owns retry and one-shot accounting around vanilla bill completion.
    /// Retries and finish bills suppress vanilla counter/message handling while
    /// preserving the recipe worker callback exactly once.
    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.Notify_IterationCompleted))]
    public static class Patch_BillCompleted
    {
        public static bool Prefix(Bill_Production __instance, Pawn billDoer,
            List<Thing> ingredients, ref bool __state)
        {
            __state = false;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return true;

            bool isFinishBill = store.IsFinishBill(__instance);
            bool retry = store.ConsumeBillRetry(__instance);
            __state = retry;
            if (BillLifecyclePolicy.ShouldRunVanillaCompletion(isFinishBill, retry))
                return true;

            // Bill_Production.Notify_IterationCompleted does only repeat-count
            // accounting/message work plus this recipe callback. We replace the
            // former, so preserve the latter explicitly and exactly once.
            __instance.recipe.Worker.Notify_IterationCompleted(billDoer, ingredients);
            return false;
        }

        public static void Postfix(Bill_Production __instance, Pawn billDoer,
            List<Thing> ingredients, bool __state)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            store.InvalidateManagedJobs();
            for (int i = store.entries.Count - 1; i >= 0; i--)
            {
                WorkItemEntry e = store.entries[i];
                if (ReferenceEquals(e.finishBill, __instance))
                {
                    Dispatcher.CompleteDispatch(store, e, retry: __state);
                    return;
                }
            }
        }
    }
}
