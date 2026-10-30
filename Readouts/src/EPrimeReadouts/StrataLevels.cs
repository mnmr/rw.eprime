using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using RimWorld;
using Verse;

namespace EPrimeReadouts
{
    /// Optional Strata API binding. No maps or live collections are retained:
    /// process-owned delegates depend only on the fixed loaded assemblies.
    /// Read topology only behind LevelStacks' gate or the count refresh gate.
    internal static class StrataLevels
    {
        private static bool resolved;
        private static Func<bool>? combined;
        private static Func<int>? epoch;
        private static Func<Map, object>? links;
        private static Func<object, int>? linkCount;
        private static Func<object, int, Map>? linkMap;
        private static Func<ResourceCounter, Dictionary<ThingDef, int>>? rawCounts;

        internal static bool Combined { get { Resolve(); return combined != null && combined(); } }
        internal static int Epoch { get { Resolve(); return epoch == null ? 0 : epoch(); } }

        internal static Dictionary<ThingDef, int> RawCounts(ResourceCounter counter) => rawCounts!(counter);

        internal static List<Map>? LevelsOf(Map map)
        {
            if (!Combined) return null;
            try
            {
                object reachable = links!(map);
                int count = linkCount!(reachable);
                var result = new List<Map>(count + 1) { map };
                for (int i = 0; i < count; i++)
                {
                    Map level = linkMap!(reachable, i);
                    if (level != null && !level.Disposed && !result.Contains(level)) result.Add(level);
                }
                result.Sort(MapOrder);
                return result;
            }
            catch (Exception exception)
            {
                Disable("level lookup", exception);
                return null;
            }
        }

        private static readonly Comparison<Map> MapOrder = (left, right) => left.uniqueID.CompareTo(right.uniqueID);

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            Type resources = GenTypes.GetTypeInAnyAssembly("Strata.StrataResources");
            Type graph = GenTypes.GetTypeInAnyAssembly("Strata.LevelGraph");
            if (resources == null || graph == null) return;
            try
            {
                const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                FieldInfo flag = resources.GetField("Combined", statics)!;
                FieldInfo revision = graph.GetField("graphEpoch", statics)!;
                MethodInfo reachable = graph.GetMethod("ReachableLevels", new[] { typeof(Map) })!;
                Type listType = reachable.ReturnType;
                PropertyInfo item = listType.GetProperty("Item")!;
                FieldInfo mapField = item.PropertyType.GetField("map")!;
                var map = Expression.Parameter(typeof(Map), "map");
                var list = Expression.Parameter(typeof(object), "list");
                var index = Expression.Parameter(typeof(int), "index");
                var counter = Expression.Parameter(typeof(ResourceCounter), "counter");
                links = Expression.Lambda<Func<Map, object>>(Expression.Convert(Expression.Call(reachable, map), typeof(object)), map).Compile();
                linkCount = Expression.Lambda<Func<object, int>>(Expression.Property(Expression.Convert(list, listType), "Count"), list).Compile();
                linkMap = Expression.Lambda<Func<object, int, Map>>(Expression.Field(
                    Expression.Property(Expression.Convert(list, listType), item, index), mapField), list, index).Compile();
                rawCounts = Expression.Lambda<Func<ResourceCounter, Dictionary<ThingDef, int>>>(Expression.Field(counter,
                    typeof(ResourceCounter).GetField("countedAmounts", BindingFlags.Instance | BindingFlags.NonPublic)!), counter).Compile();
                epoch = Expression.Lambda<Func<int>>(Expression.Field(null, revision)).Compile();
                combined = Expression.Lambda<Func<bool>>(Expression.Field(null, flag)).Compile();
            }
            catch (Exception exception) { Disable("API binding", exception); }
        }

        private static void Disable(string boundary, Exception exception)
        {
            combined = null;
            epoch = null;
            links = null;
            linkCount = null;
            linkMap = null;
            rawCounts = null;
            Log.Warning("[EPrimeReadouts] Strata " + boundary + " failed; using the game's resource counters: " + exception.Message);
        }
    }
}
