using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace WorkRoles.Patches
{
    /// A caravan's LOCAL hour normally flips only at the global hour boundary
    /// (timezones are whole hours), which the game component's hourly schedule
    /// already covers. The one exception is the caravan itself moving across a
    /// timezone meridian mid-hour — a tile change — so the transition is
    /// patched instead of polled: time-ruled caches recompile on the exact
    /// tick the crossing happens.
    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.Tile), MethodType.Setter)]
    public static class Patch_WorldObject_SetTile
    {
        public static void Prefix(WorldObject __instance, ref PlanetTile __state)
            => __state = __instance.Tile;

        public static void Postfix(WorldObject __instance, PlanetTile __state)
        {
            if (!(__instance is Caravan caravan)) return;
            var newTile = caravan.Tile;
            if (!__state.Valid || !newTile.Valid || __state == newTile) return;

            var store = RoleStore.Current;
            if (store?.roles == null) return;
            bool anyTimeRuled = false;
            foreach (var role in store.roles)
                if (role != null && role.activeHours != Role.AllHours)
                { anyTimeRuled = true; break; }
            if (!anyTimeRuled) return;

            var grid = Find.WorldGrid;
            if (GenDate.TimeZoneAt(grid.LongLatOf(__state).x)
                == GenDate.TimeZoneAt(grid.LongLatOf(newTile).x)) return;

            CompiledJobOrders.InvalidateBatch(caravan.PawnsListForReading);
        }
    }
}
