using System.Collections.Generic;
using System.Reflection;
using RimWorld.Planet;
using Verse;

namespace WorkRoles
{
    /// Canonicalizes floor maps to the map at the bottom of their stack, so
    /// every location consumer (location rules, scope filter, recommendations)
    /// treats a multi-map stack as one location. Three lanes: vanilla pocket
    /// maps (undercaves, Strata levels, and other mods built on them) follow
    /// their source map;
    /// MultiFloors' level controller and As above So below's ABApi supply the
    /// ground map where the pocket chain doesn't (verified against the 1.6
    /// assemblies, temp/inspect-multifloors.ps1 + temp/inspect-asabove2.ps1).
    internal static class FloorMaps
    {
        // Strata digs have no depth cap; 32 exceeds any practical stack.
        private const int MaxDepth = 32;

        private static bool resolved;
        private static MethodInfo? tryGetController; // LevelUtility.TryGetLevelControllerOnCurrentTile[Always](Map, out MF_LevelMapComp)
        private static PropertyInfo? mapByLevel;     // MF_LevelMapComp.MapByLevel: Dictionary<int, Map>
        private static MethodInfo? abGetGroundMap;   // AsAboveSoBelow.ABApi.GetGroundMap(Map)

        // Stacks change only when maps are created or removed, but a same-tick
        // destroy+create (gravship stack travel) keeps the count equal, so the
        // guard hashes map ids instead.
        private static readonly Dictionary<Map, Map> cache = new Dictionary<Map, Map>();
        private static int cacheStamp = -1;

        internal static void ReleaseForTeardown()
        {
            cache.Clear();
            cacheStamp = -1;
        }

        internal static Map? Canonical(Map? map)
        {
            if (map == null) return null;
            var maps = Find.Maps;
            if (maps != null)
            {
                int stamp = maps.Count;
                for (int i = 0; i < maps.Count; i++)
                    stamp = unchecked(stamp * 31 + maps[i].uniqueID);
                if (stamp != cacheStamp)
                {
                    cache.Clear();
                    cacheStamp = stamp;
                }
            }
            if (cache.TryGetValue(map, out Map canonical)) return canonical;
            canonical = Compute(map);
            cache[map] = canonical;
            return canonical;
        }

        private static Map Compute(Map map)
        {
            Map current = map;
            for (int depth = 0; depth < MaxDepth; depth++)
            {
                Map? next = SourceOf(current) ?? GroundOf(current) ?? ColumnGroundOf(current);
                if (next == null || next == current) break;
                current = next;
            }
            return current;
        }

        private static Map? SourceOf(Map map) =>
            map.Parent is PocketMapParent pocket ? pocket.sourceMap : null;

        private static Map? GroundOf(Map map)
        {
            Resolve();
            if (tryGetController == null) return null;
            try
            {
                var args = new object?[] { map, null };
                if (!(bool)tryGetController.Invoke(null, args) || args[1] == null)
                    return null;
                return mapByLevel!.GetValue(args[1]) // resolved together with tryGetController
                    is System.Collections.IDictionary levels && levels.Contains(0)
                    ? levels[0] as Map
                    : null;
            }
            catch (System.Exception exception)
            {
                tryGetController = null;
                Log.Warning("[WorkRoles] MultiFloors level lookup failed; floor maps "
                    + "list as separate locations: " + exception.Message);
                return null;
            }
        }

        private static Map? ColumnGroundOf(Map map)
        {
            Resolve();
            if (abGetGroundMap == null) return null;
            try
            {
                return abGetGroundMap.Invoke(null, new object[] { map }) as Map;
            }
            catch (System.Exception exception)
            {
                abGetGroundMap = null;
                Log.Warning("[WorkRoles] As above So below ground-map lookup failed; "
                    + "its levels list as separate locations: " + exception.Message);
                return null;
            }
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            ResolveAsAboveSoBelow();
            var utility = GenTypes.GetTypeInAnyAssembly("MultiFloors.LevelUtility");
            var comp = GenTypes.GetTypeInAnyAssembly("MultiFloors.MF_LevelMapComp");
            if (utility == null || comp == null) return;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            tryGetController =
                utility.GetMethod("TryGetLevelControllerOnCurrentTileAlways", flags)
                ?? utility.GetMethod("TryGetLevelControllerOnCurrentTile", flags);
            mapByLevel = comp.GetProperty("MapByLevel");
            if (tryGetController == null || mapByLevel == null
                || tryGetController.GetParameters().Length != 2)
            {
                tryGetController = null;
                mapByLevel = null;
                Log.Warning("[WorkRoles] MultiFloors detected but its level API "
                    + "changed; floor maps list as separate locations.");
            }
        }

        private static void ResolveAsAboveSoBelow()
        {
            var api = GenTypes.GetTypeInAnyAssembly("AsAboveSoBelow.ABApi");
            if (api == null) return;
            abGetGroundMap = api.GetMethod("GetGroundMap",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Map) }, null);
            if (abGetGroundMap == null)
                Log.Warning("[WorkRoles] As above So below detected but its API "
                    + "changed; its levels list as separate locations.");
        }
    }
}
