using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace WorkRoles.Patches
{
    [HarmonyPatch(typeof(MechanitorControlGroup), nameof(MechanitorControlGroup.SetWorkMode),
        new[] { typeof(MechWorkModeDef), typeof(GlobalTargetInfo) })]
    internal static class Patch_MechanicalWorkMode
    {
        private static void Prefix(MechanitorControlGroup __instance, out MechWorkModeDef __state) => __state = __instance.WorkMode;
        private static void Postfix(MechanitorControlGroup __instance, MechWorkModeDef __state)
        {
            if (__instance.WorkMode != __state) MechanicalWorkers.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(Pawn_PlayerSettings), nameof(Pawn_PlayerSettings.AreaRestrictionInPawnCurrentMap), MethodType.Setter)]
    internal static class Patch_MechanicalArea
    {
        private static void Prefix(Pawn_PlayerSettings __instance, out Area __state) => __state = __instance.AreaRestrictionInPawnCurrentMap;
        private static void Postfix(Pawn_PlayerSettings __instance, Pawn ___pawn, Area __state)
        {
            if (MechanicalWorkers.IsMechanical(___pawn) && __instance.AreaRestrictionInPawnCurrentMap != __state)
                MechanicalWorkers.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(Pawn_DraftController), nameof(Pawn_DraftController.Drafted), MethodType.Setter)]
    internal static class Patch_MechanicalDrafted
    {
        private static void Prefix(Pawn_DraftController __instance, out bool __state) => __state = __instance.Drafted;
        private static void Postfix(Pawn_DraftController __instance, Pawn ___pawn, bool __state)
        {
            if (MechanicalWorkers.IsMechanical(___pawn) && __instance.Drafted != __state)
                MechanicalWorkers.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(Pawn_PlayerSettings), nameof(Pawn_PlayerSettings.Notify_AreaRemoved))]
    internal static class Patch_MechanicalAreaRemoved
    {
        private static void Prefix(Pawn ___pawn, out Area? __state) =>
            __state = RoleStore.Current?.mechanicalWorkersEnabled == true && MechanicalWorkers.IsMechanical(___pawn)
                ? MechanicalWorkers.ServiceArea(___pawn) : null;
        private static void Postfix(Pawn ___pawn, Area? __state)
        {
            if (__state != null && MechanicalWorkers.ServiceArea(___pawn) != __state)
                MechanicalWorkers.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(AreaManager), "NotifyEveryoneAreaRemoved")]
    internal static class Patch_MiscContainedAreaRemoved
    {
        private static void Postfix(Area area) => MiscRobotSupport.NotifyAreaRemoved(area);
    }

    [HarmonyPatch]
    internal static class Patch_MechanicalMentalState
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState));
            yield return AccessTools.Method(typeof(MentalStateHandler), "ClearMentalStateDirect");
        }
        private static void Prefix(Pawn ___pawn, out bool __state) => __state = ___pawn.InMentalState;
        private static void Postfix(Pawn ___pawn, bool __state)
        {
            if (RoleStore.Current?.mechanicalWorkersEnabled == true && MechanicalWorkers.IsMechanical(___pawn)
                && __state != ___pawn.InMentalState) MechanicalWorkers.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.DeinitAndRemoveMap))]
    internal static class Patch_MechanicalMapRemoved
    {
        private static void Prefix(Map map) => MechanicalWorkers.ReleaseMap(map);
    }

    [MainThreadPatch, HarmonyPatch]
    internal static class Patch_MiscRobotActivation
    {
        private static bool Prepare() => AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation") != null;
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var type = AccessTools.TypeByName("AIRobot.X2_Building_AIRobotRechargeStation");
            yield return AccessTools.Method(type, "Notify_CallBotForShutdown");
            yield return AccessTools.Method(type, "Notify_CallBotForRecharge");
            yield return AccessTools.Method(type, "Notify_SpawnBot");
        }
        private static void Prefix(Thing __instance, out bool __state) => __state = MiscRobotSupport.StationAutomaticWorkEnabled(__instance);
        private static void Postfix(Thing __instance, bool __state)
        {
            if (__state != MiscRobotSupport.StationAutomaticWorkEnabled(__instance)) MechanicalWorkers.NotifyChanged();
        }
    }

    [HarmonyPatch]
    internal static class Patch_QualityJobsMechanicalPreparation
    {
        private static bool Prepare() => AccessTools.Method(AccessTools.TypeByName("QualityJobs.Commands"), "SetAllowRobotPreparation") != null;
        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName("QualityJobs.Commands"), "SetAllowRobotPreparation");
        private static void Prefix(out bool __state) => __state = MechanicalWorkers.QualityPreparationAllowed;
        private static void Postfix(bool __state)
        {
            if (__state != MechanicalWorkers.QualityPreparationAllowed) MechanicalWorkers.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(MechanitorControlGroup), nameof(MechanitorControlGroup.Assign))]
    internal static class Patch_MechanicalGroupAssigned
    {
        private static void Prefix(Pawn pawn, out MechanitorControlGroup __state) => __state = pawn.GetMechControlGroup();
        private static void Postfix(Pawn pawn, MechanitorControlGroup __state)
        {
            if (__state != pawn.GetMechControlGroup()) MechanicalWorkers.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(CompOverseerSubject), nameof(CompOverseerSubject.Notify_DisconnectedFromOverseer))]
    internal static class Patch_MechanicalDisconnected
    {
        private static void Postfix()
        {
            if (RoleStore.Current?.mechanicalWorkersEnabled == true) MechanicalWorkers.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(Pawn_MechanitorTracker), nameof(Pawn_MechanitorTracker.Notify_BandwidthChanged))]
    internal static class Patch_MechanicalBandwidth
    {
        private static void Prefix(Pawn_MechanitorTracker __instance, out Pawn[]? __state) =>
            __state = RoleStore.Current?.mechanicalWorkersEnabled == true
                ? __instance.ControlledPawns.ToArray() : null;
        private static void Postfix(Pawn_MechanitorTracker __instance, Pawn[]? __state)
        {
            if (__state == null) return;
            List<Pawn> current = __instance.ControlledPawns;
            bool changed = current.Count != __state.Length;
            for (int i = 0; i < __state.Length && !changed; i++) changed = !current.Contains(__state[i]);
            if (changed) MechanicalWorkers.NotifyChanged();
        }
    }
}
