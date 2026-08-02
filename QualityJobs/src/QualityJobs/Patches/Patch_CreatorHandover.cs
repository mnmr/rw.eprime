using HarmonyLib;
using QualityJobs.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace QualityJobs.Patches
{
    /// Spec §8 handover: TryMakePreToilReservations runs only when a job truly
    /// starts (synced simulation), never during float-menu preview — the only
    /// safe place to reassign the creator in MP.
    [HarmonyPatch(typeof(JobDriver_DoBill), nameof(JobDriver_DoBill.TryMakePreToilReservations))]
    public static class Patch_CreatorHandover
    {
        public static void Postfix(JobDriver_DoBill __instance, bool __result)
        {
            if (!__result) return;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;

            Job? job = __instance.job;
            if (!(job?.bill is Bill_ProductionWithUft)) return;
            // Direct field read avoids GetTargetQueue's lazy list allocation.
            var queue = job.targetQueueB;
            if (queue == null || queue.Count != 1) return;
            if (!(queue[0].Thing is UnfinishedThing uft)) return;

            WorkItemEntry? entry = store.FindByUft(uft);
            if (entry == null || entry.state != WorkItemState.Shared) return;

            Pawn pawn = __instance.pawn;
            if (UftAuthor.Get(uft) != pawn) UftAuthor.Assign(uft, pawn);
            store.RemoveEntry(entry); // active again; re-pooled at next idle sweep
        }
    }
}
