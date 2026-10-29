using HarmonyLib;
using QualityJobs.UI;
using RimShared.UiLib;
using Verse;

namespace QualityJobs.Patches
{
    /// Explicit teardown for per-save and per-map presentation caches. These
    /// hooks run before game/map disposal while identities are still available.
    [HarmonyPatch(typeof(Game), nameof(Game.DeinitAndRemoveMap))]
    public static class Patch_GameRemoveMap
    {
        public static void Prefix(Game __instance, Map map)
        {
            WorkerCapabilities.ReleaseMap(map);
            __instance.GetComponent<QualityJobsStore>()?.ReleaseMap(map);
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.Dispose))]
    public static class Patch_GameDispose
    {
        public static void Prefix(Game __instance)
        {
            MiscRobotSupport.ResetStations(__instance);
            QualityJobsStore? store = __instance.GetComponent<QualityJobsStore>();
            if (store != null)
                store.ReleasePresentation();
            BillIds.Reset();
            UftAuthor.ResetDisplay();
            PreceptIds.Reset();
            Patch_StockGate.ResetPresentation();
            Patch_PlaySettings.ResetPresentation();
            Command_QualityJob.ResetPresentationCache();
            QualityJobsMod.ResetPresentationCaches();
            BillWindowIntegration.Reset();
            WrText.Reset();
            WrTips.Reset();
        }
    }
}
