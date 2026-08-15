using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// <summary>
    /// Publishes one global observation revision, one full-generation revision,
    /// and owner-local revisions for selectively refreshed snapshots.
    /// </summary>
    public sealed class OwnerInvalidationRevisions<TOwner> where TOwner : class
    {
        private readonly Dictionary<TOwner, int> ownerRevisions;

        public OwnerInvalidationRevisions(IEqualityComparer<TOwner> comparer = null)
        {
            ownerRevisions = new Dictionary<TOwner, int>(
                comparer ?? EqualityComparer<TOwner>.Default);
        }

        public int Current { get; private set; }
        public int FullGeneration { get; private set; }

        public int RevisionOf(TOwner owner)
        {
            if (owner != null && ownerRevisions.TryGetValue(owner, out int revision))
                return revision;
            return FullGeneration;
        }

        public void Invalidate(TOwner owner)
        {
            if (owner == null) return;
            Advance();
            ownerRevisions[owner] = Current;
        }

        public void InvalidateAll()
        {
            Advance();
            FullGeneration = Current;
            ownerRevisions.Clear();
        }

        public void Release(TOwner owner)
        {
            if (owner == null || !ownerRevisions.Remove(owner)) return;
            Advance();
        }

        private void Advance()
        {
            unchecked { Current++; }
        }
    }
}
