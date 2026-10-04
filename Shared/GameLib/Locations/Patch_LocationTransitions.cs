using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimShared.GameLib
{
    /// Map classification is a snapshot input. Invalidate on the exact
    /// transitions that can change it; never poll map state. The
    /// classification cache publishes MapClassifications.LocationRevision,
    /// which is the dependency location-aware snapshots gate on. A mod with
    /// further location-dependent state implements the optional hooks.
    internal static partial class LocationTransitions
    {
        internal static void Invalidate(Map map)
        {
            if (map != null)
            {
                MapClassifications.InvalidateClassification(map);
                OnClassificationInvalidated(map);
            }
        }

        internal static void InvalidateMapSet()
        {
            MapClassifications.InvalidateMapSet();
            OnMapSetChanged();
        }

        /// Optional per-mod hook: runs after a map's classification was
        /// invalidated.
        static partial void OnClassificationInvalidated(Map map);

        /// Optional per-mod hook: runs after a map entered or left the
        /// loaded map set.
        static partial void OnMapSetChanged();
    }

    /// Covers a spawned or minified grav engine, plus a spawned root holder
    /// (pawn, transporter, etc.) carrying one.
    [HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
    public static class Patch_Thing_SpawnSetup_Locations
    {
        public static void Postfix(Thing __instance, Map map)
        {
            if (MapClassifications.ContainsGravEngine(__instance))
                LocationTransitions.Invalidate(map);
        }
    }

    /// Capture the old root map before vanilla detaches the thing, then
    /// invalidate after the map's listers and holder graph have changed.
    [HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
    public static class Patch_Thing_DeSpawn_Locations
    {
        public static void Prefix(Thing __instance, ref Map __state)
        {
            if (MapClassifications.ContainsGravEngine(__instance))
                __state = __instance.MapHeld;
        }

        public static void Postfix(Map __state) =>
            LocationTransitions.Invalidate(__state);
    }

    /// Held minified engines can cross a map boundary without the inner
    /// building spawning or despawning (trade, inventories, transporters).
    [HarmonyPatch(typeof(ThingOwner), "NotifyAdded")]
    public static class Patch_ThingOwner_NotifyAdded_Locations
    {
        public static void Postfix(ThingOwner __instance, Thing item)
        {
            if (!MapClassifications.ContainsGravEngine(item)) return;
            LocationTransitions.Invalidate(
                ThingOwnerUtility.GetRootMap(__instance.Owner));
        }
    }

    [HarmonyPatch(typeof(ThingOwner), "NotifyRemoved")]
    public static class Patch_ThingOwner_NotifyRemoved_Locations
    {
        public static void Prefix(
            ThingOwner __instance, Thing item, ref Map __state)
        {
            if (MapClassifications.ContainsGravEngine(item))
                __state = ThingOwnerUtility.GetRootMap(__instance.Owner);
        }

        public static void Postfix(Map __state) =>
            LocationTransitions.Invalidate(__state);
    }

    /// Settlement ownership changes alter Map.IsPlayerHome while pawns stay put.
    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.SetFaction))]
    public static class Patch_WorldObject_SetFaction_Locations
    {
        public static void Prefix(WorldObject __instance, ref Faction __state) =>
            __state = __instance.Faction;

        public static void Postfix(
            WorldObject __instance, Faction __state, Faction newFaction)
        {
            if (__state == newFaction
                || !(__instance is MapParent parent)
                || !parent.HasMap)
                return;
            LocationTransitions.Invalidate(parent.Map);
        }
    }

    /// Landing-site settlement is another explicit home-classification change.
    [HarmonyPatch(typeof(MapParent), nameof(MapParent.Notify_MyMapSettled))]
    public static class Patch_MapParent_NotifyMyMapSettled_Locations
    {
        public static void Postfix(Map map) =>
            LocationTransitions.Invalidate(map);
    }

    /// Maps can enter or leave the loaded map set without changing a parent
    /// faction. These events own settlement membership and floor-stack
    /// canonicalization.
    [HarmonyPatch(typeof(Game), nameof(Game.AddMap))]
    public static class Patch_Game_AddMap_Locations
    {
        public static void Postfix() => LocationTransitions.InvalidateMapSet();
    }

    [HarmonyPatch(typeof(Game), nameof(Game.DeinitAndRemoveMap))]
    public static class Patch_Game_RemoveMap_Locations
    {
        public static void Postfix() => LocationTransitions.InvalidateMapSet();
    }
}
