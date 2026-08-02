using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Deletes our one-shot bill and drops the entry when it completes its
    /// single iteration (spec §7 deletion triggers). Runs after vanilla's
    /// repeat-count decrement.
    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.Notify_IterationCompleted))]
    public static class Patch_BillCompleted
    {
        public static void Postfix(Bill_Production __instance, Pawn billDoer, List<Thing> ingredients)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            for (int i = store.entries.Count - 1; i >= 0; i--)
            {
                WorkItemEntry e = store.entries[i];
                if (e.finishBill == __instance)
                {
                    Dispatcher.CompleteDispatch(store, e);
                    return;
                }
            }
        }
    }
}
