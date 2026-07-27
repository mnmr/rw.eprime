using System;
using System.Collections.Generic;

namespace EPrimeReadouts.Core
{
    /// <summary>
    /// Depth-cycling helpers for the editor band. Geometry is now produced by
    /// ReadoutLayoutEngine in editor mode; this class only exposes the shared
    /// depth arithmetic that both the engine and the editor UI need.
    /// </summary>
    public static class EditorBand
    {
        /// <summary>
        /// The deepest depth the editor can cycle to: min(3, nonEmptyTierCount + 1),
        /// floor 1. So 0 tiers → 1, 1 tier → 2, 2 tiers → 3, 3 tiers → 3.
        /// </summary>
        public static int MaxDepth(List<List<string>> tiers)
        {
            int n = tiers == null ? 0 : tiers.Count;
            return Math.Min(3, n + 1);
        }

        /// <summary>
        /// Clamps depth to a valid current-tier index (1-based). Out-of-range
        /// (less than 1 or greater than MaxDepth) returns 1 — the default
        /// current tier. Valid 1..MaxDepth is kept as-is.
        /// </summary>
        public static int ClampDepth(List<List<string>> tiers, int depth)
        {
            int md = MaxDepth(tiers);
            if (depth < 1 || depth > md)
                return 1;
            return depth;
        }

    }
}
