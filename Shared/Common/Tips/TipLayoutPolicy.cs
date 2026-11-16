using System;

namespace RimShared.Common
{
    public enum TipColumnAlignment
    {
        Left = 0,
        Right = 1,
    }

    public static class TipTableLayout
    {
        public static float TextX(
            float columnX,
            float columnWidth,
            float textWidth,
            TipColumnAlignment alignment) =>
            alignment == TipColumnAlignment.Right
                ? TipLayoutPolicy.RightAlignedTextX(
                    columnX, columnWidth, textWidth)
                : columnX;

        /// Column widths for a table that must fit <paramref name="available"/>
        /// (the room for column text, gaps and insets already removed). A
        /// table that fits keeps its natural widths (same array). Otherwise
        /// the widest columns share one cap, so narrow columns keep their
        /// width and only the long ones wrap. No column goes below
        /// <paramref name="minWidth"/>, even when that overflows.
        public static float[] FitColumns(
            float[] natural, float available, float minWidth)
        {
            float total = 0f;
            for (int i = 0; i < natural.Length; i++) total += natural[i];
            if (total <= available) return natural;

            // Columns at or under the cap keep their width and leave their
            // unused share to the rest, until the cap stops rising.
            float cap = 0f;
            float room = available;
            int sharing = natural.Length;
            bool settled = false;
            var kept = new bool[natural.Length];
            while (!settled && sharing > 0)
            {
                settled = true;
                cap = room / sharing;
                for (int i = 0; i < natural.Length; i++)
                    if (!kept[i] && natural[i] <= cap)
                    {
                        kept[i] = true;
                        room -= natural[i];
                        sharing--;
                        settled = false;
                    }
            }
            cap = Math.Max(minWidth, (float)Math.Floor(cap));
            var fitted = new float[natural.Length];
            for (int i = 0; i < natural.Length; i++)
                fitted[i] = Math.Min(natural[i], cap);
            return fitted;
        }
    }

    public readonly struct TipColumnLayout
    {
        internal TipColumnLayout(float labelWidth, float valueWidth, float gap)
        {
            LabelWidth = labelWidth;
            ValueWidth = valueWidth;
            ValueX = labelWidth + gap;
            TotalWidth = ValueX + valueWidth;
        }

        public float LabelWidth { get; }
        public float ValueWidth { get; }
        public float ValueX { get; }
        public float TotalWidth { get; }
    }

    /// Column/header policy for planned-work tables. Pool rows retain the
    /// resource-specific stock fields; single-resource rows move a differing
    /// stock value into the title line instead of repeating it per work item.
    public readonly struct PlannedWorkTipLayout
    {
        private PlannedWorkTipLayout(
            bool showResourceColumn,
            bool showInStockColumn,
            bool showStockInHeader)
        {
            ShowResourceColumn = showResourceColumn;
            ShowInStockColumn = showInStockColumn;
            ShowStockInHeader = showStockInHeader;
        }

        public bool ShowResourceColumn { get; }
        public bool ShowInStockColumn { get; }
        public bool ShowStockInHeader { get; }
        public int ColumnCount => 4
            + (ShowResourceColumn ? 1 : 0)
            + (ShowInStockColumn ? 1 : 0);

        public static PlannedWorkTipLayout For(
            bool pooled, int available, int inStock) =>
            new PlannedWorkTipLayout(
                showResourceColumn: pooled,
                showInStockColumn: pooled,
                showStockInHeader: !pooled && available != inStock);
    }

    /// Pure tooltip sizing policy: constrain content independently, then add
    /// the symmetric frame after content layout is complete.
    public static class TipLayoutPolicy
    {
        public static float ContentWidth(float naturalWidth, float maxContentWidth) =>
            Math.Min(naturalWidth, maxContentWidth);

        public static float FramedExtent(float contentExtent, float frame) =>
            contentExtent + frame * 2f;

        public static TipColumnLayout SharedColumns(
            float headerLabelWidth,
            float headerValueWidth,
            float rowLabelWidth,
            float rowValueWidth,
            float gap) =>
            new TipColumnLayout(
                Math.Max(headerLabelWidth, rowLabelWidth),
                Math.Max(headerValueWidth, rowValueWidth),
                gap);

        public static float RightAlignedTextX(
            float columnX, float columnWidth, float textWidth) =>
            columnX + Math.Max(0f, columnWidth - textWidth);
    }
}
