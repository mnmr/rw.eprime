using System;

namespace WorkRoles.Core
{
    /// <summary>
    /// Height budget for a priority grid whose measured header stays fixed while
    /// its uniformly sized colonist rows scroll within the remaining space.
    /// </summary>
    public readonly struct PriorityGridHeightLayout
    {
        private PriorityGridHeightLayout(
            float rowsContentHeight,
            float rowsPanelHeight,
            float windowHeight)
        {
            RowsContentHeight = rowsContentHeight;
            RowsPanelHeight = rowsPanelHeight;
            WindowHeight = windowHeight;
        }

        public float RowsContentHeight { get; }
        public float RowsPanelHeight { get; }
        public float WindowHeight { get; }

        public static PriorityGridHeightLayout Calculate(
            int rowCount,
            float rowHeight,
            float fixedContentHeight,
            float windowMarginsHeight,
            float scrollChromeHeight,
            float maxWindowHeight)
        {
            if (rowCount < 0) throw new ArgumentOutOfRangeException(nameof(rowCount));
            RequirePositiveFinite(rowHeight, nameof(rowHeight));
            RequireNonnegativeFinite(fixedContentHeight, nameof(fixedContentHeight));
            RequireNonnegativeFinite(windowMarginsHeight, nameof(windowMarginsHeight));
            RequireNonnegativeFinite(scrollChromeHeight, nameof(scrollChromeHeight));
            RequireNonnegativeFinite(maxWindowHeight, nameof(maxWindowHeight));

            float rowsContentHeight = rowCount * rowHeight;
            if (float.IsInfinity(rowsContentHeight))
                throw new ArgumentOutOfRangeException(nameof(rowCount));

            float availableRowsPanelHeight = maxWindowHeight
                - windowMarginsHeight
                - fixedContentHeight;
            if (availableRowsPanelHeight < scrollChromeHeight)
                throw new ArgumentOutOfRangeException(nameof(maxWindowHeight));

            float desiredRowsPanelHeight = rowsContentHeight + scrollChromeHeight;
            if (float.IsInfinity(desiredRowsPanelHeight))
                throw new ArgumentOutOfRangeException(nameof(rowCount));
            float rowsPanelHeight = Math.Min(
                desiredRowsPanelHeight,
                availableRowsPanelHeight);
            float windowHeight = windowMarginsHeight
                + fixedContentHeight
                + rowsPanelHeight;

            return new PriorityGridHeightLayout(
                rowsContentHeight,
                rowsPanelHeight,
                windowHeight);
        }

        private static void RequirePositiveFinite(float value, string parameterName)
        {
            if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }

        private static void RequireNonnegativeFinite(float value, string parameterName)
        {
            if (value < 0f || float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
