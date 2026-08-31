using HarmonyLib;
using Implanner.UI;
using Verse;
using Verse.Profile;

namespace Implanner.Patches
{
    /// Clears static presentation caches when the world is torn down (main
    /// menu / new game load) so nothing pins the old world graph or leaks
    /// stale state into another save.
    [HarmonyPatch(typeof(MemoryUtility), nameof(MemoryUtility.ClearAllMapsAndWorld))]
    public static class Patch_WorldTeardown
    {
        public static void Postfix()
        {
            ColonyScope.ReleaseSnapshot();
            Catalogs.Release();
            WrText.Reset();
            PlannerLabels.Reset();
            PlannerTips.Reset();
            GearIconMetrics.ReleaseGraphics();
            PlannerReconciler.Reset();
            Patch_PlaySettings.ResetPresentation();
            ExternalPawnFacts.Reset();
        }
    }
}
