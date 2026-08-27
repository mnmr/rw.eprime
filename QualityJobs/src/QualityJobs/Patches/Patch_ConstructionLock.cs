using HarmonyLib;
using RimWorld;
using QualityJobs.Core;
using Verse;
using Verse.AI;

namespace QualityJobs.Patches
{
    /// Construction lock (spec §10): read-only postfix — also runs during
    /// client-local float-menu scans, so it must not mutate anything. Only
    /// FinishFrame jobs are suppressed; blocking-thing haul jobs returned by
    /// the same method must pass through.
    [HarmonyPatch(typeof(WorkGiver_ConstructFinishFrames),
        nameof(WorkGiver_ConstructFinishFrames.JobOnThing))]
    public static class Patch_ConstructionLock
    {
        public static void Postfix(Pawn pawn, Thing t, ref Job? __result)
        {
            if (__result == null || __result.def != JobDefOf.FinishFrame) return;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || store.plans.Count == 0) return;
            ConstructionPlan? plan = store.FindPlan(t);
            if (plan == null) return;

            if (plan.state == ConstructionPlanState.Paused)
                __result = null;
            else if (plan.state == ConstructionPlanState.Dispatched && plan.finisher != pawn)
                __result = null;
        }
    }
}
