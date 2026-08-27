using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace EPrimeReadouts.Core
{
    public enum PlannedWorkKind
    {
        Bill = 0,
        Buildable = 1,
    }

    public enum PlannedWorkSource
    {
        Standard = 0,
        QualityJob = 1,
    }

    /// One aggregated work/resource/source tuple. Stable def names keep
    /// game-owned objects and translated labels out of the immutable count
    /// snapshot.
    public readonly struct PlannedWorkEntry : IEquatable<PlannedWorkEntry>
    {
        public PlannedWorkEntry(
            PlannedWorkKind kind,
            string workDefName,
            string? stuffDefName,
            string resourceDefName,
            int queued,
            int unitCost,
            int drain,
            PlannedWorkSource source = PlannedWorkSource.Standard)
        {
            Kind = kind;
            Source = source;
            WorkDefName = workDefName;
            StuffDefName = stuffDefName;
            ResourceDefName = resourceDefName;
            Queued = queued;
            UnitCost = unitCost;
            Drain = drain;
        }

        public PlannedWorkKind Kind { get; }
        public PlannedWorkSource Source { get; }
        public string WorkDefName { get; }
        public string? StuffDefName { get; }
        public string ResourceDefName { get; }
        public int Queued { get; }
        public int UnitCost { get; }
        public int Drain { get; }

        public bool Equals(PlannedWorkEntry other) =>
            Kind == other.Kind
            && Source == other.Source
            && string.Equals(WorkDefName, other.WorkDefName,
                StringComparison.Ordinal)
            && string.Equals(StuffDefName, other.StuffDefName,
                StringComparison.Ordinal)
            && string.Equals(ResourceDefName, other.ResourceDefName,
                StringComparison.Ordinal)
            && Queued == other.Queued
            && UnitCost == other.UnitCost
            && Drain == other.Drain;

        public override bool Equals(object obj) =>
            obj is PlannedWorkEntry other && Equals(other);

        public override int GetHashCode() =>
            ((int)Kind * 397)
            ^ ((int)Source * 7919)
            ^ (WorkDefName != null
                ? StringComparer.Ordinal.GetHashCode(WorkDefName) : 0)
            ^ (ResourceDefName != null
                ? StringComparer.Ordinal.GetHashCode(ResourceDefName) : 0)
            ^ UnitCost;

        internal static readonly IComparer<PlannedWorkEntry> CanonicalComparer =
            new CanonicalEntryComparer();

        private sealed class CanonicalEntryComparer : IComparer<PlannedWorkEntry>
        {
            public int Compare(PlannedWorkEntry left, PlannedWorkEntry right)
            {
                int compare = left.Kind.CompareTo(right.Kind);
                if (compare != 0) return compare;
                compare = string.Compare(left.WorkDefName, right.WorkDefName,
                    StringComparison.Ordinal);
                if (compare != 0) return compare;
                compare = string.Compare(left.StuffDefName, right.StuffDefName,
                    StringComparison.Ordinal);
                if (compare != 0) return compare;
                compare = string.Compare(left.ResourceDefName,
                    right.ResourceDefName, StringComparison.Ordinal);
                if (compare != 0) return compare;
                compare = left.UnitCost.CompareTo(right.UnitCost);
                return compare != 0 ? compare
                    : left.Source.CompareTo(right.Source);
            }
        }
    }

    /// Ranked detail rows plus the exact lump sum for anything beyond the
    /// requested row cap. Built only inside a tooltip cache miss.
    public sealed class PlannedWorkSelection
    {
        private static readonly IReadOnlyList<PlannedWorkEntry> emptyRows =
            Array.AsReadOnly(Array.Empty<PlannedWorkEntry>());
        private readonly IReadOnlyList<PlannedWorkEntry> rows;

        private PlannedWorkSelection(
            IReadOnlyList<PlannedWorkEntry> rows,
            int overflowCount,
            int overflowQueued,
            int overflowDrain,
            string? overflowResourceDefName,
            PlannedWorkSource? overflowSource)
        {
            this.rows = rows;
            OverflowCount = overflowCount;
            OverflowQueued = overflowQueued;
            OverflowDrain = overflowDrain;
            OverflowResourceDefName = overflowResourceDefName;
            OverflowSource = overflowSource;
        }

        public IReadOnlyList<PlannedWorkEntry> Rows => rows;
        public int OverflowCount { get; }
        public int OverflowQueued { get; }
        public int OverflowDrain { get; }
        /// Null means either no overflow or an overflow spanning resources.
        public string? OverflowResourceDefName { get; }
        /// Null means either no overflow or an overflow spanning sources.
        public PlannedWorkSource? OverflowSource { get; }

        public static PlannedWorkSelection ForResources(
            IReadOnlyList<PlannedWorkEntry>? entries,
            IReadOnlyList<string>? resourceDefNames,
            int maxRows)
        {
            if (maxRows <= 0) throw new ArgumentOutOfRangeException(nameof(maxRows));
            if (entries == null || entries.Count == 0
                || resourceDefNames == null || resourceDefNames.Count == 0)
                return new PlannedWorkSelection(
                    emptyRows, 0, 0, 0, null, null);

            var matches = new List<PlannedWorkEntry>();
            for (int i = 0; i < entries.Count; i++)
                if (Includes(resourceDefNames, entries[i].ResourceDefName))
                    matches.Add(entries[i]);
            if (matches.Count == 0)
                return new PlannedWorkSelection(
                    emptyRows, 0, 0, 0, null, null);

            matches.Sort(DrainComparer.Instance);
            if (matches.Count <= maxRows)
                return new PlannedWorkSelection(
                    new ReadOnlyCollection<PlannedWorkEntry>(matches),
                    0, 0, 0, null, null);

            int detailCount = maxRows - 1;
            var details = new PlannedWorkEntry[detailCount];
            for (int i = 0; i < detailCount; i++) details[i] = matches[i];

            int queued = 0;
            int drain = 0;
            string? resource = null;
            bool mixed = false;
            PlannedWorkSource? source = null;
            bool mixedSource = false;
            for (int i = detailCount; i < matches.Count; i++)
            {
                PlannedWorkEntry entry = matches[i];
                queued = SaturatingAdd(queued, entry.Queued);
                drain = SaturatingAdd(drain, entry.Drain);
                if (resource == null && !mixed) resource = entry.ResourceDefName;
                else if (!string.Equals(resource, entry.ResourceDefName,
                             StringComparison.Ordinal))
                {
                    resource = null;
                    mixed = true;
                }
                if (!source.HasValue && !mixedSource) source = entry.Source;
                else if (source.HasValue && source.Value != entry.Source)
                {
                    source = null;
                    mixedSource = true;
                }
            }
            return new PlannedWorkSelection(
                Array.AsReadOnly(details),
                matches.Count - detailCount, queued, drain, resource, source);
        }

        private static int SaturatingAdd(int left, int right)
            => left >= int.MaxValue - right
                ? int.MaxValue : left + right;

        private static bool Includes(
            IReadOnlyList<string> resources, string resourceDefName)
        {
            for (int i = 0; i < resources.Count; i++)
                if (string.Equals(resources[i], resourceDefName,
                        StringComparison.Ordinal)) return true;
            return false;
        }

        private sealed class DrainComparer : IComparer<PlannedWorkEntry>
        {
            internal static readonly DrainComparer Instance = new DrainComparer();

            public int Compare(PlannedWorkEntry left, PlannedWorkEntry right)
            {
                int compare = right.Drain.CompareTo(left.Drain);
                return compare != 0 ? compare
                    : PlannedWorkEntry.CanonicalComparer.Compare(left, right);
            }
        }
    }
}
