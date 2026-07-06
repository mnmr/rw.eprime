using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Profile;

namespace WorkRoles.Patches
{
    [HarmonyPatch(typeof(Pawn_WorkSettings))]
    public static class Patch_PawnWorkSettings
    {
        private static bool IsManaged(Pawn pawn) =>
            RoleStore.Current?.IsManaged(pawn) == true;

        [HarmonyPrefix]
        [HarmonyPatch("WorkGiversInOrderNormal", MethodType.Getter)]
        public static bool NormalPrefix(Pawn ___pawn, ref List<WorkGiver> __result)
        {
            if (!IsManaged(___pawn)) return true;
            __result = CompiledJobOrders.NormalFor(___pawn);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch("WorkGiversInOrderEmergency", MethodType.Getter)]
        public static bool EmergencyPrefix(Pawn ___pawn, ref List<WorkGiver> __result)
        {
            if (!IsManaged(___pawn)) return true;
            __result = CompiledJobOrders.EmergencyFor(___pawn);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Pawn_WorkSettings.GetPriority))]
        public static bool GetPriorityPrefix(Pawn ___pawn, WorkTypeDef w, ref int __result)
        {
            if (!IsManaged(___pawn)) return true;
            __result = CompiledJobOrders.PriorityFor(___pawn, w);
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Pawn_WorkSettings.SetPriority))]
        public static bool SetPriorityPrefix(Pawn ___pawn)
        {
            // Managed pawns: the role store is the single source of truth (spec §6).
            return !IsManaged(___pawn);
        }
    }

    /// Capability changes (health/traits/genes/age) alter what a pawn can do -> recompile.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Notify_DisabledWorkTypesChanged))]
    public static class Patch_Pawn_NotifyDisabledWorkTypesChanged
    {
        public static void Postfix(Pawn __instance) => CompiledJobOrders.Invalidate(__instance);
    }

    /// Evict destroyed pawns so the static cache and store don't pin them (review issue).
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Destroy))]
    public static class Patch_Pawn_Destroy
    {
        public static void Postfix(Pawn __instance)
        {
            CompiledJobOrders.Invalidate(__instance);
            RoleStore.Current?.pawnSets.Remove(__instance);
        }
    }

    /// Clear the static cache when the world is torn down (main menu / new game load).
    [HarmonyPatch(typeof(MemoryUtility), nameof(MemoryUtility.ClearAllMapsAndWorld))]
    public static class Patch_MemoryUtility_ClearAllMapsAndWorld
    {
        public static void Postfix() => CompiledJobOrders.InvalidateAll();
    }
}
