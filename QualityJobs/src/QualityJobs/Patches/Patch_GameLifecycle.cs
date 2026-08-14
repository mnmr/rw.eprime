using HarmonyLib;
using QualityJobs.UI;
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
            __instance.GetComponent<QualityJobsStore>()?.ReleaseMap(map);
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.Dispose))]
    public static class Patch_GameDispose
    {
        public static void Prefix(Game __instance)
        {
            QualityJobsStore? store = __instance.GetComponent<QualityJobsStore>();
            if (store != null)
            {
                store.ReleasePresentation();
                QualityJobsApi.ReleaseMemoOwner(store);
            }
            BillIds.Reset();
            PreceptIds.Reset();
            Patch_StockGate.ResetPresentation();
            Patch_PlaySettings.ResetPresentation();
            Command_QualityJob.ResetPresentationCache();
            QualityJobsMod.ResetPresentationCaches();
            WrText.Reset();
            WrTips.Reset();
        }
    }
}
