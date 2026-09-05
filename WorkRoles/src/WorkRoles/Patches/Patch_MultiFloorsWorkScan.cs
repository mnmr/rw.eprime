using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimShared.Common;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.Patches
{
    /// MultiFloors' prioritized cross-level work scanner (its default mode)
    /// transpiles JobGiver_Work.TryIssueJobPackage to call its own static list
    /// provider instead of the Pawn_WorkSettings.WorkGiversInOrderNormal getter
    /// this mod patches. That provider keeps two giver lanes per pawn (work
    /// types at or below its priority threshold, then the rest) and rebuilds
    /// them at work-type granularity from GetPriority whenever its dirty flag
    /// is set. For managed pawns that erased role giver exclusions (a role
    /// holding only some hauling jobs ran ALL hauling jobs) and broke activity
    /// attribution. The rebuild is the only step that reads priorities, so it
    /// is the only step replaced: for managed pawns the lanes are filled from
    /// the compiled role order, split by MultiFloors' own threshold rule on
    /// the vanilla priority projection. The provider's gates, level-settings
    /// filter, lane choice and cross-level scan passes stay MultiFloors code,
    /// so managed and unmanaged pawns run the same scanner. A pass-through
    /// prefix on the provider sets the dirty flag when the compiled order
    /// identity moved; threshold changes and leaving management already mark
    /// it dirty through MultiFloors' own postfixes. Unmanaged pawns are never
    /// touched; emergency work still flows through the patched
    /// WorkGiversInOrderEmergency getter. Resolution is fail-closed: if any
    /// part of the MultiFloors API changed, the resolved members are dropped,
    /// both prefixes stand down, and one warning names the degradation
    /// (verified against the 1.6.1.3 assembly, temp/mf-decomp).
    internal static class Patch_MultiFloorsWorkScan
    {
        private delegate ref List<WorkGiver> GiverLaneRef(Pawn_WorkSettings workSettings);
        private delegate ref bool OrderDirtyRef(Pawn_WorkSettings workSettings);

        private static readonly AccessTools.FieldRef<Pawn_WorkSettings, Pawn> PawnOf =
            AccessTools.FieldRefAccess<Pawn_WorkSettings, Pawn>("pawn");

        private static GiverLaneRef? highLane;
        private static GiverLaneRef? lowLane;
        private static OrderDirtyRef? orderDirty;
        private static FieldInfo? settingsField;                    // MultiFloorsModHandler.Settings
        private static AccessTools.FieldRef<object, int>? thresholdOf; // MultiFloorsModSettings.WorkGiverHighPriorityThreshold

        /// Lane source stamps —
        /// Owner: process static, entries per managed pawn whose lanes were
        ///   last rebuilt from a compiled order.
        /// Key: pawn identity.
        /// Value: the CompiledJobOrders.NormalFor list reference the lanes were
        ///   last built from (the lanes themselves are owned by MultiFloors).
        /// Dependencies: compiled job order identity for the pawn only —
        ///   already governed by CompiledJobOrders' invalidation matrix, so no
        ///   independent invalidation inputs exist.
        /// Refresh policy: immediate — compared on every provider call; a moved
        ///   identity only marks MultiFloors' dirty flag, and the lanes are
        ///   rebuilt when MultiFloors next asks for them.
        /// Equality policy: reference identity; an unchanged compiled list
        ///   leaves the dirty flag and the lanes alone.
        /// Teardown: entries removed on pawn destroy and when the pawn leaves
        ///   management, cleared on world teardown.
        private static readonly Dictionary<Pawn, List<WorkGiver>> laneSources =
            new Dictionary<Pawn, List<WorkGiver>>(ReferenceIdentityComparer<Pawn>.Instance);

        /// Rebuild scratch: vanilla priorities parallel to the compiled order.
        /// Main-thread only, like every Harmony patch here.
        private static readonly List<int> scratchPriorities = new List<int>();

        internal static void NotifyDestroyed(Pawn pawn) => laneSources.Remove(pawn);

        internal static void NotifyUnmanaged(Pawn pawn) => laneSources.Remove(pawn);

        internal static void ReleaseForTeardown() => laneSources.Clear();

        internal static void Install(Harmony harmony)
        {
            var scanner = GenTypes.GetTypeInAnyAssembly(
                "MultiFloors.HarmonyPatches.HarmonyPatch_ScanJobsOnOtherLevelPrioritized");
            if (scanner == null) return; // MultiFloors not installed.

            try
            {
                var provider = scanner.GetMethod("GetWorkGiversInOrderNormal",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(Pawn_WorkSettings) }, null);
                var rebuild = scanner.GetMethod("CacheWorkGiversInOrder",
                    BindingFlags.NonPublic | BindingFlags.Static, null,
                    new[] { typeof(Pawn_WorkSettings) }, null);
                var fields = GenTypes.GetTypeInAnyAssembly("MultiFloors.PrepatcherFields");
                var high = LaneAccessor(fields, "HighPriorityWorkGiversInOrderNormal");
                var low = LaneAccessor(fields, "LowPriorityWorkGiversInOrderNormal");
                var dirtyMethod = fields?.GetMethod("WorkGiversOrderDirty",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(Pawn_WorkSettings) }, null);
                var handler = GenTypes.GetTypeInAnyAssembly("MultiFloors.MultiFloorsModHandler");
                var settings = handler?.GetField("Settings",
                    BindingFlags.Public | BindingFlags.Static);
                var threshold = settings?.FieldType.GetField("WorkGiverHighPriorityThreshold",
                    BindingFlags.Public | BindingFlags.Instance);
                if (provider == null || provider.ReturnType != typeof(List<WorkGiver>)
                    || rebuild == null || rebuild.ReturnType != typeof(void)
                    || high == null || low == null
                    || dirtyMethod == null
                    || dirtyMethod.ReturnType != typeof(bool).MakeByRefType()
                    || settings == null || settings.FieldType.IsValueType
                    || threshold == null || threshold.FieldType != typeof(int))
                    throw new MissingMemberException(
                        "MultiFloors prioritized work scanner members not found");

                orderDirty = (OrderDirtyRef)dirtyMethod.CreateDelegate(typeof(OrderDirtyRef));
                highLane = high;
                lowLane = low;
                settingsField = settings;
                thresholdOf = AccessTools.FieldRefAccess<int>(
                    settings.FieldType, "WorkGiverHighPriorityThreshold");
                harmony.Patch(provider, prefix: new HarmonyMethod(
                    typeof(Patch_MultiFloorsWorkScan), nameof(MarkDirtyPrefix)));
                harmony.Patch(rebuild, prefix: new HarmonyMethod(
                    typeof(Patch_MultiFloorsWorkScan), nameof(RebuildLanesPrefix)));
                Log.Message("[WorkRoles] MultiFloors detected; role job orders feed "
                    + "its prioritized work scanner for managed pawns.");
            }
            catch (Exception exception)
            {
                highLane = null;
                lowLane = null;
                orderDirty = null;
                settingsField = null;
                thresholdOf = null;
                Log.Warning("[WorkRoles] MultiFloors detected but its prioritized "
                    + "work scanner API changed; role job lists may not apply on "
                    + "multi-level maps: " + exception.Message);
            }
        }

        private static GiverLaneRef? LaneAccessor(Type? fields, string name)
        {
            var method = fields?.GetMethod(name,
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Pawn_WorkSettings) }, null);
            return method == null
                || method.ReturnType != typeof(List<WorkGiver>).MakeByRefType()
                ? null
                : (GiverLaneRef)method.CreateDelegate(typeof(GiverLaneRef));
        }

        /// Pass-through on MultiFloors' list provider: a managed pawn whose
        /// compiled order identity moved since its lanes were built needs a
        /// rebuild, which MultiFloors performs itself once its flag is dirty.
        public static void MarkDirtyPrefix(Pawn_WorkSettings workSettings)
        {
            var dirty = orderDirty;
            if (dirty == null) return; // Stood down after a failed install.
            var pawn = PawnOf(workSettings);
            if (pawn == null || RoleStore.Current?.IsManaged(pawn) != true) return;
            if (!laneSources.TryGetValue(pawn, out var source)
                || !ReferenceEquals(source, CompiledJobOrders.NormalFor(pawn)))
                dirty(workSettings) = true;
        }

        /// Replaces MultiFloors' lane rebuild for managed pawns only. The lanes
        /// are MultiFloors-owned lists (its own rebuild Clears them), so the
        /// cache-owned compiled order is copied in, never handed over. The
        /// threshold is MultiFloors' client-local mod setting, exactly as in
        /// its own rebuild; the split reads the vanilla projection, which is
        /// deterministic per compiled order.
        public static bool RebuildLanesPrefix(Pawn_WorkSettings workSettings)
        {
            var dirty = orderDirty;
            if (dirty == null) return true; // Stood down after a failed install.
            var pawn = PawnOf(workSettings);
            if (pawn == null || RoleStore.Current?.IsManaged(pawn) != true) return true;
            var settings = settingsField!.GetValue(null);
            if (settings == null) return true; // MultiFloors' own rebuild handles this state.

            var compiled = CompiledJobOrders.NormalFor(pawn);
            ref List<WorkGiver> high = ref highLane!(workSettings);
            ref List<WorkGiver> low = ref lowLane!(workSettings);
            if (high == null) high = new List<WorkGiver>(compiled.Count);
            if (low == null) low = new List<WorkGiver>();

            scratchPriorities.Clear();
            for (int i = 0; i < compiled.Count; i++)
                scratchPriorities.Add(
                    CompiledJobOrders.VanillaPriorityFor(pawn, compiled[i].def.workType));
            MultiFloorsLanes.Split(compiled, scratchPriorities,
                thresholdOf!(settings), high, low);

            laneSources[pawn] = compiled;
            dirty(workSettings) = false;
            return false;
        }
    }
}
