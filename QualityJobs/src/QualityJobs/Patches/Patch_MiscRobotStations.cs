using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace QualityJobs.Patches
{
    [MainThreadPatch, HarmonyPatch]
    internal static class Patch_MiscRobotStationRegistration
    {
        private static bool Prepare()
            => AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation") != null;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            var type = AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation");
            yield return AccessTools.Method(type, "SpawnSetup");
            yield return AccessTools.Method(type, "ExposeData");
        }

        private static void Postfix(Thing __instance)
        {
            // Packed stations do not SpawnSetup on load. PostLoadInit sees them
            // in any holder, including inventories and travelling transporters.
            if (Scribe.mode == LoadSaveMode.Inactive || Scribe.mode == LoadSaveMode.PostLoadInit)
                WorkerCapabilities.RegisterRobotStation(__instance);
        }
    }

    [MainThreadPatch, HarmonyPatch]
    internal static class Patch_MiscRobotStationDestroyed
    {
        private static bool Prepare()
            => AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation") != null;

        private static MethodBase TargetMethod()
            => AccessTools.Method(AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation"), "Destroy");

        private static void Postfix(Thing __instance)
        {
            if (__instance.Destroyed) WorkerCapabilities.RemoveRobotStation(__instance);
        }
    }
}
