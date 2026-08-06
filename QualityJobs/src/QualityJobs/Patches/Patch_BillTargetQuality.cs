using HarmonyLib;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Bill target quality: when a finished product rolls below the bill's
    /// target, the bill count is raised by one so a replacement is produced
    /// (the sub-target item is kept). Quality is rolled inside vanilla's
    /// GenRecipe.PostProcessProduct, so a postfix there sees the final value.
    ///
    /// Scope: bills in RepeatCount mode only. TargetCount ("do until you have
    /// X") already filters counted items by quality range in vanilla, and
    /// Forever never stops; both would drift if we raised their numbers.
    ///
    /// One-shot finish bills route the increment to their SOURCE bill via the
    /// store entry: raising the one-shot's own count would make the finisher
    /// craft a brand-new item from scratch off a single-iteration bill.
    ///
    /// MP determinism: runs inside product creation (synced sim), reads only
    /// synced store state, and mutates the bill identically on every client.
    [HarmonyPatch(typeof(GenRecipe), "PostProcessProduct")]
    public static class Patch_BillTargetQuality
    {
        // Recipes can yield several products per iteration; the count must
        // rise once per completed iteration, not once per product. The guard
        // keys on (bill id, tick): same sim values on every client, and a
        // string+int pair cannot root game objects across world unloads.
        private static string? lastBumpBillId;
        private static int lastBumpTick = -1;

        public static void Postfix(Thing __result, Pawn worker)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;

            if (!(worker.jobs?.curJob?.bill is Bill_Production bill)) return;

            // Resolve one-shot finish bills to their source bill; a dangling
            // source (deleted while dispatched) skips the feature.
            Bill_Production? targetBill = bill;
            if (store.IsFinishBill(bill))
            {
                targetBill = null;
                for (int i = 0; i < store.entries.Count; i++)
                    if (store.entries[i].finishBill == bill)
                    {
                        var source = store.entries[i].sourceBill;
                        if (source != null && !source.DeletedOrDereferenced)
                            targetBill = source;
                        break;
                    }
                if (targetBill == null) return;
            }

            if (targetBill.repeatMode != BillRepeatModeDefOf.RepeatCount) return;

            int target = store.TargetQualityFor(targetBill);
            if (target <= 0) return;

            CompQuality? comp = __result.TryGetComp<CompQuality>();
            if (comp == null) return;
            if (!RetryDecision.ShouldRetry((QualityLevel)(int)comp.Quality,
                    (QualityLevel)target)) return;

            string billId = BillIds.IdOf(targetBill);
            int tick = Find.TickManager.TicksGame;
            if (tick == lastBumpTick && billId == lastBumpBillId) return;
            lastBumpBillId = billId;
            lastBumpTick = tick;

            // Vanilla's Notify_IterationCompleted decrements repeatCount after
            // products are made; raising it here nets out to "this item did
            // not count", so the bill produces one more.
            targetBill.repeatCount++;
        }
    }
}
