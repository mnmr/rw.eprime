using HarmonyLib;
using QualityJobs.Core;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Bill target quality: when a finished product rolls below the bill's
    /// target, mark the executing iteration for retry without changing a bill
    /// counter. Quality is rolled inside vanilla's GenRecipe.PostProcessProduct,
    /// so a postfix there sees the final value.
    ///
    /// Scope: managed recipes (ManagedRecipes) with bills in RepeatCount mode
    /// only. TargetCount ("do until you have X") already filters counted items
    /// by quality range in vanilla, and Forever never stops.
    ///
    /// A retry does not remove the below-target product: it stays spawned
    /// like any finished product, and the bill keeps its repeat count, so the
    /// bill stays alive and makes the item again.
    ///
    /// One-shot finish bills resolve target-quality configuration from their
    /// SOURCE bill, but mark the executing temporary bill so the completion
    /// patch can consume the signal for the exact iteration.
    ///
    /// MP determinism: runs inside product creation (synced sim), reads only
    /// synced store state, and records the same transient signal on every client.
    [HarmonyPatch(typeof(GenRecipe), "PostProcessProduct")]
    public static class Patch_BillTargetQuality
    {
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
            // Only managed recipes retry: an unmanaged recipe has no Quality
            // Jobs bill section, and an ingredient-quality recipe (VGE
            // genoframes) would only burn its ingredient on every remake.
            if (!ManagedRecipes.IsManagedRecipe(targetBill.recipe)) return;

            int target = store.TargetQualityFor(targetBill);
            if (target <= 0) return;

            CompQuality? comp = __result.TryGetComp<CompQuality>();
            if (comp == null) return;
            // Unmanaged bills complete vanilla-style: a stored target quality
            // (per bill or the per-save default) must not keep remaking items.
            if (!RetryDecision.ShouldRetry(store.ConfigFor(targetBill).Managed,
                    (QualityLevel)(int)comp.Quality, (QualityLevel)target)) return;

            // Multiple products from one iteration mark the same (bill, tick)
            // pair idempotently. Completion consumes it once.
            store.MarkBillRetry(bill);
        }
    }
}
