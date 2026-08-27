using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Verse;

namespace EPrimeReadouts
{
    /// MultiFloors integration: resolves the level stack a map belongs to so
    /// resource counts can aggregate across floors and every floor shares the
    /// ground map's render snapshot. This binds to MultiFloors only: Strata
    /// and "As above, so below" merge counts inside their own ResourceCounter
    /// patches (which our resourceCounter reads inherit), and "As above, so
    /// below II" keeps all levels inside a single Map, so aggregating any of
    /// them here would double-count.
    internal static class LevelStacks
    {
        // One-time binding: the loaded mod set is fixed for the process, so a
        // failed or absent probe never retries. On any API mismatch or runtime
        // failure the integration disables itself with a single warning and
        // readouts fall back to per-floor counts.
        private static bool resolved;

        // Compiled once at bind time into a cached static delegate:
        //   map => TryGetLevelControllerOnCurrentTile[Always](map, out comp)
        //          && comp != null ? comp.MapByLevel : null
        // so steady-state lookups are plain delegate calls with no
        // MethodInfo.Invoke argument-array or boxing allocations.
        private static Func<Map, Dictionary<int, Map>?>? stackOf;

        // Cache contract:
        // Owner: process; entries reference only the current session's maps.
        // Key: Map identity.
        // Value: canonical ground map reference (the map itself when it has
        //        no stack); immutable by publication.
        // Dependencies: the map-set stamp bumped on map add/remove/teardown.
        // Refresh policy: immediate; entries recompute lazily after a bump.
        // Equality policy: n/a (reference value).
        // Teardown: Reset clears entries so no removed map stays reachable.
        private static readonly Dictionary<Map, Map> canonicalCache =
            new Dictionary<Map, Map>();
        private static int mapSetStamp;
        private static int cachedStamp = -1;

        internal static bool MultiFloorsActive
        {
            get { Resolve(); return stackOf != null; }
        }

        internal static int MapSetStamp => mapSetStamp;

        // Map components construct on the load worker thread, so the bump
        // must be atomic; every reader runs on the main thread.
        internal static void BumpMapSet() =>
            System.Threading.Interlocked.Increment(ref mapSetStamp);

        internal static void Reset()
        {
            canonicalCache.Clear();
            cachedStamp = -1;
            BumpMapSet();
        }

        /// Render-path safe once resolved: a stamp compare plus a dictionary
        /// hit, except on the first lookup after the map set changed.
        internal static Map? CanonicalOrSelf(Map? map)
        {
            if (map == null) return null;
            Resolve();
            if (stackOf == null) return map;
            if (cachedStamp != mapSetStamp)
            {
                canonicalCache.Clear();
                cachedStamp = mapSetStamp;
            }
            if (canonicalCache.TryGetValue(map, out Map canonical)) return canonical;
            Dictionary<int, Map>? levels = LevelsOf(map);
            canonical = levels != null
                && levels.TryGetValue(0, out Map ground)
                && ground != null
                ? ground
                : map;
            canonicalCache[map] = canonical;
            return canonical;
        }

        /// The stack's mod-owned live level dictionary, or null when the map
        /// has no stack. Callers may only read it inside an invalidation-gated
        /// builder and must not retain or publish it.
        internal static Dictionary<int, Map>? LevelsOf(Map? map)
        {
            Resolve();
            if (stackOf == null || map == null) return null;
            try
            {
                return stackOf(map);
            }
            catch (Exception exception)
            {
                stackOf = null;
                Log.Warning("[EPrimeReadouts] MultiFloors level lookup failed; "
                    + "readouts show per-floor counts: " + exception.Message);
                return null;
            }
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            Type utility = GenTypes.GetTypeInAnyAssembly("MultiFloors.LevelUtility");
            Type comp = GenTypes.GetTypeInAnyAssembly("MultiFloors.MF_LevelMapComp");
            if (utility == null || comp == null) return;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
            MethodInfo tryGet =
                utility.GetMethod("TryGetLevelControllerOnCurrentTileAlways", flags)
                ?? utility.GetMethod("TryGetLevelControllerOnCurrentTile", flags);
            PropertyInfo mapByLevel = comp.GetProperty("MapByLevel");
            if (tryGet == null
                || tryGet.ReturnType != typeof(bool)
                || mapByLevel == null
                || mapByLevel.PropertyType != typeof(Dictionary<int, Map>)
                || !TryGetShapeMatches(tryGet, comp))
            {
                Log.Warning("[EPrimeReadouts] MultiFloors detected but its level "
                    + "API changed; readouts show per-floor counts.");
                return;
            }
            try
            {
                stackOf = CompileStackOf(tryGet, mapByLevel, comp);
            }
            catch (Exception exception)
            {
                Log.Warning("[EPrimeReadouts] MultiFloors level API binding "
                    + "failed; readouts show per-floor counts: " + exception.Message);
            }
        }

        private static bool TryGetShapeMatches(MethodInfo tryGet, Type comp)
        {
            ParameterInfo[] parameters = tryGet.GetParameters();
            return parameters.Length == 2
                && parameters[0].ParameterType == typeof(Map)
                && parameters[1].ParameterType == comp.MakeByRefType();
        }

        private static Func<Map, Dictionary<int, Map>?> CompileStackOf(
            MethodInfo tryGet, PropertyInfo mapByLevel, Type compType)
        {
            ParameterExpression map = Expression.Parameter(typeof(Map), "map");
            ParameterExpression comp = Expression.Variable(compType, "comp");
            Expression body = Expression.Block(
                new[] { comp },
                Expression.Condition(
                    Expression.AndAlso(
                        Expression.Call(tryGet, map, comp),
                        Expression.ReferenceNotEqual(
                            comp, Expression.Constant(null, compType))),
                    Expression.Property(comp, mapByLevel),
                    Expression.Constant(null, typeof(Dictionary<int, Map>))));
            return Expression.Lambda<Func<Map, Dictionary<int, Map>?>>(body, map)
                .Compile();
        }
    }
}
