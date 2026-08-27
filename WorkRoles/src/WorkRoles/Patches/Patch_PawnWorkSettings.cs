using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Profile;

namespace WorkRoles.Patches
{
    public readonly struct PriorityGridPriorityTransitionState
    {
        internal PriorityGridPriorityTransitionState(Pawn pawn, WorkTypeDef workType)
        {
            Pawn = workType != null && PriorityGridFacts.IsRelevant(pawn)
                ? pawn : null;
            WorkType = workType;
            Priority = Pawn?.workSettings?.GetPriority(workType) ?? 0;
            Revision = Pawn == null
                ? 0 : PriorityGridFacts.Revisions.RevisionOf(Pawn);
        }

        internal Pawn? Pawn { get; }
        internal WorkTypeDef? WorkType { get; }
        internal int Priority { get; }
        internal int Revision { get; }

        internal void InvalidateIfChanged()
        {
            if (Pawn == null || WorkType == null
                || (Pawn.workSettings?.GetPriority(WorkType) ?? 0) == Priority
                || PriorityGridFacts.Revisions.RevisionOf(Pawn) != Revision)
                return;
            PriorityGridFacts.Invalidate(Pawn);
        }
    }

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
            // Hottest patched path (JobGiver_Work): the compiled cache hit also
            // proves managed ownership, followed by one flat-array read.
            var store = RoleStore.Current;
            if (store == null || !CompiledJobOrders.TryPriorityForManaged(
                    ___pawn, w, store.reportVanillaPriorities, out __result))
                return true;
            // Raw ranks (1..N) by default; optionally vanilla 0-4 for readers
            // like Numbers that expect that range (Options tab toggle).
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(nameof(Pawn_WorkSettings.SetPriority))]
        public static bool SetPriorityPrefix(Pawn ___pawn, WorkTypeDef w, int priority,
            out PriorityGridPriorityTransitionState __state)
        {
            // Managed pawns: the role store is the single source of truth (spec §6).
            if (!IsManaged(___pawn))
            {
                __state = new PriorityGridPriorityTransitionState(___pawn, w);
                return true;
            }
            __state = default;
            // The swallowed write came from another mod — tell the player once.
            PrioritySetWatcher.OnBlockedSetPriority(___pawn, w, priority);
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(nameof(Pawn_WorkSettings.SetPriority))]
        public static void SetPriorityPostfix(
            PriorityGridPriorityTransitionState __state)
            => __state.InvalidateIfChanged();

        [HarmonyPrefix]
        [HarmonyPatch(nameof(Pawn_WorkSettings.DisableAll))]
        public static bool DisableAllPrefix(Pawn ___pawn)
        {
            // Vanilla's only call is pre-game pawn prep, so a managed pawn here
            // means another mod — same single-source policy as SetPriority
            // (null work type = "all work" in the watcher dialog).
            if (!IsManaged(___pawn)) return true;
            PrioritySetWatcher.OnBlockedSetPriority(___pawn, null!, 0); // null = "all work" in the watcher dialog
            return false;
        }
    }

    /// Capability changes (health/traits/genes/age) alter what a pawn can do -> recompile.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Notify_DisabledWorkTypesChanged))]
    public static class Patch_Pawn_NotifyDisabledWorkTypesChanged
    {
        public static void Postfix(Pawn __instance)
        {
            if (ExternalPawnFacts.IsRelevant(__instance))
                ExternalPawnFacts.Invalidate(__instance);
            PriorityGridFacts.Invalidate(__instance);
            CompiledJobOrders.Invalidate(__instance);
        }
    }

    /// Location rules depend on which canonical map (if any) holds the pawn.
    /// The tracker recompiles only when that actually changed, so hops between
    /// floors of one stack stay free.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    public static class Patch_Pawn_SpawnSetup
    {
        public static void Postfix(Pawn __instance) => PawnLocationTracker.NotifySpawned(__instance);
    }

    /// A despawn's destination is unknown here (floor transfers respawn within
    /// the same call stack); the tracker resolves it next tick. Pawn.ExitMap
    /// normally funnels through this, gravship takeoff calls DeSpawn directly.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DeSpawn))]
    public static class Patch_Pawn_DeSpawn
    {
        public static void Postfix(Pawn __instance) => PawnLocationTracker.NotifyDespawned(__instance);
    }

    /// Evict destroyed pawns from runtime orders and the persistent role store.
    /// The window-owned external generation is released or rebuilt as a whole.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Destroy))]
    public static class Patch_Pawn_Destroy
    {
        public static void Prefix(Pawn __instance, out bool __state) =>
            __state = ExternalPawnFacts.IsRelevant(__instance);

        public static void Postfix(Pawn __instance, bool __state)
        {
            CompiledJobOrders.Invalidate(__instance);
            PawnLocationTracker.NotifyDestroyed(__instance);
            JobRankBaseline.NotifyDestroyed(__instance);
            RoleStore.Current?.pawnSets.Remove(__instance);
            if (__state) ExternalPawnFacts.Release(__instance);
            PriorityGridFacts.Release(__instance);
        }
    }

    /// Clear the static caches when the world is torn down (main menu / new
    /// game load): compiled orders, the store reference (would otherwise pin
    /// the old world graph) and the session-grown tooltip registry.
    [HarmonyPatch(typeof(MemoryUtility), nameof(MemoryUtility.ClearAllMapsAndWorld))]
    public static class Patch_MemoryUtility_ClearAllMapsAndWorld
    {
        public static void Postfix()
        {
            PrioritySetWatcher.ReleaseForTeardown();
            WorkRolesGameComponent.ReleaseForTeardown();
            ActivityTracker.ReleaseForTeardown();
            BillRoleTransfer.ReleaseForTeardown();
            UI.RoleDrag.Cancel();
            UI.KeyOverride.Restore();
            UI.WindowDataLifecycle.ReleaseShared();
            UI.WorkRolesTex.ReleaseForTeardown();
            Patch_DialogBillConfig_DoWindowContents.ReleaseForTeardown();
            DefinitionReloadCoordinator.CancelPendingWarm();
            DefinitionReloadCoordinator.ReleaseForTeardown();
            UI.RoleClipboard.Clear();
            CompiledJobOrders.ReleaseForTeardown();
            PriorityGridFacts.ReleaseForTeardown();
            JobRankBaseline.ReleaseForTeardown();
            FloorMaps.ReleaseForTeardown();
            PawnLocationTracker.ReleaseForTeardown();
            RoleStore.ClearCached();
            UI.StructuredTipPresenter.Reset();
        }
    }
}
