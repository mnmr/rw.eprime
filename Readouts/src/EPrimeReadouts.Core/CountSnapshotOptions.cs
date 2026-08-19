using System;

namespace EPrimeReadouts.Core
{
    /// Per-player inputs that determine which game state a count snapshot must
    /// collect. Pure presentation choices stay outside this value.
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
