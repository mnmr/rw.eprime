using System;
using System.Collections.Generic;

namespace RimShared.Common
{
    /// Caches one value per key until the caller-provided revision changes.
    /// Cache contract: Owner = caller; Key = <typeparamref name="TKey"/>;
    /// Value = caller-owned immutable <typeparamref name="TValue"/>;
    /// Dependencies = <typeparamref name="TRevision"/> and builder state;
    /// Refresh policy = immediate on revision mismatch; Equality policy = a
    /// matching revision preserves value identity; Teardown = Remove or Clear.
    public sealed class RevisionedCache<TKey, TRevision, TValue>
        where TKey : notnull
    {
        private sealed class Entry
        {
            public TRevision Revision = default!; // Every construction site assigns it.
            public TValue Value = default!; // Every construction site assigns it.
        }

        private readonly Dictionary<TKey, Entry> entries = new Dictionary<TKey, Entry>();
        private readonly IEqualityComparer<TRevision> revisionComparer;

        public int Count => entries.Count;

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

        public bool Remove(TKey key) => entries.Remove(key);

        public void Clear() => entries.Clear();
    }
}
