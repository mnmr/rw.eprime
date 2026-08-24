namespace WorkRoles.Core
{
    /// <summary>
    /// Coalesces refreshes for a reference-owned value with one exact
    /// dependency revision. Owner identity is compared by reference.
    /// </summary>
    public sealed class OwnerRevisionGate<TOwner> where TOwner : class
    {
        private bool observed;
        private TOwner? owner;
        private int revision;

        public bool ShouldRefresh(TOwner? nextOwner, int nextRevision)
        {
            if (observed && ReferenceEquals(owner, nextOwner)
                && revision == nextRevision)
                return false;
            observed = true;
            owner = nextOwner;
            revision = nextRevision;
            return true;
        }

        public void Reset()
        {
            observed = false;
            owner = null;
            revision = 0;
        }
    }
}
