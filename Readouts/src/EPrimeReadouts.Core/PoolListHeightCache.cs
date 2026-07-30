using System;

namespace EPrimeReadouts.Core
{
    public sealed class PoolListHeightCache
    {
        private readonly float headerHeight;
        private readonly float captionGap;
        private readonly float rowHeight;
        private readonly int maxVisibleRows;
        private readonly float footerHeight;

        private bool hasCaptionHeight;
        private bool captionFolded;
        private float captionWidth;
        private int captionMetricRevision;
        private float captionHeight;

        private bool hasDesiredHeight;
        private int desiredPoolsVersion;
        private float desiredHeight;

        public PoolListHeightCache(
            float headerHeight,
            float captionGap,
            float rowHeight,
            int maxVisibleRows,
            float footerHeight)
        {
            this.headerHeight = headerHeight;
            this.captionGap = captionGap;
            this.rowHeight = rowHeight;
            this.maxVisibleRows = maxVisibleRows;
            this.footerHeight = footerHeight;
        }

        public float GetDesiredHeight(
            int poolsVersion,
            int metricRevision,
            int rowCount,
            bool folded,
            float availableWidth,
            Func<float, float> measureCaption)
        {
            if (!hasCaptionHeight
                || captionFolded != folded
                || captionWidth != availableWidth
                || captionMetricRevision != metricRevision)
            {
                captionHeight = folded ? 0f : measureCaption(availableWidth) + captionGap;
                captionFolded = folded;
                captionWidth = availableWidth;
                captionMetricRevision = metricRevision;
                hasCaptionHeight = true;
                hasDesiredHeight = false;
            }

            if (!hasDesiredHeight || desiredPoolsVersion != poolsVersion)
            {
                desiredHeight = headerHeight
                    + captionHeight
                    + Math.Min(rowCount, maxVisibleRows) * rowHeight
                    + footerHeight;
                desiredPoolsVersion = poolsVersion;
                hasDesiredHeight = true;
            }

            return desiredHeight;
        }
    }
}
