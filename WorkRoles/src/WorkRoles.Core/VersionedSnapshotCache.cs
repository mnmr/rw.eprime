using System;

namespace WorkRoles.Core
{
    /// <summary>
    /// Explicit owner-keyed snapshots with a revision that consumers can use
    /// to invalidate projections derived from the complete cache domain.
    /// </summary>
    public sealed class VersionedSnapshotCache<TOwner, TSnapshot>
        where TOwner : class
    {
        private readonly ExplicitSnapshotCache<TOwner, TSnapshot> snapshots;

        public VersionedSnapshotCache(Func<TOwner, TSnapshot> build)
        {
            snapshots = new ExplicitSnapshotCache<TOwner, TSnapshot>(build);
        }

        public int Revision { get; private set; }

        public TSnapshot Get(TOwner owner) => snapshots.Get(owner);

        public void Invalidate(TOwner owner)
        {
            if (owner == null) return;
            snapshots.Invalidate(owner);
            Advance();
        }

        public void Clear()
        {
            snapshots.Clear();
            Advance();
        }

        private void Advance()
        {
            unchecked { Revision++; }
        }
    }
}
