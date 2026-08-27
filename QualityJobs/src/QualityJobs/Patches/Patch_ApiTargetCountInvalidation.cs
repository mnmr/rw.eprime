using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// <summary>
    /// Narrow lifecycle hooks for the live values read by
    /// RecipeWorkerCounter.CountProducts. Every hook only marks the API cache
    /// dirty when its per-store dependency index watches the affected object.
    /// Direct field writes by other mods remain covered by the 2500-tick audit.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TryAbsorbStack))]
    internal static class Patch_ApiTargetCountAbsorbStack
    {
        internal readonly struct State
        {
            internal readonly int DestinationCount;
            internal readonly int SourceCount;
            internal readonly Map? DestinationMap;
            internal readonly Map? SourceMap;

            internal State(Thing destination, Thing source)
            {
                DestinationCount = destination.stackCount;
                SourceCount = source.stackCount;
                DestinationMap = destination.MapHeld;
                SourceMap = source.MapHeld;
            }
        }

        private static void Prefix(Thing __instance, Thing other,
            out State __state) => __state = new State(__instance, other);

        private static void Postfix(Thing __instance, Thing other, State __state)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            if (__state.DestinationCount != __instance.stackCount)
                store.NotifyTargetCountThingChanged(
                    __instance, __state.DestinationMap ?? __instance.MapHeld);
            if (__state.SourceCount != other.stackCount)
                store.NotifyTargetCountThingChanged(
                    other, __state.SourceMap ?? other.MapHeld);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.SplitOff))]
    internal static class Patch_ApiTargetCountSplitStack
    {
        internal readonly struct State
        {
            internal readonly int Count;
            internal readonly Map? Map;

            internal State(Thing thing)
            {
                Count = thing.stackCount;
                Map = thing.MapHeld;
            }
        }

        private static void Prefix(Thing __instance, out State __state)
            => __state = new State(__instance);

        private static void Postfix(Thing __instance, Thing __result,
            State __state)
        {
            if (__state.Count == __instance.stackCount) return;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            store.NotifyTargetCountThingChanged(__instance, __state.Map);
            if (__result != null)
                store.NotifyTargetCountThingChanged(__result, __state.Map);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.HitPoints), MethodType.Setter)]
    internal static class Patch_ApiTargetCountHitPoints
    {
        private static void Prefix(Thing __instance, out int __state)
            => __state = __instance.HitPoints;

        private static void Postfix(Thing __instance, int __state)
        {
            if (__state != __instance.HitPoints)
                QualityJobsStore.Active?.NotifyTargetCountThingChanged(
                    __instance, __instance.MapHeld);
        }
    }

    [HarmonyPatch(typeof(CompQuality), nameof(CompQuality.SetQuality))]
    internal static class Patch_ApiTargetCountQuality
    {
        private static void Prefix(CompQuality __instance,
            out QualityCategory __state) => __state = __instance.Quality;

        private static void Postfix(CompQuality __instance,
            QualityCategory __state)
        {
            if (__state != __instance.Quality)
                QualityJobsStore.Active?.NotifyTargetCountThingChanged(
                    __instance.parent, __instance.parent.MapHeld);
        }
    }

    [HarmonyPatch(typeof(Apparel), nameof(Apparel.WornByCorpse),
        MethodType.Setter)]
    internal static class Patch_ApiTargetCountTainted
    {
        private static void Prefix(Apparel __instance, out bool __state)
            => __state = __instance.WornByCorpse;

        private static void Postfix(Apparel __instance, bool __state)
        {
            if (__state != __instance.WornByCorpse)
                QualityJobsStore.Active?.NotifyTargetCountThingChanged(
                    __instance, __instance.MapHeld);
        }
    }

    [HarmonyPatch(typeof(ThingOwner), "NotifyAdded")]
    internal static class Patch_ApiTargetCountOwnerAdded
    {
        private static void Postfix(ThingOwner __instance, Thing item)
            => QualityJobsStore.Active?.NotifyTargetCountThingChanged(
                item, item.MapHeld
                      ?? (__instance.Owner != null
                          ? ThingOwnerUtility.GetRootMap(__instance.Owner)
                          : null));
    }

    [HarmonyPatch(typeof(ThingOwner), "NotifyRemoved")]
    internal static class Patch_ApiTargetCountOwnerRemoved
    {
        private static void Prefix(ThingOwner __instance, Thing item)
            => QualityJobsStore.Active?.NotifyTargetCountThingChanged(
                item, item.MapHeld
                      ?? (__instance.Owner != null
                          ? ThingOwnerUtility.GetRootMap(__instance.Owner)
                          : null));
    }

    [HarmonyPatch(typeof(ThingOwner), "NotifyAddedAndMergedWith")]
    internal static class Patch_ApiTargetCountOwnerMerged
    {
        private static void Prefix(ThingOwner __instance, Thing item)
            => QualityJobsStore.Active?.NotifyTargetCountThingChanged(
                item, item.MapHeld
                      ?? (__instance.Owner != null
                          ? ThingOwnerUtility.GetRootMap(__instance.Owner)
                          : null));
    }

    [HarmonyPatch(typeof(Pawn_InventoryTracker),
        nameof(Pawn_InventoryTracker.RemoveCount))]
    internal static class Patch_ApiTargetCountInventoryCount
    {
        private static void Prefix(Pawn_InventoryTracker __instance,
            ThingDef def, out int __state) => __state = __instance.Count(def);

        private static void Postfix(Pawn_InventoryTracker __instance,
            ThingDef def, int __state)
        {
            if (__state != __instance.Count(def))
                QualityJobsStore.Active?.NotifyTargetCountProductChanged(
                    def, __instance.pawn.MapHeld);
        }
    }

    [HarmonyPatch(typeof(MapPawns), nameof(MapPawns.UpdateRegistryForPawn))]
    internal static class Patch_ApiTargetCountPawnRegistry
    {
        private static void Postfix(Pawn p)
            => QualityJobsStore.Active?.NotifyTargetCountPawnRegistryChanged(
                p.MapHeld);
    }

    [HarmonyPatch(typeof(Bill_Production),
        nameof(Bill_Production.SetIncludeGroup))]
    internal static class Patch_ApiTargetCountIncludeGroup
    {
        private static void Prefix(Bill_Production __instance,
            out ISlotGroup? __state) => __state = __instance.GetIncludeSlotGroup();

        private static void Postfix(Bill_Production __instance,
            ISlotGroup? __state)
        {
            if (!ReferenceEquals(__state, __instance.GetIncludeSlotGroup()))
                QualityJobsStore.Active?.NotifyTargetCountBillInputChanged(
                    __instance);
        }
    }

    [HarmonyPatch]
    internal static class Patch_ApiTargetCountThingFilter
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo[] methods = typeof(ThingFilter).GetMethods(
                BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < methods.Length; i++)
                if (methods[i].Name == nameof(ThingFilter.SetAllow)
                    || methods[i].Name == nameof(ThingFilter.SetAllowAll)
                    || methods[i].Name == nameof(ThingFilter.SetAllowAllWhoCanMake)
                    || methods[i].Name == nameof(ThingFilter.SetDisallowAll)
                    || methods[i].Name == nameof(ThingFilter.SetFromPreset)
                    || methods[i].Name == nameof(ThingFilter.CopyAllowancesFrom))
                    yield return methods[i];
        }

        private static void Postfix(ThingFilter __instance)
            => QualityJobsStore.Active?.NotifyTargetCountFilterChanged(__instance);
    }

    [HarmonyPatch]
    internal static class Patch_ApiTargetCountSlotGroupCells
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(SlotGroup),
                nameof(SlotGroup.Notify_AddedCell));
            yield return AccessTools.Method(typeof(SlotGroup),
                nameof(SlotGroup.Notify_LostCell));
        }

        private static void Postfix(SlotGroup __instance)
            => QualityJobsStore.Active?.NotifyTargetCountSlotGroupChanged(
                __instance);
    }

    [HarmonyPatch(typeof(StorageGroupUtility),
        nameof(StorageGroupUtility.SetStorageGroup))]
    internal static class Patch_ApiTargetCountStorageGroup
    {
        private static void Prefix(IStorageGroupMember member,
            out StorageGroup? __state) => __state = member.Group;

        private static void Postfix(IStorageGroupMember member,
            StorageGroup? __state)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null || ReferenceEquals(__state, member.Group)) return;
            if (__state != null)
                store.NotifyTargetCountSlotGroupChanged(__state);
            if (member.Group != null)
                store.NotifyTargetCountSlotGroupChanged(member.Group);
        }
    }

    [HarmonyPatch(typeof(BillUtility),
        nameof(BillUtility.Notify_ISlotGroupRemoved))]
    internal static class Patch_ApiTargetCountSlotGroupRemoved
    {
        private static void Prefix(ISlotGroup group)
            => QualityJobsStore.Active?.NotifyTargetCountSlotGroupChanged(group);
    }
}
