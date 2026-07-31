using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using WorkRoles.Core;

namespace WorkRoles.Patches
{
    /// A LOCAL hour normally flips only at the global hour boundary
    /// (timezones are whole hours), which the boundary gates already cover.
    /// The one exception is an object moving across a timezone meridian
    /// mid-hour — a tile change — so the transition is patched instead of
    /// polled: time-ruled caches recompile on the exact crossing tick.
    /// Covers caravans and live maps (dev map moves, modded map parents).
    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.Tile), MethodType.Setter)]
    public static class Patch_WorldObject_SetTile
    {
        public static void Prefix(WorldObject __instance, ref PlanetTile __state)
            => __state = __instance.Tile;

        public static void Postfix(WorldObject __instance, PlanetTile __state)
        {
            var caravan = __instance as Caravan;
            var map = (__instance as MapParent)?.Map;
            if (caravan == null && map == null) return;
            var newTile = __instance.Tile;
            if (!__state.Valid || !newTile.Valid || __state == newTile) return;

            var store = RoleStore.Current;
            if (store?.roles == null) return;
            bool anyTimeRuled = false;
            foreach (var role in store.roles)
                if (role != null && role.activeHours != Role.AllHours)
                { anyTimeRuled = true; break; }

            var grid = Find.WorldGrid;
            switch (TimezoneCrossingPolicy.Respond(anyTimeRuled,
                GenDate.TimeZoneAt(grid.LongLatOf(__state).x),
                GenDate.TimeZoneAt(grid.LongLatOf(newTile).x),
                isTraveler: caravan != null, hasSpawnedMap: map != null))
            {
                case TimezoneCrossingResponse.InvalidateTravelerPawns:
                    CompiledJobOrders.InvalidateBatch(caravan.PawnsListForReading);
                    break;
                case TimezoneCrossingResponse.InvalidateMapTimeRuled:
                    CompiledJobOrders.InvalidateTimeRuledForMovedMap(map);
                    break;
            }
        }
    }
}
