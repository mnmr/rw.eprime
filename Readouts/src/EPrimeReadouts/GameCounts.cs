using System.Collections.Generic;
using EPrimeReadouts.Core;
using RimShared.Common;
using RimWorld;
using Verse;

namespace EPrimeReadouts
{
    /// Builds the count payload used by GameRenderData. The caller owns the
    /// 204-tick stock cadence; this class performs one deterministic pass over
    /// stock and planned-work debt together.
    /// When the map belongs to a MultiFloors stack, the pass covers every
    /// level map in ascending level order so readouts show stack totals.
    public static class GameCounts
    {
        private struct DefTally
        {
            public int ExtraStored;
            public int StoredUnforbidden;
            public int StoredForbidden;
            public int ScatteredUnforbidden;
            public int ScatteredForbidden;
        }

        // Reusable single-pass scratch buffer, not a cache: per-def stack
        // tallies keyed by ThingDef identity so the per-thing hot loop never
        // hashes defName strings; the flush converts each def to the Core
        // snapshot's string keys exactly once. Owner: process, main thread
        // only (count builders run from the game update/GUI path). Cleared
        // after every map pass, so no Thing, Map, or per-save state is
        // retained between passes.
        private static readonly Dictionary<ThingDef, DefTally> tallies =
            new Dictionary<ThingDef, DefTally>(ReferenceIdentityComparer<ThingDef>.Instance);

        internal static RenderCountSnapshot BuildSnapshot(
            Map map,
            CountSnapshotOptions options)
        {
            var accumulator = new CountAccumulator();
            QualityJobsPlannedWorkSnapshot qualityJobs =
                options.PlannedWork.QualityRework
                    ? QualityJobsBridge.Current()
                    : QualityJobsPlannedWorkSnapshot.Empty;
            Dictionary<int, Map>? levels = LevelStacks.LevelsOf(map);
            if (levels == null)
            {
                AccumulateMap(map, accumulator, options, qualityJobs);
                return accumulator.ToSnapshot();
            }

            // Ascending level order keeps the pass deterministic; the queried
            // map is accumulated directly if the controller omits it.
            var order = new List<int>(levels.Keys);
            order.Sort();
            bool sawQueriedMap = false;
            for (int i = 0; i < order.Count; i++)
            {
                Map level = levels[order[i]];
                if (level == null || level.Disposed) continue;
                if (ReferenceEquals(level, map)) sawQueriedMap = true;
                AccumulateMap(level, accumulator, options, qualityJobs);
            }
            if (!sawQueriedMap)
                AccumulateMap(map, accumulator, options, qualityJobs);
            return accumulator.ToSnapshot();
        }

        private static void AccumulateMap(
            Map map,
            CountAccumulator accumulator,
            CountSnapshotOptions options,
            QualityJobsPlannedWorkSnapshot qualityJobs)
        {
            // Zero-valued entries are skipped: consumers resolve a missing
            // key as zero, and the search candidate universe comes from
            // GameResourceCatalog.SearchableDefNames rather than zero-seeded
            // snapshot entries. This keeps the published dictionary sized by
            // what actually exists, not by the loaded def database.
            foreach (var pair in map.resourceCounter.AllCountedAmounts)
                if (pair.Value != 0)
                    accumulator.Add(pair.Key.defName, pair.Key.shortHash, pair.Value);

            // Single stock pass over the haulable lister — a flat list scan —
            // instead of re-walking every slot-group cell the way vanilla's
            // ResourceCounter already did this boundary. Storedness is a
            // haul-destination grid lookup per thing. Semantics match the
            // former stored+scattered passes: extra counted defs in storage
            // feed the group-count basis exactly as vanilla's counter would
            // if it knew them, and every stack feeds the search breakdown
            // with its disposition. The forbidden flag reads the outer thing
            // (a minified wrapper carries the comp); freshness and fog read
            // the inner. Known narrowing: a counted def that is storable but
            // never haulable is absent from the lister and gets no search
            // breakdown entry; consumers resolve it through
            // RenderCountSnapshot.SearchCountOf, which falls back to the
            // vanilla AllCountedAmounts figure above as stored and
            // unforbidden.
            // A modded thing can throw mid-pass (freshness, forbidden, fog
            // lookups); the finally keeps its partial tallies out of the next
            // pass, which would otherwise publish inflated counts.
            try
            {
                bool includeScattered = options.IncludeScattered;
                var things = map.listerThings.ThingsInGroup(
                    ThingRequestGroup.HaulableEver);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    bool stored = thing.IsInAnyStorage();
                    if (!stored && !includeScattered) continue;
                    var inner = thing.GetInnerIfMinified();
                    bool extra = GameResourceCatalog.IsExtraCountedDef(inner.def);
                    if (!extra && !inner.def.CountAsResource) continue;
                    if (inner.IsNotFresh()) continue;
                    if (thing.Position.Fogged(map)) continue;
                    bool forbidden = options.InspectForbidden
                        && thing.IsForbidden(Faction.OfPlayer);
                    tallies.TryGetValue(inner.def, out DefTally tally);
                    if (stored)
                    {
                        if (extra) tally.ExtraStored += inner.stackCount;
                        if (forbidden) tally.StoredForbidden += inner.stackCount;
                        else tally.StoredUnforbidden += inner.stackCount;
                    }
                    else if (forbidden) tally.ScatteredForbidden += inner.stackCount;
                    else tally.ScatteredUnforbidden += inner.stackCount;
                    tallies[inner.def] = tally;
                }

                // Flush the def-keyed tallies into the string-keyed accumulator.
                // Emission order does not matter: search tallies are additive and
                // the fingerprint folds commutatively.
                foreach (var pair in tallies)
                {
                    ThingDef def = pair.Key;
                    DefTally tally = pair.Value;
                    if (tally.ExtraStored != 0)
                        accumulator.Add(def.defName, def.shortHash, tally.ExtraStored);
                    if (tally.StoredUnforbidden != 0)
                        accumulator.AddSearch(def.defName, def.shortHash,
                            tally.StoredUnforbidden, stored: true, forbidden: false);
                    if (tally.StoredForbidden != 0)
                        accumulator.AddSearch(def.defName, def.shortHash,
                            tally.StoredForbidden, stored: true, forbidden: true);
                    if (tally.ScatteredUnforbidden != 0)
                        accumulator.AddSearch(def.defName, def.shortHash,
                            tally.ScatteredUnforbidden, stored: false, forbidden: false);
                    if (tally.ScatteredForbidden != 0)
                        accumulator.AddSearch(def.defName, def.shortHash,
                            tally.ScatteredForbidden, stored: false, forbidden: true);
                }
            }
            finally
            {
                tallies.Clear();
            }

            // Planned-work debt is read on the same pass as stock: sampled at
            // different ticks, material already picked up for a frame would
            // leave stock and still count as owed.
            if (options.PlannedWork.Any)
                PlannedWorkCounts.Accumulate(map, accumulator,
                    options.PlannedWork, qualityJobs);
        }

        /// Current count for a single def from the shared render snapshot.
        public static int LiveCount(Map map, ReadoutStore store, ThingDef def)
        {
            if (map == null || store == null || def == null) return 0;
            var snapshot = GameRenderData.Get(map, store).Counts;
            return snapshot.Counts.TryGetValue(def.defName, out int count) ? count : 0;
        }

    }
}
