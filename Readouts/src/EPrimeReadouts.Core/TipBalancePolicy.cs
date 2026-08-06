using System;

namespace EPrimeReadouts.Core
{
    /// Pure shape policy for tooltip content: a tip whose measured layout is
    /// much wider than it is tall narrows toward a √-area target, so short
    /// prose wraps into a few lines instead of one very wide band. The policy
    /// only ever narrows — a tip never renders wider than its natural width —
    /// and never narrows below the caller-supplied floor (the widest element
    /// that cannot wrap) or MinWidth.
    public static class TipBalancePolicy
    {
        /// Aspect coefficient: target width = Aspect · √(width·height). At 2,
        /// a 500px-wide two-line tip rebalances to ~300px over four lines.
        public const float Aspect = 2f;

        /// Content width below which no tip is narrowed further.
        public const float MinWidth = 280f;

        public static float BalancedWidth(float width, float height, float floor)
        {
            if (width <= 0f || height <= 0f) return width;
            float target = (float)Math.Ceiling(Aspect * Math.Sqrt(width * height));
            if (target < MinWidth) target = MinWidth;
            if (target < floor) target = floor;
            if (target > width) target = width;
            return target;
        }
    }
}
