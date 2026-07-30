using System;
using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    /// Caches one value per key until the caller-provided revision changes.
    public sealed class RevisionedCache<TKey, TRevision, TValue>
    {
        private sealed class Entry
        {
            public TRevision Revision;
            public TValue Value;
        }

        private readonly Dictionary<TKey, Entry> entries = new Dictionary<TKey, Entry>();
        private readonly IEqualityComparer<TRevision> revisionComparer;

        public RevisionedCache()
            : this(EqualityComparer<TRevision>.Default)
        {
        }

        public RevisionedCache(IEqualityComparer<TRevision> revisionComparer)
        {
            this.revisionComparer = revisionComparer
                ?? throw new ArgumentNullException(nameof(revisionComparer));
        }

        public TValue Get<TState>(
            TKey key,
            TRevision revision,
            TState state,
            Func<TState, TValue> build)
        {
            if (entries.TryGetValue(key, out var cached)
                && revisionComparer.Equals(cached.Revision, revision))
                return cached.Value;

            var value = build(state);
            entries[key] = new Entry { Revision = revision, Value = value };
            return value;
        }
    }
}
