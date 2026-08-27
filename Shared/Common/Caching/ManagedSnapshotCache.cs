using System;
using System.Collections.Generic;

namespace RimShared.Common
{
    /// <summary>
    /// Owner-keyed snapshots whose steady-state hit is also proof that the
    /// owner is managed. Ownership is queried only after a cache miss.
    /// </summary>
    public sealed class ManagedSnapshotCache<TOwner, TSnapshot>
        where TOwner : class
    {
        private readonly Dictionary<TOwner, TSnapshot> snapshots;

        public ManagedSnapshotCache(IEqualityComparer<TOwner>? comparer = null)
        {
            snapshots = new Dictionary<TOwner, TSnapshot>(
                comparer ?? EqualityComparer<TOwner>.Default);
        }

        public int Count => snapshots.Count;

        public bool TryGetManaged(TOwner owner,
            Func<TOwner, bool> isManaged,
            Func<TOwner, TSnapshot> build,
            out TSnapshot? snapshot)
        {
            if (isManaged == null) throw new ArgumentNullException(nameof(isManaged));
            if (build == null) throw new ArgumentNullException(nameof(build));
            if (owner != null && snapshots.TryGetValue(owner, out snapshot))
                return true;
            if (owner == null || !isManaged(owner))
            {
                snapshot = default;
                return false;
            }
            snapshot = build(owner);
            snapshots.Add(owner, snapshot);
            return true;
        }

        public TSnapshot GetOrBuild(TOwner owner, Func<TOwner, TSnapshot> build)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (build == null) throw new ArgumentNullException(nameof(build));
            if (!snapshots.TryGetValue(owner, out TSnapshot? snapshot))
            {
                snapshot = build(owner);
                snapshots.Add(owner, snapshot);
            }
            return snapshot;
        }

        public bool Remove(TOwner owner) =>
            owner != null && snapshots.Remove(owner);

        public void Clear() => snapshots.Clear();
    }
}
