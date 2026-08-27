using System;
using System.Collections.Generic;

namespace RimShared.Common
{
    /// <summary>
    /// Retains snapshots for an explicitly supplied owner cohort and rebuilds
    /// only owners whose invalidation revision changed. Reads never inspect the
    /// live source.
    /// </summary>
    public sealed class SelectiveSnapshotCache<TOwner, TSnapshot>
        where TOwner : class
    {
        private readonly Dictionary<TOwner, TSnapshot> snapshots;
        private readonly Dictionary<TOwner, int> observedOwnerRevisions;
        private readonly HashSet<TOwner> cohort;
        private readonly List<TOwner> removalBuffer = new List<TOwner>();
        private readonly Func<TOwner, TSnapshot> build;
        private int observedRevision = int.MinValue;
        private int observedFullGeneration = int.MinValue;

        public SelectiveSnapshotCache(Func<TOwner, TSnapshot> build,
            IEqualityComparer<TOwner>? comparer = null)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            comparer = comparer ?? EqualityComparer<TOwner>.Default;
            snapshots = new Dictionary<TOwner, TSnapshot>(comparer);
            observedOwnerRevisions = new Dictionary<TOwner, int>(comparer);
            cohort = new HashSet<TOwner>(comparer);
        }

        public int Count => snapshots.Count;
        public IEnumerable<TOwner> Owners => snapshots.Keys;

        public bool NeedsRefresh(OwnerInvalidationRevisions<TOwner> revisions)
        {
            if (revisions == null) throw new ArgumentNullException(nameof(revisions));
            return observedRevision != revisions.Current;
        }

        public bool Refresh(IEnumerable<TOwner> owners,
            OwnerInvalidationRevisions<TOwner> revisions)
        {
            if (revisions == null) throw new ArgumentNullException(nameof(revisions));
            if (!NeedsRefresh(revisions)) return false;

            bool full = observedFullGeneration != revisions.FullGeneration;

            cohort.Clear();
            if (owners != null)
                foreach (TOwner owner in owners)
                {
                    if (owner == null || !cohort.Add(owner)) continue;
                    int ownerRevision = revisions.RevisionOf(owner);
                    if (full || !snapshots.ContainsKey(owner)
                        || !observedOwnerRevisions.TryGetValue(owner,
                            out int observedOwnerRevision)
                        || observedOwnerRevision != ownerRevision)
                    {
                        TSnapshot rebuilt = build(owner);
                        if (!snapshots.TryGetValue(owner, out TSnapshot? published)
                            || !EqualityComparer<TSnapshot>.Default.Equals(
                                published, rebuilt))
                            snapshots[owner] = rebuilt;
                        observedOwnerRevisions[owner] = ownerRevision;
                    }
                }

            removalBuffer.Clear();
            foreach (TOwner owner in snapshots.Keys)
                if (!cohort.Contains(owner))
                    removalBuffer.Add(owner);
            for (int i = 0; i < removalBuffer.Count; i++)
            {
                TOwner owner = removalBuffer[i];
                snapshots.Remove(owner);
                observedOwnerRevisions.Remove(owner);
            }
            removalBuffer.Clear();
            cohort.Clear();

            observedRevision = revisions.Current;
            observedFullGeneration = revisions.FullGeneration;
            return true;
        }

        public bool Contains(TOwner owner) =>
            owner != null && snapshots.ContainsKey(owner);

        public TSnapshot Get(TOwner owner) => snapshots[owner];

        public bool TryGet(TOwner owner, out TSnapshot? snapshot)
        {
            if (owner != null) return snapshots.TryGetValue(owner, out snapshot);
            snapshot = default;
            return false;
        }

        public void Clear()
        {
            snapshots.Clear();
            observedOwnerRevisions.Clear();
            cohort.Clear();
            removalBuffer.Clear();
            observedRevision = int.MinValue;
            observedFullGeneration = int.MinValue;
        }
    }
}
