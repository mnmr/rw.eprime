using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace WorkRoles.Patches
{
    [MainThreadPatch, HarmonyPatch]
    internal static class Patch_MiscRobotWork
    {
        private static bool Prepare() => AccessTools.TypeByName("AIRobot.X2_AIRobot") != null;
        private static MethodBase TargetMethod() => AccessTools.Method(
            AccessTools.TypeByName("AIRobot.X2_AIRobot"), "GetWorkGivers");
        private static bool Prefix(Pawn __instance, ref List<WorkGiver> __result)
        {
            if (RoleStore.Current?.IsManaged(__instance) != true) return true;
            // AIRobot's single work node consumes the entire native work list;
            // the emergency argument does not filter its work-giver contents.
            __result = CompiledJobOrders.CombinedFor(__instance);
            return false;
        }
    }

    [MainThreadPatch, HarmonyPatch]
    internal static class Patch_MiscRobotStationRegistration
    {
        private static bool Prepare() => AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation") != null;
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var type = AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation");
            yield return AccessTools.Method(type, "SpawnSetup");
            yield return AccessTools.Method(type, "ExposeData");
            yield return AccessTools.Method(type, "AddRobotToContainer");
            yield return AccessTools.Method(type, "Button_SpawnBot");
        }
        private static void Postfix(Thing __instance)
        {
            if (Scribe.mode == LoadSaveMode.Inactive || Scribe.mode == LoadSaveMode.PostLoadInit)
                MechanicalWorkers.RegisterStation(__instance);
        }
    }

    [MainThreadPatch, HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
    internal static class Patch_MiscRobotStationMoved
    {
        private static bool Prepare() => AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation") != null;
        private static void Postfix(Thing __instance) => MechanicalWorkers.NotifyStationMoved(__instance);
    }

    [MainThreadPatch, HarmonyPatch]
    internal static class Patch_MiscRobotStationDestroyed
    {
        private static bool Prepare() => AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation") != null;
        private static MethodBase TargetMethod() => AccessTools.Method(
            AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation"), "Destroy");
        private static void Prefix(Thing __instance) => MechanicalWorkers.RemoveStation(__instance);
    }
}
