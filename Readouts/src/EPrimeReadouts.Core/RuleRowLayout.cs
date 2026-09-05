using System;

namespace EPrimeReadouts.Core
{
    /// One count-rule row in the slot options: a label on the left and a
    /// three-segment selector on the right. Wide rows keep the selector at
    /// its preferred width. A tighter row narrows the selector towards its
    /// minimum (every caption still fits with less padding), and a row too
    /// narrow for both moves the selector onto its own line under the label
    /// so the two never overlap.
    public readonly struct RuleRowLayout
    {
        public readonly float SelectorWidth;
        public readonly bool Stacked;

        private RuleRowLayout(float selectorWidth, bool stacked)
        {
            SelectorWidth = selectorWidth;
            Stacked = stacked;
        }

        public static RuleRowLayout Solve(float rowWidth, float labelWidth,
            float preferredSelectorWidth, float minSelectorWidth, float gap)
        {
            float beside = rowWidth - labelWidth - gap;
            if (beside >= preferredSelectorWidth)
                return new RuleRowLayout(preferredSelectorWidth, stacked: false);
            if (beside >= minSelectorWidth)
                return new RuleRowLayout(beside, stacked: false);
            return new RuleRowLayout(
                Math.Min(preferredSelectorWidth, rowWidth), stacked: true);
        }
    }
}
