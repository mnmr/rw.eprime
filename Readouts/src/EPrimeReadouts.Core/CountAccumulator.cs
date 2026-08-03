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
        private readonly Dictionary<string, int> counts =
            new Dictionary<string, int>();
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

        public RenderCountSnapshot ToSnapshot() =>
            new RenderCountSnapshot(counts, fingerprint);
    }
}
