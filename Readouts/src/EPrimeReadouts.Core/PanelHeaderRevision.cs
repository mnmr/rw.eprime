using System;

namespace EPrimeReadouts.Core
{
    /// Complete dependency key for the buffered header-row pixels (gear,
    /// search field or title). Field-wise equality with no string
    /// concatenation, so the per-event steady-state comparison never
    /// allocates.
    public readonly struct PanelHeaderRevision : IEquatable<PanelHeaderRevision>
    {
        public PanelHeaderRevision(
            bool showSearch,
            bool showTitle,
            string searchText,
            string title,
            float titleWidth,
            float headerWidth,
            int headerHeight,
            int uiRevision,
            float rasterScale)
        {
            ShowSearch = showSearch;
            ShowTitle = showTitle;
            SearchText = searchText ?? "";
            Title = title ?? "";
            TitleWidth = titleWidth;
            HeaderWidth = headerWidth;
            HeaderHeight = headerHeight;
            UiRevision = uiRevision;
            RasterScale = rasterScale;
        }

        public bool ShowSearch { get; }
        public bool ShowTitle { get; }
        public string SearchText { get; }
        public string Title { get; }
        public float TitleWidth { get; }
        public float HeaderWidth { get; }
        public int HeaderHeight { get; }
        public int UiRevision { get; }
        public float RasterScale { get; }

        public bool Equals(PanelHeaderRevision other) =>
            ShowSearch == other.ShowSearch
            && ShowTitle == other.ShowTitle
            && string.Equals(SearchText, other.SearchText, StringComparison.Ordinal)
            && string.Equals(Title, other.Title, StringComparison.Ordinal)
            && TitleWidth == other.TitleWidth
            && HeaderWidth == other.HeaderWidth
            && HeaderHeight == other.HeaderHeight
            && UiRevision == other.UiRevision
            && RasterScale == other.RasterScale;

        public override bool Equals(object obj) =>
            obj is PanelHeaderRevision other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ShowSearch ? 1 : 0;
                hash = hash * 397 ^ (ShowTitle ? 1 : 0);
                hash = hash * 397 ^ (SearchText != null
                    ? StringComparer.Ordinal.GetHashCode(SearchText) : 0);
                hash = hash * 397 ^ (Title != null
                    ? StringComparer.Ordinal.GetHashCode(Title) : 0);
                hash = hash * 397 ^ TitleWidth.GetHashCode();
                hash = hash * 397 ^ HeaderWidth.GetHashCode();
                hash = hash * 397 ^ HeaderHeight;
                hash = hash * 397 ^ UiRevision;
                return hash * 397 ^ RasterScale.GetHashCode();
            }
        }
    }
}
