using System;
using System.Collections.Generic;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core
{
    public readonly struct PawnPresentationStamp : IEquatable<PawnPresentationStamp>
    {
        public PawnPresentationStamp(object? store, object externalSnapshot,
            SkillPromotionTuning tuning, int language, int definition, int metrics)
        {
            Store = store;
            ExternalSnapshot = externalSnapshot;
            Tuning = tuning;
            Language = language;
            Definition = definition;
            Metrics = metrics;
        }

        public object? Store { get; }
        public object ExternalSnapshot { get; }
        public SkillPromotionTuning Tuning { get; }
        public int Language { get; }
        public int Definition { get; }
        public int Metrics { get; }

        public bool Equals(PawnPresentationStamp other) =>
            ReferenceEquals(Store, other.Store)
            && ReferenceEquals(ExternalSnapshot, other.ExternalSnapshot)
            && Tuning.Equals(other.Tuning) && Language == other.Language
            && Definition == other.Definition && Metrics == other.Metrics;
    }

    /// Owner: caller's window, partitioned by store identity. Key: pawn identity.
    /// Value: immutable per-pawn presentation. Dependencies: that pawn's external
    /// snapshot, store, tuning, language, definitions, and UI metrics; role and
    /// assignment revisions are deliberately absent. Refresh: first read after
    /// a changed dependency. Equality: equal rebuilt content retains identity
    /// within a store. Teardown: Remove for cohort eviction; Clear on close/reset.
    public sealed class PawnPresentationCache<TPawn, TSnapshot>
        where TPawn : class
        where TSnapshot : class
    {
        private readonly struct Entry
        {
            internal Entry(PawnPresentationStamp stamp, TSnapshot snapshot)
            {
                Stamp = stamp;
                Snapshot = snapshot;
            }

            internal PawnPresentationStamp Stamp { get; }
            internal TSnapshot Snapshot { get; }
        }

        private readonly Func<TPawn, TSnapshot?, TSnapshot> build;
        private readonly Func<TSnapshot, TSnapshot, bool> contentEquals;
        private readonly Dictionary<TPawn, Entry> entries;
        private object? store;

        public PawnPresentationCache(Func<TPawn, TSnapshot?, TSnapshot> build,
            Func<TSnapshot, TSnapshot, bool> contentEquals,
            IEqualityComparer<TPawn> comparer)
        {
            this.build = build ?? throw new ArgumentNullException(nameof(build));
            this.contentEquals = contentEquals
                ?? throw new ArgumentNullException(nameof(contentEquals));
            entries = new Dictionary<TPawn, Entry>(comparer);
        }

        public TSnapshot Get(TPawn pawn, PawnPresentationStamp stamp)
        {
            if (!ReferenceEquals(store, stamp.Store))
            {
                entries.Clear();
                store = stamp.Store;
            }
            bool hasPrevious = entries.TryGetValue(pawn, out Entry previous);
            if (hasPrevious && previous.Stamp.Equals(stamp))
                return previous.Snapshot;
            TSnapshot next = build(pawn, hasPrevious ? previous.Snapshot : null);
            if (hasPrevious && contentEquals(previous.Snapshot, next))
                next = previous.Snapshot;
            entries[pawn] = new Entry(stamp, next);
            return next;
        }

        public void Remove(TPawn pawn) => entries.Remove(pawn);

        public void Clear()
        {
            entries.Clear();
            store = null;
        }
    }
}
