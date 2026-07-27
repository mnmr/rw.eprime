using System.Collections.Generic;
using Verse;

namespace EPrimeReadouts
{
    /// Snapshots and fingerprints the vanilla per-map resource counter.
    public static class GameCounts
    {
        /// Cheap change probe: order-sensitive hash over the counter dict.
        /// Enumeration order is stable because ResourceCounter rebuilds the
        /// dict from its statically ordered resource list on every recount,
        /// so identical contents always produce identical fingerprints.
        public static long Fingerprint(Map map)
        {
            long fp = 17;
            foreach (var pair in map.resourceCounter.AllCountedAmounts)
            {
                fp = fp * 31 + pair.Key.shortHash;
                fp = fp * 31 + pair.Value;
            }
            return fp;
        }

        /// defName-keyed snapshot for the Core layout engine. Includes
        /// resourceReadoutAlwaysShow defs at their (possibly zero) counts,
        /// matching what vanilla surfaces.
        public static Dictionary<string, int> Snapshot(Map map)
        {
            var counts = new Dictionary<string, int>();
            foreach (var pair in map.resourceCounter.AllCountedAmounts)
                counts[pair.Key.defName] = pair.Value;
            return counts;
        }
    }
}
