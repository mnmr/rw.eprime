using System;
using System.Collections.Generic;
using EPrimeReadouts.Core;
using Verse;

namespace EPrimeReadouts
{
    /// Planned-work scans are substantially broader than stock collection, so
    /// they run on their own slower cadence and are replayed into each ordinary
    /// count snapshot in between.
    internal static class GamePlannedWorkData
    {
        internal const int RefreshIntervalTicks = 1020;

        private readonly struct BuildState
        {
            internal BuildState(
                Map map,
                PlannedWorkOptions options)
            {
                Map = map;
                Options = options;
            }

            internal readonly Map Map;
            internal readonly PlannedWorkOptions Options;
        }

        private static readonly Func<BuildState, CachedPlannedWork> build =
            Build;
        private static readonly TickValueCache<Map, CachedPlannedWork> cache =
            new TickValueCache<Map, CachedPlannedWork>(
                RefreshIntervalTicks, IdentityComparer<Map>.Instance);

        internal static CachedPlannedWork Get(
            Map map,
            int tick,
            PlannedWorkOptions options)
            => cache.Get(map, tick,
                new BuildState(map, options), build);

        internal static void Remove(Map map)
        {
            if (map != null) cache.Remove(map);
        }

        internal static void Reset() => cache.Clear();

        private static CachedPlannedWork Build(BuildState state)
        {
            if (!state.Options.Any) return CachedPlannedWork.Empty;
            var accumulator = new CountAccumulator();
            QualityJobsPlannedWorkSnapshot qualityJobs =
                state.Options.QualityRework
                    ? QualityJobsPlannedWork.Current()
                    : QualityJobsPlannedWorkSnapshot.Empty;
            PlannedWorkCounts.Accumulate(state.Map, accumulator,
                state.Options, qualityJobs);
            return new CachedPlannedWork(accumulator.ToSnapshot());
        }
    }

    internal sealed class CachedPlannedWork
    {
        internal static readonly CachedPlannedWork Empty =
            new CachedPlannedWork();

        private readonly IReadOnlyList<PlannedWorkEntry> entries;
        private readonly int[] resourceHashes;

        private CachedPlannedWork()
        {
            entries = Array.Empty<PlannedWorkEntry>();
            resourceHashes = Array.Empty<int>();
        }

        internal CachedPlannedWork(RenderCountSnapshot snapshot)
        {
            entries = snapshot.PlannedWork;
            resourceHashes = new int[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                ThingDef? def = DefDatabase<ThingDef>.GetNamedSilentFail(
                    entries[i].ResourceDefName);
                resourceHashes[i] = def?.shortHash ?? 0;
            }
        }

        internal void AccumulateInto(CountAccumulator accumulator)
        {
            for (int i = 0; i < entries.Count; i++)
                accumulator.AddCachedPlannedWork(
                    entries[i], resourceHashes[i]);
        }
    }
}
