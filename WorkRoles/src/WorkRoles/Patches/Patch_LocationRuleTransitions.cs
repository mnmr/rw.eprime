using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WorkRoles.Patches
{
    /// Location rules are snapshot inputs. Invalidate on the exact transitions
    /// that can change map classification; never poll map state.
    internal static class LocationRuleTransitions
    {
        internal static void Invalidate(Map map)
        {
            if (map != null)
                CompiledJobOrders.InvalidateLocationRules(map);
        }
    }

    /// Covers a spawned or minified grav engine, plus a spawned root holder
    /// (pawn, transporter, etc.) carrying one.
    [HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
    public static class Patch_Thing_SpawnSetup_LocationRules
    {
        public static void Postfix(Thing __instance, Map map)
        {
            if (ColonyScope.ContainsGravEngine(__instance))
                LocationRuleTransitions.Invalidate(map);
        }
    }

    /// Capture the old root map before vanilla detaches the thing, then
    /// invalidate after the map's listers and holder graph have changed.
    [HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
    public static class Patch_Thing_DeSpawn_LocationRules
    {
        public static void Prefix(Thing __instance, ref Map __state)
        {
            if (ColonyScope.ContainsGravEngine(__instance))
                __state = __instance.MapHeld;
        }

        public static void Postfix(Map __state) =>
            LocationRuleTransitions.Invalidate(__state);
    }

    /// Held minified engines can cross a map boundary without the inner
    /// building spawning or despawning (trade, inventories, transporters).
    [HarmonyPatch(typeof(ThingOwner), "NotifyAdded")]
    public static class Patch_ThingOwner_NotifyAdded_LocationRules
    {
        public static void Postfix(ThingOwner __instance, Thing item)
        {
            if (!ColonyScope.ContainsGravEngine(item)) return;
            LocationRuleTransitions.Invalidate(
                ThingOwnerUtility.GetRootMap(__instance.Owner));
        }
    }

    [HarmonyPatch(typeof(ThingOwner), "NotifyRemoved")]
    public static class Patch_ThingOwner_NotifyRemoved_LocationRules
    {
        public static void Prefix(
            ThingOwner __instance, Thing item, ref Map __state)
        {
            if (ColonyScope.ContainsGravEngine(item))
                __state = ThingOwnerUtility.GetRootMap(__instance.Owner);
        }

        public static void Postfix(Map __state) =>
            LocationRuleTransitions.Invalidate(__state);
    }

    /// Settlement ownership changes alter Map.IsPlayerHome while pawns stay put.
    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.SetFaction))]
    public static class Patch_WorldObject_SetFaction_LocationRules
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
            LocationRuleTransitions.Invalidate(parent.Map);
        }
    }

    /// Landing-site settlement is another explicit home-classification change.
    [HarmonyPatch(typeof(MapParent), nameof(MapParent.Notify_MyMapSettled))]
    public static class Patch_MapParent_NotifyMyMapSettled_LocationRules
    {
        public static void Postfix(Map map) =>
            LocationRuleTransitions.Invalidate(map);
    }
}
