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
            internal PlannedWorkOptions PlannedWork;
        }

        private static readonly Func<BuildState, PoolSnapshot> buildPools =
            state => PoolSnapshot.Build(state.Store.Model.Pools, GameResourceCatalog.Instance);
        private static readonly Func<BuildState, PoolSnapshot, RenderCountSnapshot> buildCounts =
            (state, _) => GameCounts.BuildSnapshot(state.Map, state.PlannedWork);

        // Cache contract:
        // Owner: one ReadoutStore/world at a time.
        // Key: Map identity; a MultiFloors floor map resolves to its stack's
        //      canonical ground map so every floor shares one snapshot.
        // Value: immutable shared pool/count render snapshot.
        // Dependencies: PoolsVersion immediately, 204 elapsed game ticks for
        //               counts, the planned-work reservation options
        //               immediately, and (while MultiFloors is active) the
        //               map-set stamp so stack membership changes rebuild
        //               entries.
        // Refresh policy: immediate structure; tick-throttled counts, except
        //               that a reservation-option change rebuilds counts at
        //               once (a user-authored edit must be visible while
        //               paused).
        // Equality policy: equal refreshed counts preserve snapshot identity.
        // Teardown: Remove on map removal; Reset on world teardown/owner change.
        private static ReadoutStore cacheOwner;
        private static int cacheMapSetStamp = -1;
        private static PlannedWorkOptions cachePlannedWork;
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

            // Reservation options change what the count pass gathers, so they
            // must bypass the tick throttle — a struct compare per call, then
            // a counts-only invalidation on the frame the player toggles one.
            PlannedWorkOptions plannedWork = CurrentPlannedWork();
            if (!cachePlannedWork.Equals(plannedWork))
            {
                cachePlannedWork = plannedWork;
                cache.InvalidateCounts();
            }

            return cache.Get(
                map,
                store.PoolsVersion,
                Find.TickManager.TicksGame,
                new BuildState { Map = map, Store = store, PlannedWork = plannedWork },
                buildPools,
                buildCounts);
        }

        /// The player's reservation options, with quality rework forced off
        /// while the Quality Jobs integration is unavailable so the snapshot
        /// never differs from what the options dialog says is in effect.
        private static PlannedWorkOptions CurrentPlannedWork()
        {
            var settings = EPrimeReadoutsMod.Settings;
            if (settings == null) return default;
            return new PlannedWorkOptions(
                settings.reserveForBills,
                settings.reserveForBuildables,
                settings.qualityJobsRework && QualityJobsBridge.Available);
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
            cachePlannedWork = default;
        }

        private static RenderDataCache<Map, int, PoolSnapshot, RenderCountSnapshot> NewCache() =>
            new RenderDataCache<Map, int, PoolSnapshot, RenderCountSnapshot>(
                CountRefreshIntervalTicks);
    }
}
