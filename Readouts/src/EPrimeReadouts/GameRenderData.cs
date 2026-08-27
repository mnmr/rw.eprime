using System;
using EPrimeReadouts.Core;
using RimShared.Common;
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
            internal int Tick;
            internal ReadoutStore Store;
            internal CountSnapshotOptions Options;
        }

        private static readonly Func<BuildState, PoolSnapshot> buildPools =
            state => PoolSnapshot.Build(state.Store.Model.Pools, GameResourceCatalog.Instance);
        private static readonly Func<BuildState, PoolSnapshot, RenderCountSnapshot> buildCounts =
            (state, _) => GameCounts.BuildSnapshot(
                state.Map, state.Tick, state.Options);

        // Cache contract:
        // Owner: one ReadoutStore/world at a time.
        // Key: Map identity; a MultiFloors floor map resolves to its stack's
        //      canonical ground map so every floor shares one snapshot.
        // Value: immutable shared pool/count render snapshot.
        // Dependencies: PoolsVersion immediately, 204 elapsed game ticks for
        //               counts, the derived collection needs (count-basis
        //               options unioned with the stored count rules) and
        //               planned-work options immediately; planned-work scans
        //               independently every 1020 elapsed game ticks;
        //               and (while MultiFloors is active) the map-set stamp so
        //               stack membership changes rebuild entries.
        // Refresh policy: immediate structure; tick-throttled counts, except
        //               that a count-collection option change rebuilds at
        //               once (a user-authored edit must be visible while
        //               paused).
        // Equality policy: equal refreshed counts preserve snapshot identity.
        // Teardown: Remove on map removal; Reset on world teardown/owner change.
        private static ReadoutStore? cacheOwner;
        private static int cacheMapSetStamp = -1;
        private static CountSnapshotOptions cacheOptions;

        // Cache contract:
        // Owner: the same ReadoutStore/world as the render cache above.
        // Key: none (two booleans).
        // Value: whether any stored count rule forces scattered collection or
        //        forbidden inspection beyond the global options.
        // Dependencies: CountRulesVersion.
        // Refresh policy: immediate on revision change.
        // Equality policy: value booleans; equal recomputes are identical.
        // Teardown: reset with the render cache on owner change and Reset.
        private static int unionRulesVersion = -1;
        private static bool unionForcesScattered;
        private static bool unionForcesForbidden;
        private static readonly RenderDataCache<Map, int, PoolSnapshot, RenderCountSnapshot>
            cache = NewCache();

        internal static RenderDataSnapshot<PoolSnapshot, RenderCountSnapshot> Get(
            Map map,
            ReadoutStore store)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            if (store == null) throw new ArgumentNullException(nameof(store));

            map = LevelStacks.CanonicalOrSelf(map)!; // non-null for non-null input

            if (!ReferenceEquals(cacheOwner, store))
            {
                cache.Clear();
                GamePlannedWorkData.Reset();
                QualityJobsPlannedWork.Reset();
                cacheOwner = store;
                unionRulesVersion = -1;
            }
            if (LevelStacks.MultiFloorsActive
                && cacheMapSetStamp != LevelStacks.MapSetStamp)
            {
                cache.Clear();
                GamePlannedWorkData.Reset();
                cacheMapSetStamp = LevelStacks.MapSetStamp;
            }

            // Count-basis and reservation options change what the count pass
            // gathers, so they bypass the tick throttle — a struct compare per
            // call, then a counts-only invalidation on the toggle frame.
            CountSnapshotOptions options = CurrentOptions(store);
            if (!cacheOptions.Equals(options))
            {
                if (!cacheOptions.PlannedWork.Equals(options.PlannedWork))
                    GamePlannedWorkData.Reset();
                cacheOptions = options;
                cache.InvalidateCounts();
            }

            int tick = Find.TickManager.TicksGame;

            return cache.Get(
                map,
                store.PoolsVersion,
                tick,
                new BuildState
                {
                    Map = map,
                    Tick = tick,
                    Store = store,
                    Options = options,
                },
                buildPools,
                buildCounts);
        }

        /// The effective collection options: the player's count-basis options
        /// widened by the union of stored count rules, so the snapshot always
        /// gathers what any overridden token needs while unconfigured
        /// storage-only users keep the cheap pass. Quality rework is forced
        /// off while the Quality Jobs integration is unavailable so the
        /// snapshot matches what the options dialog says is in effect.
        private static CountSnapshotOptions CurrentOptions(ReadoutStore store)
        {
            var settings = EPrimeReadoutsMod.Settings;
            if (settings == null) return default;
            if (unionRulesVersion != store.CountRulesVersion)
            {
                unionForcesScattered =
                    CountRuleUnion.AnyForcesScattered(store.Model.CountRules);
                unionForcesForbidden =
                    CountRuleUnion.AnyForcesForbidden(store.Model.CountRules);
                unionRulesVersion = store.CountRulesVersion;
            }
            return new CountSnapshotOptions(
                settings.searchStorageOnly && !unionForcesScattered,
                settings.searchHideForbidden || unionForcesForbidden,
                new PlannedWorkOptions(
                    settings.reserveForBills,
                    settings.reserveForBuildables,
                    settings.qualityJobsRework && QualityJobsBridge.Available));
        }

        internal static void Remove(Map map)
        {
            if (map == null) return;
            cache.Remove(map);
            GamePlannedWorkData.Remove(map);
            QualityJobsPlannedWork.Reset();
            if (cache.Count == 0) cacheOwner = null;
        }

        internal static void Reset()
        {
            cache.Clear();
            GamePlannedWorkData.Reset();
            cacheOwner = null;
            cacheMapSetStamp = -1;
            cacheOptions = default;
            unionRulesVersion = -1;
            unionForcesScattered = false;
            unionForcesForbidden = false;
            QualityJobsPlannedWork.Reset();
        }

        private static RenderDataCache<Map, int, PoolSnapshot, RenderCountSnapshot> NewCache() =>
            new RenderDataCache<Map, int, PoolSnapshot, RenderCountSnapshot>(
                CountRefreshIntervalTicks);
    }
}
