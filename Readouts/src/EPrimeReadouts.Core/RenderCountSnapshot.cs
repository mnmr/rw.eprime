using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EPrimeReadouts.Core
{
    public sealed class RenderCountSnapshot : IEquatable<RenderCountSnapshot>
    {
        private static readonly IReadOnlyDictionary<string, SearchCount> emptySearchCounts =
            new ReadOnlyDictionary<string, SearchCount>(new Dictionary<string, SearchCount>());

        private readonly IReadOnlyDictionary<string, int> counts;
        private readonly IReadOnlyDictionary<string, SearchCount> searchCounts;

        public RenderCountSnapshot(
            IReadOnlyDictionary<string, int> counts,
            long fingerprint,
            IReadOnlyDictionary<string, SearchCount> searchCounts = null)
        {
            if (counts == null) throw new ArgumentNullException(nameof(counts));
            var copy = new Dictionary<string, int>(counts.Count);
            foreach (var pair in counts) copy[pair.Key] = pair.Value;
            this.counts = new ReadOnlyDictionary<string, int>(copy);
            if (searchCounts == null || searchCounts.Count == 0)
                this.searchCounts = emptySearchCounts;
            else
            {
                var searchCopy = new Dictionary<string, SearchCount>(searchCounts.Count);
                foreach (var pair in searchCounts) searchCopy[pair.Key] = pair.Value;
                this.searchCounts = new ReadOnlyDictionary<string, SearchCount>(searchCopy);
            }
            Fingerprint = fingerprint;
        }

        public IReadOnlyDictionary<string, int> Counts => counts;
        /// Per-def search breakdown; defs absent here have nothing countable
        /// on the map. Never null.
        public IReadOnlyDictionary<string, SearchCount> SearchCounts => searchCounts;
        public long Fingerprint { get; }

        public bool Equals(RenderCountSnapshot other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other == null || counts.Count != other.counts.Count
                || searchCounts.Count != other.searchCounts.Count)
                return false;
            foreach (var pair in counts)
                if (!other.counts.TryGetValue(pair.Key, out int value)
                    || value != pair.Value)
                    return false;
            foreach (var pair in searchCounts)
                if (!other.searchCounts.TryGetValue(pair.Key, out SearchCount value)
                    || value.Total != pair.Value.Total
                    || value.Stored != pair.Value.Stored
                    || value.Unforbidden != pair.Value.Unforbidden
                    || value.StoredUnforbidden != pair.Value.StoredUnforbidden)
                    return false;
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as RenderCountSnapshot);
        public override int GetHashCode() => counts.Count;
    }
}
