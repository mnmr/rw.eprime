namespace EPrimeReadouts.Core
{
    /// Displayed-count math for the storage-only and hide-forbidden options.
    /// Every consumer of a per-def count (layout engine, tooltip breakdowns)
    /// must resolve through this so displayed numbers agree everywhere.
    public static class CountBasis
    {
        public static int Displayed(
            in SearchCount search, bool storageOnly, bool hideForbidden)
        {
            if (hideForbidden)
                return storageOnly ? search.StoredUnforbidden : search.Unforbidden;
            return storageOnly ? search.Stored : search.Total;
        }
    }
}
