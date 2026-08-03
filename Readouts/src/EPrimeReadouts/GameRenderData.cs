using System;
using EPrimeReadouts.Core;
using Verse;

namespace EPrimeReadouts
{
    /// One shared render-data snapshot per map. Store-backed structure updates
    /// immediately; resource counts are refreshed on vanilla's 204-tick cadence.
    internal static class GameRenderData
    {
        internal const int CountRefreshIntervalTicks = 204;

        private struct BuildState
        {
            internal Map Map;
            internal ReadoutStore Store;
        }

        private static readonly Func<BuildState, PoolSnapshot> buildPools =
            state => PoolSnapshot.Build(state.Store.Model.Pools, GameResourceCatalog.Instance);
        private static readonly Func<BuildState, PoolSnapshot, RenderCountSnapshot> buildCounts =
            (state, _) => GameCounts.BuildSnapshot(state.Map);

        // Cache contract:
        // Owner: one ReadoutStore/world at a time.
        // Key: Map identity; a MultiFloors floor map resolves to its stack's
        //      canonical ground map so every floor shares one snapshot.
        // Value: immutable shared pool/count render snapshot.
        // Dependencies: PoolsVersion immediately, 204 elapsed game ticks for
        //               counts, and (while MultiFloors is active) the map-set
        //               stamp so stack membership changes rebuild entries.
        // Refresh policy: immediate structure; tick-throttled counts.
        // Equality policy: equal refreshed counts preserve snapshot identity.
        // Teardown: Remove on map removal; Reset on world teardown/owner change.
        private static ReadoutStore cacheOwner;
        private static int cacheMapSetStamp = -1;
        private static readonly RenderDataCache<Map, int, PoolSnapshot, RenderCountSnapshot>
            cache = NewCache();

        internal static RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> Get(
            Map map,
            ReadoutStore store)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (store == null) throw new ArgumentNullException(nameof(store));

            map = LevelStacks.CanonicalOrSelf(map);

            if (!ReferenceEquals(cacheOwner, store))
            {
                cache.Clear();
                cacheOwner = store;
            }
            if (LevelStacks.MultiFloorsActive
                && cacheMapSetStamp != LevelStacks.MapSetStamp)
            {
                cache.Clear();
                cacheMapSetStamp = LevelStacks.MapSetStamp;
            }

            return cache.Get(
                map,
                store.PoolsVersion,
                Find.TickManager.TicksGame,
                new BuildState { Map = map, Store = store },
                buildPools,
                buildCounts);
        }

        internal static void Remove(Map map)
        {
            if (map == null) return;
            cache.Remove(map);
            if (cache.Count == 0) cacheOwner = null;
        }

        internal static void Reset()
        {
            cache.Clear();
            cacheOwner = null;
            cacheMapSetStamp = -1;
        }

        private static RenderDataCache<Map, int, PoolSnapshot, RenderCountSnapshot> NewCache() =>
            new RenderDataCache<Map, int, PoolSnapshot, RenderCountSnapshot>(
                CountRefreshIntervalTicks);
    }
}
