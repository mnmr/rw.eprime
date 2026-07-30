using System;
using System.Collections.Generic;

namespace EPrimeReadouts.Core
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
    public sealed class RenderDataCache<TKey, TRevision, TStructure, TCounts>
    {
        private sealed class Entry
        {
            public RenderDataSnapshot<TStructure, TCounts> Snapshot;
            public int LastCountRefreshTick;
            public TRevision StructureRevision;
        }

        private readonly Dictionary<TKey, Entry> entries = new Dictionary<TKey, Entry>();
        private readonly int countRefreshInterval;
        private readonly IEqualityComparer<TCounts> countsComparer;

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
                if (tick - existing.LastCountRefreshTick >= countRefreshInterval)
                {
                    var refreshedCounts = buildCounts();
                    if (!countsComparer.Equals(existing.Snapshot.Counts, refreshedCounts))
                        existing.Snapshot = new RenderDataSnapshot<TStructure, TCounts>(
                            existing.Snapshot.Structure, refreshedCounts);
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
                if (tick - existing.LastCountRefreshTick >= countRefreshInterval)
                {
                    var refreshedCounts = buildCounts(state, existing.Snapshot.Structure);
                    if (!countsComparer.Equals(existing.Snapshot.Counts, refreshedCounts))
                        existing.Snapshot = new RenderDataSnapshot<TStructure, TCounts>(
                            existing.Snapshot.Structure, refreshedCounts);
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
    }
}
