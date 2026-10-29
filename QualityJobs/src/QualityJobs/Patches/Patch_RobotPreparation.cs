using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace QualityJobs.Patches
{
    [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
    public static class Patch_RobotPreparation_ItemDestroyed
    {
        public static void Postfix(Thing __instance)
        {
            if (!__instance.Destroyed) return;
            if (__instance is UnfinishedThing uft)
                QualityJobsStore.Active?.ForgetPreparationSource(uft);
            else if (__instance is IBillGiver)
                QualityJobsStore.Active?.PrunePreparationSources();
        }
    }

    [HarmonyPatch]
    public static class Patch_RobotPreparation_BillDeleted
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(BillStack), nameof(BillStack.Delete));
            yield return AccessTools.Method(typeof(BillStack), nameof(BillStack.Clear));
            yield return AccessTools.Method(typeof(BillStack), nameof(BillStack.RemoveIncompletableBills));
        }

        public static void Prefix(BillStack __instance, out int __state)
            => __state = __instance.Count;

        public static void Postfix(BillStack __instance, int __state)
        {
            // RemoveIncompletableBills is called for every ordinary bench scan.
            // The unchanged hot path must not traverse any provenance.
            if (__instance.Count < __state)
                QualityJobsStore.Active?.PrunePreparationSources();
        }
    }

    // Vanilla checks this before both new crafting and bound/shared resume.
    [HarmonyPatch(typeof(Bill), nameof(Bill.PawnAllowedToStartAnew))]
    public static class Patch_RobotPreparation_BillAdmission
    {
        public static void Postfix(Bill __instance, Pawn p, ref bool __result)
        {
            if (!__result) return;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store != null && !RobotPreparation.AllowsBillWork(store, p, __instance))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(JobDriver_DoBill),
        nameof(JobDriver_DoBill.TryMakePreToilReservations))]
    public static class Patch_RobotPreparation_BillStart
    {
        // Before the creator-handover postfix removes the shared entry, so its
        // original managed source still participates in the admission decision.
        public static bool Prefix(JobDriver_DoBill __instance, ref bool __result)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || RobotPreparation.AllowsJob(store,
                    __instance.pawn, __instance.job))
                return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(JobDriver_ConstructFinishFrame),
        nameof(JobDriver_ConstructFinishFrame.TryMakePreToilReservations))]
    public static class Patch_RobotPreparation_FrameStart
    {
        public static bool Prefix(JobDriver_ConstructFinishFrame __instance,
            ref bool __result)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || RobotPreparation.AllowsJob(store,
                    __instance.pawn, __instance.job))
                return true;
            __result = false;
            return false;
        }
    }
}
