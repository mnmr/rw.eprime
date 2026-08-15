using System;
using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    /// Sums per-def count contributions from one or more sources (e.g. the
    /// level maps of a multi-floor stack) into a single RenderCountSnapshot.
    /// The fingerprint folds contributions commutatively, so it is stable for
    /// identical contribution sets regardless of source order; snapshot
    /// equality remains content-based either way. Publication is one-shot so
    /// the owned dictionaries can transfer into the immutable snapshot.
    public sealed class CountAccumulator
    {
        private struct SearchTally
        {
            public int Total;
            public int Stored;
            public int Unforbidden;
            public int StoredUnforbidden;
        }

        private struct DebtTally
        {
            public int Bills;
            public int Buildables;
        }

        private readonly Dictionary<string, int> counts =
            new Dictionary<string, int>();
        private readonly Dictionary<string, SearchTally> searchTallies =
            new Dictionary<string, SearchTally>();
        private readonly Dictionary<string, DebtTally> debtTallies =
            new Dictionary<string, DebtTally>();
        private long fingerprint = 17;
        private bool published;

        public void Add(string defName, int defHash, int count)
        {
            EnsureWritable();
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
            EnsureWritable();
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
            EnsureWritable();
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

        /// Records ingredients an outstanding bill iteration will consume.
        /// Non-positive amounts are ignored so a def with nothing owed never
        /// enters the debt map.
        public void AddBillDebt(string defName, int defHash, int amount)
        {
            EnsureWritable();
            if (amount <= 0) return;
            debtTallies.TryGetValue(defName, out DebtTally tally);
            tally.Bills += amount;
            debtTallies[defName] = tally;
            unchecked
            {
                fingerprint += (long)defHash * 8191 + amount * 13;
            }
        }

        /// Records materials an undelivered blueprint or part-built frame still
        /// needs. Weighted differently from bill debt so an equal amount moving
        /// between the two buckets still changes the fingerprint.
        public void AddBuildableDebt(string defName, int defHash, int amount)
        {
            EnsureWritable();
            if (amount <= 0) return;
            debtTallies.TryGetValue(defName, out DebtTally tally);
            tally.Buildables += amount;
            debtTallies[defName] = tally;
            unchecked
            {
                fingerprint += (long)defHash * 8191 + amount * 17;
            }
        }

        public RenderCountSnapshot ToSnapshot()
        {
            EnsureWritable();
            Dictionary<string, SearchCount> search = null;
            if (searchTallies.Count != 0)
            {
                search = new Dictionary<string, SearchCount>(searchTallies.Count);
                foreach (var pair in searchTallies)
                    search[pair.Key] = new SearchCount(pair.Value.Total,
                        pair.Value.Stored, pair.Value.Unforbidden,
                        pair.Value.StoredUnforbidden);
            }
            Dictionary<string, PlannedWorkDebt> debts = null;
            if (debtTallies.Count != 0)
            {
                debts = new Dictionary<string, PlannedWorkDebt>(debtTallies.Count);
                foreach (var pair in debtTallies)
                    debts[pair.Key] = new PlannedWorkDebt(
                        pair.Value.Bills, pair.Value.Buildables);
            }

            RenderCountSnapshot snapshot = RenderCountSnapshot.FromOwnedBuffers(
                counts, fingerprint, search, debts);
            published = true;
            return snapshot;
        }

        private void EnsureWritable()
        {
            if (published)
                throw new InvalidOperationException(
                    "A published count accumulator cannot be reused.");
        }
    }
}
