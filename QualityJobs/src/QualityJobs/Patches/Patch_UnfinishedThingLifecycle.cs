using HarmonyLib;
using RimWorld;
using Verse;

namespace QualityJobs.Patches
{
    /// Keeps the stock-cap count cache current from narrow Thing lifecycle
    /// events, and invalidates bill/plan snapshots when their owning things move.
    /// The base-Thing patches perform only type tests for unrelated things and
    /// never allocate. The 2500-tick audit remains the recovery path for missed
    /// events or load ordering.
    [HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
    public static class Patch_UnfinishedThingSpawn
    {
        public static void Postfix(Thing __instance, Map map)
        {
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            store.NotifyTargetCountThingChanged(__instance, map);
            if (__instance is Pawn)
                store.NotifyTargetCountPawnRegistryChanged(map);
            bool isUft = __instance is UnfinishedThing;
            bool isBillGiver = __instance is IBillGiver;
            bool isPlanCandidate = __instance is Blueprint_Build
                || __instance is Building;
            if (!isUft && !isBillGiver && !isPlanCandidate) return;
            if (isUft) store.NotifyUftSpawned((UnfinishedThing)__instance, map);
            if (isBillGiver)
                store.NotifyBillGiverLocationChanged((IBillGiver)__instance);
            if (isPlanCandidate) store.NotifyPlanTargetLocationChanged(__instance);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
    public static class Patch_UnfinishedThingDespawn
    {
        public static void Prefix(Thing __instance)
        {
            if (!__instance.Spawned) return;
            QualityJobsStore? store = QualityJobsStore.Active;
            if (store == null) return;
            Map map = __instance.Map;
            store.NotifyTargetCountThingChanged(__instance, map);
            if (__instance is Pawn)
                store.NotifyTargetCountPawnRegistryChanged(map);
            bool isUft = __instance is UnfinishedThing;
            bool isBillGiver = __instance is IBillGiver;
            if (!isUft && !isBillGiver) return;
            if (isUft)
            {
                var uft = (UnfinishedThing)__instance;
                store.NotifyUftDespawned(uft, uft.Map);
            }
            if (isBillGiver)
                store.NotifyBillGiverLocationChanged((IBillGiver)__instance);
        }

        public static void Postfix(Thing __instance)
        {
            if (!(__instance is Blueprint_Build) && !(__instance is Building))
                return;
            QualityJobsStore.Active?.NotifyPlanTargetLocationChanged(__instance);
        }
    }
}
