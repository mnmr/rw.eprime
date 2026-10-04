using System;
using System.Collections.Generic;

namespace RimShared.Common
{
    /// <summary>
    /// Owner-keyed snapshots whose read path is deliberately separate from
    /// publishing a derived projection into mutable external state. A steady
    /// managed hit is also proof that the owner is managed: ownership is
    /// queried only after a cache miss.
    /// </summary>
    public sealed class ExplicitProjectionCache<TOwner, TSnapshot>
        where TOwner : class
    {
        private readonly Dictionary<TOwner, TSnapshot> snapshots;
        private readonly Func<TOwner, TSnapshot> build;
        private readonly Action<TOwner, TSnapshot> publish;

        public ExplicitProjectionCache(
            Func<TOwner, TSnapshot> build,
            Action<TOwner, TSnapshot> publish,
            IEqualityComparer<TOwner>? comparer = null)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            this.publish = publish ?? throw new ArgumentNullException(nameof(publish));
            snapshots = new Dictionary<TOwner, TSnapshot>(
                comparer ?? EqualityComparer<TOwner>.Default);
        }

        public bool TryGetManaged(TOwner owner,
            Func<TOwner, bool> isManaged,
            out TSnapshot? snapshot)
        {
            if (isManaged == null) throw new ArgumentNullException(nameof(isManaged));
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

        public TSnapshot GetOrBuild(TOwner owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (!snapshots.TryGetValue(owner, out TSnapshot? snapshot))
            {
                snapshot = build(owner);
                snapshots.Add(owner, snapshot);
            }
            return snapshot;
        }

        public void PublishFresh(TOwner owner)
        {
            Remove(owner);
            publish(owner, GetOrBuild(owner));
        }

        public bool Remove(TOwner owner) =>
            owner != null && snapshots.Remove(owner);

        public void Clear() => snapshots.Clear();
    }
}
