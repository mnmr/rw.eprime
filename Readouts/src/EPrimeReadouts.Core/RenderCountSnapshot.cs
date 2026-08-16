using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EPrimeReadouts.Core
{
    public sealed class RenderCountSnapshot : IEquatable<RenderCountSnapshot>
    {
        private static readonly IReadOnlyDictionary<string, int> emptyCounts =
            new ReadOnlyDictionary<string, int>(new Dictionary<string, int>());
        private static readonly IReadOnlyDictionary<string, SearchCount> emptySearchCounts =
            new ReadOnlyDictionary<string, SearchCount>(new Dictionary<string, SearchCount>());
        private static readonly IReadOnlyDictionary<string, PlannedWorkDebt> emptyDebts =
            new ReadOnlyDictionary<string, PlannedWorkDebt>(
                new Dictionary<string, PlannedWorkDebt>());
        private static readonly IReadOnlyList<PlannedWorkEntry> emptyPlannedWork =
            Array.AsReadOnly(Array.Empty<PlannedWorkEntry>());

        private readonly IReadOnlyDictionary<string, int> counts;
        private readonly IReadOnlyDictionary<string, SearchCount> searchCounts;
        private readonly IReadOnlyDictionary<string, PlannedWorkDebt> debts;
        private readonly IReadOnlyList<PlannedWorkEntry> plannedWork;

        public RenderCountSnapshot(
            IReadOnlyDictionary<string, int> counts,
            long fingerprint,
            IReadOnlyDictionary<string, SearchCount> searchCounts = null,
            IReadOnlyDictionary<string, PlannedWorkDebt> debts = null,
            IReadOnlyList<PlannedWorkEntry> plannedWork = null)
        {
            if (counts == null) throw new ArgumentNullException(nameof(counts));
            this.counts = CopyCounts(counts);
            if (searchCounts == null || searchCounts.Count == 0)
                this.searchCounts = emptySearchCounts;
            else
                this.searchCounts = CopySearchCounts(searchCounts);
            if (debts == null || debts.Count == 0)
                this.debts = emptyDebts;
            else
                this.debts = CopyDebts(debts);
            if (plannedWork == null || plannedWork.Count == 0)
                this.plannedWork = emptyPlannedWork;
            else
                this.plannedWork = CopyPlannedWork(plannedWork);
            Fingerprint = fingerprint;
        }

        private RenderCountSnapshot(
            long fingerprint,
            IReadOnlyDictionary<string, int> counts,
            IReadOnlyDictionary<string, SearchCount> searchCounts,
            IReadOnlyDictionary<string, PlannedWorkDebt> debts,
            IReadOnlyList<PlannedWorkEntry> plannedWork)
        {
            this.counts = counts;
            this.searchCounts = searchCounts;
            this.debts = debts;
            this.plannedWork = plannedWork;
            Fingerprint = fingerprint;
        }

        /// Transfers buffers built exclusively by a one-shot accumulator.
        /// The accumulator seals itself before the snapshot becomes visible,
        /// so no mutable access to these dictionaries escapes publication.
        internal static RenderCountSnapshot FromOwnedBuffers(
            Dictionary<string, int> counts,
            long fingerprint,
            Dictionary<string, SearchCount> searchCounts,
            Dictionary<string, PlannedWorkDebt> debts,
            PlannedWorkEntry[] plannedWork)
        {
            if (counts == null) throw new ArgumentNullException(nameof(counts));
            return new RenderCountSnapshot(
                fingerprint,
                counts.Count == 0
                    ? emptyCounts
                    : new ReadOnlyDictionary<string, int>(counts),
                searchCounts == null || searchCounts.Count == 0
                    ? emptySearchCounts
                    : new ReadOnlyDictionary<string, SearchCount>(searchCounts),
                debts == null || debts.Count == 0
                    ? emptyDebts
                    : new ReadOnlyDictionary<string, PlannedWorkDebt>(debts),
                plannedWork == null || plannedWork.Length == 0
                    ? emptyPlannedWork
                    : Array.AsReadOnly(plannedWork));
        }

        private static IReadOnlyDictionary<string, int> CopyCounts(
            IReadOnlyDictionary<string, int> source)
        {
            if (source.Count == 0) return emptyCounts;
            var copy = new Dictionary<string, int>(source.Count);
            foreach (var pair in source) copy[pair.Key] = pair.Value;
            return new ReadOnlyDictionary<string, int>(copy);
        }

        private static IReadOnlyDictionary<string, SearchCount> CopySearchCounts(
            IReadOnlyDictionary<string, SearchCount> source)
        {
            var copy = new Dictionary<string, SearchCount>(source.Count);
            foreach (var pair in source) copy[pair.Key] = pair.Value;
            return new ReadOnlyDictionary<string, SearchCount>(copy);
        }

        private static IReadOnlyDictionary<string, PlannedWorkDebt> CopyDebts(
            IReadOnlyDictionary<string, PlannedWorkDebt> source)
        {
            var copy = new Dictionary<string, PlannedWorkDebt>(source.Count);
            foreach (var pair in source) copy[pair.Key] = pair.Value;
            return new ReadOnlyDictionary<string, PlannedWorkDebt>(copy);
        }

        private static IReadOnlyList<PlannedWorkEntry> CopyPlannedWork(
            IReadOnlyList<PlannedWorkEntry> source)
        {
            var copy = new PlannedWorkEntry[source.Count];
            for (int i = 0; i < source.Count; i++) copy[i] = source[i];
            return Array.AsReadOnly(copy);
        }

        public IReadOnlyDictionary<string, int> Counts => counts;
        /// Per-def search breakdown; defs absent here have nothing countable
        /// on the map. Never null.
        public IReadOnlyDictionary<string, SearchCount> SearchCounts => searchCounts;
        /// Per-def planned-work debt; defs absent here owe nothing. Empty when
        /// every reservation option is off. Never null.
        public IReadOnlyDictionary<string, PlannedWorkDebt> Debts => debts;
        /// Item/resource provenance for planned-work tooltip tables. Never null.
        public IReadOnlyList<PlannedWorkEntry> PlannedWork => plannedWork;
        public long Fingerprint { get; }

        /// Debt for one def, defaulting to nothing owed.
        public PlannedWorkDebt DebtOf(string defName)
            => debts.TryGetValue(defName, out PlannedWorkDebt debt)
                ? debt : default;

        public bool Equals(RenderCountSnapshot other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other == null || counts.Count != other.counts.Count
                || searchCounts.Count != other.searchCounts.Count
                || debts.Count != other.debts.Count
                || plannedWork.Count != other.plannedWork.Count)
                return false;
            foreach (var pair in counts)
                if (!other.counts.TryGetValue(pair.Key, out int value)
                    || value != pair.Value)
                    return false;
            foreach (var pair in searchCounts)
                if (!other.searchCounts.TryGetValue(pair.Key, out SearchCount value)
                    || value.Total != pair.Value.Total
                    || value.Stored != pair.Value.Stored
                    || value.Unforbidden != pair.Value.Unforbidden
                    || value.StoredUnforbidden != pair.Value.StoredUnforbidden)
                    return false;
            foreach (var pair in debts)
                if (!other.debts.TryGetValue(pair.Key, out PlannedWorkDebt value)
                    || !value.Equals(pair.Value))
                    return false;
            for (int i = 0; i < plannedWork.Count; i++)
                if (!plannedWork[i].Equals(other.plannedWork[i])) return false;
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as RenderCountSnapshot);
        public override int GetHashCode() => counts.Count;
    }
}
