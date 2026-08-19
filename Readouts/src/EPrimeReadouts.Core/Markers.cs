using System;

namespace EPrimeReadouts.Core
{
    public enum TriangleState { Absent, Dim, Lit, HoverLit }

    /// Marker triangles: one per tier the group has (max 3), lit while that
    /// tier is visible at the current depth. Depth cycles 1..tierCount and
    /// wraps; any out-of-range stored depth means "show all tiers".
    public static class Markers
    {
        public static int ClampDepth(int tierCount, int depth)
        {
            if (tierCount <= 0) return 0;
            if (depth < 1 || depth > tierCount) return tierCount;
            return depth;
        }

        public static int NextDepth(int tierCount, int depth)
        {
            depth = ClampDepth(tierCount, depth);
            return depth >= tierCount ? 1 : depth + 1;
        }

        public static void Compute(int tierCount, int depth, TriangleState[] into)
        {
            if (into == null || into.Length < TierOps.MaxTiers)
                throw new ArgumentException("into must have at least " + TierOps.MaxTiers + " elements", nameof(into));
            depth = ClampDepth(tierCount, depth);
            for (int i = 0; i < TierOps.MaxTiers; i++)
                into[i] = StateAt(tierCount, depth, i);
        }

        public static TriangleState StateAt(
            int tierCount, int depth, int tierIndex)
        {
            depth = ClampDepth(tierCount, depth);
            return tierIndex < 0 || tierIndex >= tierCount
                ? TriangleState.Absent
                : tierIndex < depth
                    ? TriangleState.Lit
                    : TriangleState.Dim;
        }
    }
}
