using System;

namespace EPrimeReadouts.Core
{
    /// Effective collection inputs that determine which game state a count
    /// snapshot must collect: the per-player count options, widened by the
    /// union of stored count rules so overridden tokens always find their
    /// data. Pure presentation choices stay outside this value.
    public readonly struct CountSnapshotOptions
        : IEquatable<CountSnapshotOptions>
    {
        public CountSnapshotOptions(
            bool storageOnly,
            bool hideForbidden,
            PlannedWorkOptions plannedWork)
        {
            StorageOnly = storageOnly;
            HideForbidden = hideForbidden;
            PlannedWork = plannedWork;
        }

        public readonly bool StorageOnly;
        public readonly bool HideForbidden;
        public readonly PlannedWorkOptions PlannedWork;

        public bool IncludeScattered => !StorageOnly;
        public bool InspectForbidden => HideForbidden;

        public bool Equals(CountSnapshotOptions other)
            => StorageOnly == other.StorageOnly
               && HideForbidden == other.HideForbidden
               && PlannedWork.Equals(other.PlannedWork);

        public override bool Equals(object obj)
            => obj is CountSnapshotOptions other && Equals(other);

        public override int GetHashCode()
            => (StorageOnly ? 1 : 0)
               | (HideForbidden ? 2 : 0)
               | (PlannedWork.GetHashCode() << 2);
    }
}
