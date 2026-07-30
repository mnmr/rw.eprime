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
            (state, pools) => GameCounts.BuildSnapshot(state.Map, state.Store, pools);

        private static ReadoutStore cacheOwner;
        private static RenderDataCache<Map, int, PoolSnapshot, RenderCountSnapshot> cache = NewCache();

        internal static RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> Get(
            Map map,
            ReadoutStore store)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (store == null) throw new ArgumentNullException(nameof(store));

            if (!ReferenceEquals(cacheOwner, store))
            {
                cacheOwner = store;
                cache = NewCache();
            }

            return cache.Get(
                map,
                store.PoolsVersion,
                Find.TickManager.TicksGame,
                new BuildState { Map = map, Store = store },
                buildPools,
                buildCounts);
        }

        private static RenderDataCache<Map, int, PoolSnapshot, RenderCountSnapshot> NewCache() =>
            new RenderDataCache<Map, int, PoolSnapshot, RenderCountSnapshot>(
                CountRefreshIntervalTicks);
    }
}
