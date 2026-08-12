namespace WorkRoles.Core
{
    /// Age-band selection for role age gates. Band edges mirror the game's
    /// work unlock ages (3/7/10/13) plus the 13-17/18+ split for teen work
    /// penalties. A contiguous selection [lo..hi] maps to (minAge, maxAge)
    /// years where 0 means no gate on that end.
    public static class AgeBands
    {
        public static readonly int[] Starts = { 3, 7, 10, 13, 18 };
        /// Inclusive band tops; 0 = open (the last band has no cap).
        public static readonly int[] Ends = { 6, 9, 12, 17, 0 };
        public const int Count = 5;

        /// Selection shown for stored gates: a band is selected when it
        /// overlaps [minAge, maxAge] (0 = unbounded on that end). Overlap of
        /// an interval with ordered bands is always contiguous.
        public static (int Lo, int Hi) SelectionFor(int minAge, int maxAge)
        {
            int lo = -1;
            int hi = -1;
            for (int band = 0; band < Count; band++)
            {
                bool aboveMin = Ends[band] == 0 || Ends[band] >= minAge;
                bool belowMax = maxAge == 0 || Starts[band] <= maxAge;
                if (!aboveMin || !belowMax) continue;
                if (lo < 0) lo = band;
                hi = band;
            }
            // Contradictory gates (min past max) select nothing; show the
            // full range rather than an impossible empty selection.
            return lo < 0 ? (0, Count - 1) : (lo, hi);
        }

        /// Canonical stored gates for a selection; the full range stores
        /// (0, 0) = no gates.
        public static (int MinAge, int MaxAge) StoredFor(int lo, int hi)
        {
            return (lo == 0 ? 0 : Starts[lo], hi == Count - 1 ? 0 : Ends[hi]);
        }

        /// True when nobody in the band can do any of the role's work: even
        /// the band's oldest age is below the earliest unlock age.
        public static bool BandLacksJobs(int band, int minUnlockAge)
        {
            return Ends[band] != 0 && Ends[band] < minUnlockAge;
        }

        /// One click on band index `clicked` against selection [lo..hi]:
        /// an unselected band extends the range to reach it, a selected end
        /// band is trimmed off, a selected interior band collapses the range
        /// to itself, and the only selected band is a no-op.
        public static (int Lo, int Hi) Click(int lo, int hi, int clicked)
        {
            if (clicked < lo) return (clicked, hi);
            if (clicked > hi) return (lo, clicked);
            if (lo == hi) return (lo, hi);
            if (clicked == lo) return (lo + 1, hi);
            if (clicked == hi) return (lo, hi - 1);
            return (clicked, clicked);
        }
    }
}
