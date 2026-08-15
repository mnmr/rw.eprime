namespace EPrimeReadouts.Core
{
    /// Displayed-count math for the storage-only, hide-forbidden and
    /// planned-work options. Every consumer of a per-def count (layout engine,
    /// tooltip breakdowns) must resolve through this so displayed numbers agree
    /// everywhere.
    public static class CountBasis
    {
        public static int Displayed(
            in SearchCount search, bool storageOnly, bool hideForbidden)
            => Displayed(search, storageOnly, hideForbidden,
                debt: 0, allowNegative: false);

        /// <param name="debt">Material already owed to planned work; ignored
        /// when non-positive so a malformed value can never inflate a counter.</param>
        /// <param name="allowNegative">Let an overrun show as a negative number
        /// instead of capping the counter at zero.</param>
        public static int Displayed(
            in SearchCount search, bool storageOnly, bool hideForbidden,
            int debt, bool allowNegative)
        {
            int onHand = hideForbidden
                ? (storageOnly ? search.StoredUnforbidden : search.Unforbidden)
                : (storageOnly ? search.Stored : search.Total);
            if (debt <= 0) return onHand;
            int displayed = onHand - debt;
            return displayed < 0 && !allowNegative ? 0 : displayed;
        }
    }
}
