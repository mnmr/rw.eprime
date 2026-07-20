namespace WorkRoles.Core
{
    /// <summary>
    /// Coalesces cache refreshes by two independently-owned integer revisions.
    /// Multiple changes before the next observation still require one refresh.
    /// </summary>
    public sealed class RevisionPairGate
    {
        private bool observed;
        private int first;
        private int second;

        public bool ShouldRefresh(int firstRevision, int secondRevision)
        {
            if (observed && first == firstRevision && second == secondRevision)
                return false;
            observed = true;
            first = firstRevision;
            second = secondRevision;
            return true;
        }
    }
}
