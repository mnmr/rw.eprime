using System;
using System.Collections.Generic;

namespace RimShared.Common
{
    public sealed class RenderDataSnapshot<TStructure, TCounts>
    {
        internal RenderDataSnapshot(TStructure structure, TCounts counts)
        {
            Structure = structure;
            Counts = counts;
        }

        public TStructure Structure { get; }
        public TCounts Counts { get; }
    }

    /// Shared per-key render data. Structure and count data have independent
    /// invalidation rules so user edits can apply immediately while expensive
    /// count refreshes remain tick-throttled.
    /// Cache contract: Owner = caller; Key = <typeparamref name="TKey"/>;
    /// Value = immutable <see cref="RenderDataSnapshot{TStructure,TCounts}"/>;
    /// Dependencies = structure revision, tick and caller-owned builder inputs;
    /// Refresh policy = immediate structure and tick-throttled counts;
    /// Equality policy = equal count refreshes preserve snapshot identity;
    /// Teardown = <see cref="Remove"/> per owner key or <see cref="Clear"/>.
    public sealed class RenderDataCache<TKey, TRevision, TStructure, TCounts>
        where TKey : notnull
    {
        private sealed class Entry
        {
            public RenderDataSnapshot<TStructure, TCounts> Snapshot = null!; // Every construction site assigns it.
            public int LastCountRefreshTick;
            public TRevision StructureRevision = default!; // Every construction site assigns it.
            /// Set by InvalidateCounts; cleared by the rebuild it forces.
            public bool CountsDirty;
        }

        private readonly Dictionary<TKey, Entry> entries = new Dictionary<TKey, Entry>();
        private readonly int countRefreshInterval;
        private readonly IEqualityComparer<TCounts> countsComparer;

        public int Count => entries.Count;

        public RenderDataCache(int countRefreshInterval)
            : this(countRefreshInterval, EqualityComparer<TCounts>.Default)
        {
        }

        public RenderDataCache(
            int countRefreshInterval,
            IEqualityComparer<TCounts> countsComparer)
        {
            if (countRefreshInterval <= 0)
                throw new ArgumentOutOfRangeException(nameof(countRefreshInterval));
            this.countRefreshInterval = countRefreshInterval;
            this.countsComparer = countsComparer
                ?? throw new ArgumentNullException(nameof(countsComparer));
        }

        public RenderDataSnapshot<TStructure, TCounts> Get(
            TKey key,
            TRevision structureRevision,
            int tick,
            Func<TStructure> buildStructure,
            Func<TCounts> buildCounts)
        {
            if (entries.TryGetValue(key, out var existing))
            {
                if (!EqualityComparer<TRevision>.Default.Equals(
                    existing.StructureRevision, structureRevision))
                {
                    existing.Snapshot = new RenderDataSnapshot<TStructure, TCounts>(
                        buildStructure(), existing.Snapshot.Counts);
                    existing.StructureRevision = structureRevision;
                }
                if (existing.CountsDirty
                    || tick - existing.LastCountRefreshTick >= countRefreshInterval)
                {
                    var refreshedCounts = buildCounts();
                    if (!countsComparer.Equals(existing.Snapshot.Counts, refreshedCounts))
                        existing.Snapshot = new RenderDataSnapshot<TStructure, TCounts>(
                            existing.Snapshot.Structure, refreshedCounts);
                    existing.CountsDirty = false;
                    existing.LastCountRefreshTick = tick;
                }
                return existing.Snapshot;
            }

            var entry = new Entry
            {
                Snapshot = new RenderDataSnapshot<TStructure, TCounts>(
                    buildStructure(), buildCounts()),
                LastCountRefreshTick = tick,
                StructureRevision = structureRevision,
            };
            entries.Add(key, entry);
            return entry.Snapshot;
        }

        public RenderDataSnapshot<TStructure, TCounts> Get<TState>(
            TKey key,
            TRevision structureRevision,
            int tick,
            TState state,
            Func<TState, TStructure> buildStructure,
            Func<TState, TStructure, TCounts> buildCounts)
        {
            if (entries.TryGetValue(key, out var existing))
            {
                if (!EqualityComparer<TRevision>.Default.Equals(
                    existing.StructureRevision, structureRevision))
                {
                    existing.Snapshot = new RenderDataSnapshot<TStructure, TCounts>(
                        buildStructure(state), existing.Snapshot.Counts);
                    existing.StructureRevision = structureRevision;
                }
                if (existing.CountsDirty
                    || tick - existing.LastCountRefreshTick >= countRefreshInterval)
                {
                    var refreshedCounts = buildCounts(state, existing.Snapshot.Structure);
                    if (!countsComparer.Equals(existing.Snapshot.Counts, refreshedCounts))
                        existing.Snapshot = new RenderDataSnapshot<TStructure, TCounts>(
                            existing.Snapshot.Structure, refreshedCounts);
                    existing.CountsDirty = false;
                    existing.LastCountRefreshTick = tick;
                }
                return existing.Snapshot;
            }

            var structure = buildStructure(state);
            var entry = new Entry
            {
                Snapshot = new RenderDataSnapshot<TStructure, TCounts>(
                    structure, buildCounts(state, structure)),
                LastCountRefreshTick = tick,
                StructureRevision = structureRevision,
            };
            entries.Add(key, entry);
            return entry.Snapshot;
        }

        /// Forces the next Get to rebuild counts for every entry, ignoring the
        /// tick throttle. For count dependencies that are not game state — a
        /// player option that changes what the count pass gathers — which must
        /// apply immediately, including while the game is paused and the tick
        /// never advances. Structure snapshots are untouched, and an equal
        /// rebuild still preserves snapshot identity.
        public void InvalidateCounts()
        {
            foreach (var entry in entries.Values) entry.CountsDirty = true;
        }

        /// Marks only one owner key dirty when an external count dependency is
        /// partitioned more narrowly than this cache's complete key set.
        public bool InvalidateCounts(TKey key)
        {
            if (!entries.TryGetValue(key, out Entry? entry)) return false;
            entry.CountsDirty = true;
            return true;
        }

        public bool Remove(TKey key) => entries.Remove(key);

        public void Clear() => entries.Clear();
    }
}
