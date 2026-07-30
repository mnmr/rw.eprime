using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// <summary>
    /// Owner-keyed snapshots whose read path is deliberately separate from
    /// publishing a derived projection into mutable external state.
    /// </summary>
    public sealed class ExplicitProjectionCache<TOwner, TSnapshot>
        where TOwner : class
    {
        private readonly ManagedSnapshotCache<TOwner, TSnapshot> snapshots;
        private readonly Func<TOwner, TSnapshot> build;
        private readonly Action<TOwner, TSnapshot> publish;

        public ExplicitProjectionCache(
            Func<TOwner, TSnapshot> build,
            Action<TOwner, TSnapshot> publish,
            IEqualityComparer<TOwner> comparer = null)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            this.publish = publish ?? throw new ArgumentNullException(nameof(publish));
            snapshots = new ManagedSnapshotCache<TOwner, TSnapshot>(comparer);
        }

        public bool TryGetManaged(TOwner owner,
            Func<TOwner, bool> isManaged,
            out TSnapshot snapshot) =>
            snapshots.TryGetManaged(owner, isManaged, build, out snapshot);

        public TSnapshot GetOrBuild(TOwner owner) =>
            snapshots.GetOrBuild(owner, build);

        public void PublishFresh(TOwner owner)
        {
            snapshots.Remove(owner);
            TSnapshot snapshot = snapshots.GetOrBuild(owner, build);
            publish(owner, snapshot);
        }

        public bool Remove(TOwner owner) => snapshots.Remove(owner);

        public void Clear() => snapshots.Clear();
    }
}
