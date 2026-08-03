using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    /// Sums per-def count contributions from one or more sources (e.g. the
    /// level maps of a multi-floor stack) into a single RenderCountSnapshot.
    /// The fingerprint folds contributions commutatively, so it is stable for
    /// identical contribution sets regardless of source order; snapshot
    /// equality remains content-based either way.
    public sealed class CountAccumulator
    {
        private struct SearchTally
        {
            public int Total;
            public int Stored;
            public int Unforbidden;
            public int StoredUnforbidden;
        }

        private readonly Dictionary<string, int> counts =
            new Dictionary<string, int>();
        private readonly Dictionary<string, SearchTally> searchTallies =
            new Dictionary<string, SearchTally>();
        private long fingerprint = 17;

        public void Add(string defName, int defHash, int count)
        {
            counts.TryGetValue(defName, out int have);
            counts[defName] = have + count;
            unchecked
            {
                fingerprint += (long)defHash * 31 + count;
            }
        }

        /// Registers a def at zero without disturbing an existing total, so
        /// extra counted defs always appear in the snapshot.
        public void AddZero(string defName, int defHash)
        {
            if (!counts.ContainsKey(defName))
                counts[defName] = 0;
            unchecked
            {
                fingerprint += defHash;
            }
        }

        /// Records one stack for the search-count breakdown. Stored and
        /// forbidden describe the stack itself; the per-def totals fall out of
        /// summing every stack's contribution.
        public void AddSearch(string defName, int defHash, int count,
            bool stored, bool forbidden)
        {
            searchTallies.TryGetValue(defName, out SearchTally tally);
            tally.Total += count;
            if (stored) tally.Stored += count;
            if (!forbidden)
            {
                tally.Unforbidden += count;
                if (stored) tally.StoredUnforbidden += count;
            }
            searchTallies[defName] = tally;
            unchecked
            {
                // Distinct weights per stack disposition keep the fingerprint
                // sensitive to stored/forbidden shifts at unchanged totals.
                fingerprint += (long)defHash * 131
                    + count * (stored ? 3 : 5)
                    + (forbidden ? 7 : 11);
            }
        }

        public RenderCountSnapshot ToSnapshot()
        {
            var search = new Dictionary<string, SearchCount>(searchTallies.Count);
            foreach (var pair in searchTallies)
                search[pair.Key] = new SearchCount(pair.Value.Total,
                    pair.Value.Stored, pair.Value.Unforbidden,
                    pair.Value.StoredUnforbidden);
            return new RenderCountSnapshot(counts, fingerprint, search);
        }
    }
}
