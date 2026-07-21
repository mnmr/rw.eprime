using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// <summary>
    /// Owner-keyed values frozen until an explicit lifecycle or mutation event
    /// clears them. Reading never observes, hashes, or compares the live source.
    /// </summary>
    public sealed class ExplicitSnapshotCache<TKey, TSnapshot>
        where TKey : class
    {
        private readonly Dictionary<TKey, TSnapshot> snapshots =
            new Dictionary<TKey, TSnapshot>();
        private readonly Func<TKey, TSnapshot> build;

        public ExplicitSnapshotCache(Func<TKey, TSnapshot> build)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
        }

        public TSnapshot Get(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (!snapshots.TryGetValue(key, out TSnapshot snapshot))
            {
                snapshot = build(key);
                snapshots.Add(key, snapshot);
            }
            return snapshot;
        }

        public void Invalidate(TKey key)
        {
            if (key != null) snapshots.Remove(key);
        }

        public void Clear() => snapshots.Clear();
    }
}
