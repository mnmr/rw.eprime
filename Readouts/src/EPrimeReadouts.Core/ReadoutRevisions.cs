using System;

namespace EPrimeReadouts.Core
{
    [Flags]
    public enum ReadoutChange
    {
        None = 0,
        Groups = 1,
        Pools = 2,
        Thresholds = 4,
        All = Groups | Pools | Thresholds,
    }

    /// <summary>
    /// Monotonic cache stamps split by the model domain that changed.
    /// Consumers observe only the domains their cached data depends on.
    /// </summary>
    public sealed class ReadoutRevisions
    {
        public int Version { get; private set; }
        public int Groups { get; private set; }
        public int Pools { get; private set; }
        public int Thresholds { get; private set; }

        public void Bump(ReadoutChange change)
        {
            if (change == ReadoutChange.None) return;

            Version++;
            if ((change & ReadoutChange.Groups) != 0) Groups++;
            if ((change & ReadoutChange.Pools) != 0) Pools++;
            if ((change & ReadoutChange.Thresholds) != 0) Thresholds++;
        }
    }
}
