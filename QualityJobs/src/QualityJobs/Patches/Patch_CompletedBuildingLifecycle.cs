using HarmonyLib;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    [HarmonyPatch(typeof(Thing), nameof(Thing.SetFaction))]
    internal static class Patch_CompletedBuildingFaction
    {
        private static void Postfix(Thing __instance) =>
            QualityJobsStore.Active?.NotifyCompletedBuildingChanged(__instance);
    }

    [HarmonyPatch(typeof(DesignationManager), nameof(DesignationManager.RemoveDesignation))]
    internal static class Patch_CompletedBuildingCancel
    {
        private static void Postfix(Designation des)
        {
            if (des.def == DesignationDefOf.Deconstruct && des.target.HasThing)
                QualityJobsStore.Active?.NotifyDeconstructionCancelled(des.target.Thing);
        }
    }
}
