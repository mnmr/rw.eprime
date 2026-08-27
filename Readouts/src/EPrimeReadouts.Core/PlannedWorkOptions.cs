using System;

namespace EPrimeReadouts.Core
{
    /// The per-player choices that change what a count snapshot contains.
    ///
    /// Deliberately excludes "show overruns as negative numbers": that option
    /// only changes how an already-computed debt is rendered, so folding it in
    /// here would rebuild the snapshot for a pure display change.
    public readonly struct PlannedWorkOptions : IEquatable<PlannedWorkOptions>
    {
        /// Subtract the ingredients outstanding bill iterations will consume.
        public readonly bool ReserveBills;
        /// Subtract materials undelivered blueprints and frames still need.
        public readonly bool ReserveBuildables;
        /// Scale reservations by the expected rework a quality target implies.
        /// Inert on its own — it multiplies whichever reservations are enabled.
        public readonly bool QualityRework;

        public PlannedWorkOptions(
            bool reserveBills, bool reserveBuildables, bool qualityRework)
        {
            ReserveBills = reserveBills;
            ReserveBuildables = reserveBuildables;
            QualityRework = qualityRework;
        }

        /// True when a snapshot build must scan bills or buildables at all.
        public bool Any => ReserveBills || ReserveBuildables;

        public bool Equals(PlannedWorkOptions other)
            => ReserveBills == other.ReserveBills
               && ReserveBuildables == other.ReserveBuildables
               && QualityRework == other.QualityRework;

        public override bool Equals(object obj)
            => obj is PlannedWorkOptions other && Equals(other);

        public override int GetHashCode()
            => (ReserveBills ? 1 : 0)
               | (ReserveBuildables ? 2 : 0)
               | (QualityRework ? 4 : 0);
    }
}
