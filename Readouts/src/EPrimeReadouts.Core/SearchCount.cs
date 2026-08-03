namespace EPrimeReadouts.Core
{
    /// Per-def stack breakdown backing the search-result filters. Total spans
    /// every counted stack on the map (stored or scattered); the remaining
    /// fields carve out the subsets the per-player search options narrow to.
    /// Group counters keep the vanilla storage-only basis and never read this.
    public readonly struct SearchCount
    {
        public readonly int Total;
        public readonly int Stored;
        public readonly int Unforbidden;
        public readonly int StoredUnforbidden;

        public SearchCount(int total, int stored, int unforbidden, int storedUnforbidden)
        {
            Total = total;
            Stored = stored;
            Unforbidden = unforbidden;
            StoredUnforbidden = storedUnforbidden;
        }
    }
}
