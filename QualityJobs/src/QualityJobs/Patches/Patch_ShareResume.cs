using System.Collections.Generic;
using HarmonyLib;
using QualityJobs.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace QualityJobs.Patches
{
    /// Shared helper: builds the DoBill job for a UFT that the pawn did not
    /// create (shared or dispatched). Mirrors WorkGiver_DoBill.FinishUftJob
    /// (Decompiled\RimWorld\WorkGiver_DoBill.cs:168-186) minus the creator
    /// check. Used by Patch_ShareResume_FinishJob and WorkGiver_FinishQualityWork
    /// so the logic is never duplicated.
    internal static class FinishUftJobHelper
    {
        /// Builds the same DoBill job as vanilla's FinishUftJob without
        /// requiring the pawn to be the UFT creator.
        /// Re-verify the body against the decompile on game updates.
        internal static Job BuildFinishUftJob(Pawn pawn, UnfinishedThing uft,
            Bill_ProductionWithUft bill)
        {
            Job? haulOffJob = WorkGiverUtility.HaulStuffOffBillGiverJob(
                pawn, bill.billStack.billGiver, uft);
            if (haulOffJob != null && haulOffJob.targetA.Thing != uft)
                return haulOffJob;
            Job job = JobMaker.MakeJob(JobDefOf.DoBill, (Thing)bill.billStack.billGiver);
            job.bill         = bill;
            job.targetQueueB = new List<LocalTargetInfo> { uft };
            job.countQueue   = new List<int> { 1 };
            job.haulMode     = HaulMode.ToCellNonStorage;
            return job;
        }
    }

    /// Spec §8 resume matching. Read-only: also runs during client-local
    /// float-menu generation in MP, so it must not mutate anything.
    [HarmonyPatch(typeof(WorkGiver_DoBill), "ClosestUnfinishedThingForBill")]
    public static class Patch_ShareResume_Match
    {
        public static void Postfix(Pawn pawn, Bill_ProductionWithUft bill,
            ref UnfinishedThing __result)
        {
            if (__result != null) return;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || !store.shareUnfinishedWork) return;
            if (store.entries.Count == 0) return;

            // Compute billKey lazily: only once the first entry passes the
            // cheap state/spawned/map/recipe guards, avoiding the precept
            // id lookup entirely when no Shared entries exist for this recipe.
            StyleKey billKey = default;
            bool billKeyComputed = false;
            UnfinishedThing? best = null;
            float bestDist = float.MaxValue;
            List<WorkItemEntry> entries = store.entries;
            for (int i = 0; i < entries.Count; i++)
            {
                WorkItemEntry e = entries[i];
                if (e.state != WorkItemState.Shared) continue;
                UnfinishedThing? t = e.uft;
                if (t == null || !t.Spawned || t.Map != pawn.Map) continue;
                if (t.Recipe != bill.recipe) continue;
                if (!billKeyComputed)
                {
                    billKey = StyleSnapshot.KeyOf(bill);
                    billKeyComputed = true;
                }
                StyleKey entryKey = e.snapshot?.ToStyleKey() ?? StyleKey.Unknown;
                if (!ShareMatch.StyleCompatible(entryKey, billKey)) continue;
                if (t.IsForbidden(pawn) || !pawn.CanReserve(t)) continue;
                if (!IngredientsAllowed(t, bill)) continue;
                if (!pawn.CanReach(t, PathEndMode.Touch, pawn.NormalMaxDanger())) continue;
                float d = (t.Position - pawn.Position).LengthHorizontalSquared;
                // Intentionally simpler than vanilla's GenClosest (straight-line
                // squared distance + thingIDNumber tie-break); deterministic and
                // iteration-order-independent — do not "fix" toward GenClosest.
                if (d < bestDist || (d == bestDist && best != null
                    && t.thingIDNumber < best.thingIDNumber))
                {
                    best = t;
                    bestDist = d;
                }
            }
            if (best != null) __result = best;
        }

        private static bool IngredientsAllowed(UnfinishedThing t, Bill bill)
        {
            for (int i = 0; i < t.ingredients.Count; i++)
                if (!bill.IsFixedOrAllowedIngredient(t.ingredients[i].def)) return false;
            return true;
        }
    }

    /// FinishUftJob hard-errors when Creator != pawn (WorkGiver_DoBill.cs:170-174).
    /// For shared UFTs, build the same short job ourselves. No mutation here
    /// (float-menu safe); the creator handover happens at real job start.
    [HarmonyPatch(typeof(WorkGiver_DoBill), "FinishUftJob")]
    public static class Patch_ShareResume_FinishJob
    {
        public static bool Prefix(Pawn pawn, UnfinishedThing uft,
            Bill_ProductionWithUft bill, ref Job __result)
        {
            if (uft.Creator == pawn) return true;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || !store.IsShared(uft)) return true;

            // Delegate to shared helper (mirrors WorkGiver_DoBill.FinishUftJob
            // without the creator check; re-verify on game updates).
            __result = FinishUftJobHelper.BuildFinishUftJob(pawn, uft, bill);
            return false;
        }
    }
}
