using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimShared.Common;
using RimWorld;
using Verse;

namespace WorkRoles.Patches
{
    /// MultiFloors' prioritized cross-level work scanner (its default mode)
    /// transpiles JobGiver_Work.TryIssueJobPackage to call its own static list
    /// provider instead of the Pawn_WorkSettings.WorkGiversInOrderNormal getter
    /// this mod patches, and that provider rebuilds giver lists at work-type
    /// granularity from GetPriority. For managed pawns that erased role giver
    /// exclusions (a role holding only some hauling jobs ran ALL hauling jobs)
    /// and broke activity attribution (jobs from unclaimed givers light no
    /// role). This prefix feeds MultiFloors the compiled role order instead:
    /// its high-priority lane carries the full compiled normal order and its
    /// low lane stays empty, so MultiFloors' cross-level scan gates observe
    /// the correct emptiness and its second (low-priority) scan pass is a
    /// cheap no-op. MultiFloors' per-level work settings still apply through
    /// its own level filter. Unmanaged pawns keep untouched MultiFloors
    /// behavior; emergency work still flows through the patched
    /// WorkGiversInOrderEmergency getter. Resolution is fail-closed: if any
    /// part of the MultiFloors API changed, nothing is patched and one
    /// warning names the degradation (verified against the 1.6 assembly,
    /// temp/mf-decomp).
    internal static class Patch_MultiFloorsWorkScan
    {
        private delegate ref List<WorkGiver> GiverLaneRef(Pawn_WorkSettings workSettings);
        private delegate ref bool OrderDirtyRef(Pawn_WorkSettings workSettings);

        private static readonly AccessTools.FieldRef<Pawn_WorkSettings, Pawn> PawnOf =
            AccessTools.FieldRefAccess<Pawn_WorkSettings, Pawn>("pawn");

        private static GiverLaneRef? highLane;
        private static GiverLaneRef? lowLane;
        private static OrderDirtyRef? orderDirty;
        private static AccessTools.FieldRef<bool>? scanningHighPriority;
        private static Func<List<WorkGiver>, Pawn, List<WorkGiver>>? levelFilter;

        /// Lane source stamps —
        /// Owner: process static, entries per managed pawn seen by MultiFloors'
        ///   scanner.
        /// Key: pawn identity.
        /// Value: the CompiledJobOrders.NormalFor list reference last copied
        ///   into MultiFloors' high-priority lane (the copy itself is owned and
        ///   mutated by MultiFloors).
        /// Dependencies: compiled job order identity for the pawn only —
        ///   already governed by CompiledJobOrders' invalidation matrix, so no
        ///   independent invalidation inputs exist.
        /// Refresh policy: immediate — compared on every scan; the lanes are
        ///   recopied only when the compiled list identity moved.
        /// Equality policy: reference identity; an unchanged compiled list
        ///   reuses the existing lane contents without copying or allocating.
        /// Teardown: entries removed on pawn destroy, cleared on world teardown.
        private static readonly Dictionary<Pawn, List<WorkGiver>> laneSources =
            new Dictionary<Pawn, List<WorkGiver>>(ReferenceIdentityComparer<Pawn>.Instance);

        internal static void NotifyDestroyed(Pawn pawn) => laneSources.Remove(pawn);

        internal static void ReleaseForTeardown() => laneSources.Clear();

        internal static void Install(Harmony harmony)
        {
            var scanner = GenTypes.GetTypeInAnyAssembly(
                "MultiFloors.HarmonyPatches.HarmonyPatch_ScanJobsOnOtherLevelPrioritized");
            if (scanner == null) return; // MultiFloors not installed.

            try
            {
                var target = scanner.GetMethod("GetWorkGiversInOrderNormal",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(Pawn_WorkSettings) }, null);
                var scanFlag = scanner.GetField("ScanningHighPriorityWorkGivers",
                    BindingFlags.NonPublic | BindingFlags.Static);
                var filter = scanner.GetMethod("GetFilteredWorkGiversInOrder",
                    BindingFlags.NonPublic | BindingFlags.Static, null,
                    new[] { typeof(List<WorkGiver>), typeof(Pawn) }, null);
                var fields = GenTypes.GetTypeInAnyAssembly("MultiFloors.PrepatcherFields");
                var high = LaneAccessor(fields, "HighPriorityWorkGiversInOrderNormal");
                var low = LaneAccessor(fields, "LowPriorityWorkGiversInOrderNormal");
                var dirtyMethod = fields?.GetMethod("WorkGiversOrderDirty",
                    BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(Pawn_WorkSettings) }, null);
                if (target == null || high == null || low == null
                    || scanFlag == null || scanFlag.FieldType != typeof(bool)
                    || filter == null || filter.ReturnType != typeof(List<WorkGiver>)
                    || dirtyMethod == null
                    || dirtyMethod.ReturnType != typeof(bool).MakeByRefType())
                    throw new MissingMemberException(
                        "MultiFloors prioritized work scanner members not found");

                scanningHighPriority = AccessTools.StaticFieldRefAccess<bool>(scanFlag);
                levelFilter = (Func<List<WorkGiver>, Pawn, List<WorkGiver>>)filter
                    .CreateDelegate(typeof(Func<List<WorkGiver>, Pawn, List<WorkGiver>>));
                orderDirty = (OrderDirtyRef)dirtyMethod.CreateDelegate(typeof(OrderDirtyRef));
                highLane = high;
                lowLane = low;
                harmony.Patch(target, prefix: new HarmonyMethod(
                    typeof(Patch_MultiFloorsWorkScan), nameof(GetWorkGiversPrefix)));
            }
            catch (Exception exception)
            {
                highLane = null;
                lowLane = null;
                orderDirty = null;
                scanningHighPriority = null;
                levelFilter = null;
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

        public static bool GetWorkGiversPrefix(
            Pawn_WorkSettings workSettings, ref List<WorkGiver> __result)
        {
            var pawn = PawnOf(workSettings);
            if (pawn == null || RoleStore.Current?.IsManaged(pawn) != true)
                return true;

            var compiled = CompiledJobOrders.NormalFor(pawn);
            ref List<WorkGiver> high = ref highLane!(workSettings);
            ref List<WorkGiver> low = ref lowLane!(workSettings);
            if (high == null || low == null
                || !laneSources.TryGetValue(pawn, out var source)
                || !ReferenceEquals(source, compiled))
            {
                if (high == null) high = new List<WorkGiver>(compiled.Count);
                if (low == null) low = new List<WorkGiver>();
                // MultiFloors owns the lane lists (its own rebuild path Clears
                // them), so the cache-owned compiled list is copied in, never
                // handed over.
                high.Clear();
                for (int i = 0; i < compiled.Count; i++)
                    high.Add(compiled[i]);
                low.Clear();
                laneSources[pawn] = compiled;
            }
            // MultiFloors' lane rebuild stays bypassed while the pawn is
            // managed; keeping its flag dirty makes a pawn that later leaves
            // management rebuild from restored vanilla priorities immediately.
            orderDirty!(workSettings) = true;
            var lane = scanningHighPriority!() ? high : low;
            __result = levelFilter!(lane, pawn);
            return false;
        }
    }
}
