using System;

namespace EPrimeReadouts.Core
{
    public sealed class PoolListHeightCache
    {
        // Cache contract: Owner = caller-supplied world/store identity; Key =
        // owner, pools version, row count, fold state, width and metric revision;
        // Value = desired panel height; Dependencies = all key fields plus the
        // fixed constructor metrics; Refresh policy = immediate; Equality policy
        // = exact key reuse; Teardown = Reset.
        private readonly float headerHeight;
        private readonly float captionGap;
        private readonly float rowHeight;
        private readonly int maxVisibleRows;
        private readonly float footerHeight;

        private bool hasCaptionHeight;
        private object captionOwner;
        private bool captionFolded;
        private string captionText;
        private float captionWidth;
        private int captionMetricRevision;
        private float captionHeight;

        private bool hasDesiredHeight;
        private object desiredOwner;
        private int desiredPoolsVersion;
        private int desiredRowCount;
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
            object owner,
            int poolsVersion,
            int metricRevision,
            int rowCount,
            bool folded,
            float availableWidth,
            string caption,
            Func<string, float, float> measureCaption)
        {
            if (!hasCaptionHeight
                || !ReferenceEquals(captionOwner, owner)
                || !string.Equals(captionText, caption, StringComparison.Ordinal)
                || captionFolded != folded
                || captionWidth != availableWidth
                || captionMetricRevision != metricRevision)
            {
                captionHeight = folded ? 0f : measureCaption(caption, availableWidth) + captionGap;
                captionOwner = owner;
                captionText = caption;
                captionFolded = folded;
                captionWidth = availableWidth;
                captionMetricRevision = metricRevision;
                hasCaptionHeight = true;
                hasDesiredHeight = false;
            }

            if (!hasDesiredHeight
                || !ReferenceEquals(desiredOwner, owner)
                || desiredPoolsVersion != poolsVersion
                || desiredRowCount != rowCount)
            {
                desiredHeight = headerHeight
                    + captionHeight
                    + Math.Min(rowCount, maxVisibleRows) * rowHeight
                    + footerHeight;
                desiredPoolsVersion = poolsVersion;
                desiredOwner = owner;
                desiredRowCount = rowCount;
                hasDesiredHeight = true;
            }

            return desiredHeight;
        }

        public void Reset()
        {
            hasCaptionHeight = false;
            captionOwner = null;
            hasDesiredHeight = false;
            desiredOwner = null;
        }
    }
}
