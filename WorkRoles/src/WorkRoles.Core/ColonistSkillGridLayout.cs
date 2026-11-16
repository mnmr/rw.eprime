using System;

namespace WorkRoles.Core
{
    /// Immutable geometry published with the selected colonist's skill snapshot.
    public sealed class ColonistSkillGridLayout
    {
        private const float SeparatorMargin = 12f;
        private const float SeparatorWidth = 2f;
        public const float CellHeight = 20f;

        private readonly float[] cellLefts;
        private readonly float[] cellTops;
        private readonly float[] separatorLefts;

        private ColonistSkillGridLayout(int skillCount, float columnWidth,
            int compactColumns)
        {
            bool compact = compactColumns > 0;
            Compact = compact;
            ColumnCount = compact ? compactColumns : skillCount > 12 ? 3 : 2;
            RowCount = (skillCount + ColumnCount - 1) / ColumnCount;
            // Keep the familiar six-row panel footprint when extra skills use
            // a third column. Larger skill packs may still grow downwards.
            if (compact) RowCount = Math.Max(CompactRows, RowCount);
            else if (skillCount > 12) RowCount = Math.Max(6, RowCount);
            ColumnWidth = columnWidth;
            float advance = columnWidth + 2f * SeparatorMargin + SeparatorWidth;
            // The compact grid has no recommendation section after it: a
            // separator leads every column and the last column ends the grid.
            float lead = compact ? SeparatorWidth + SeparatorMargin : 0f;
            RecommendationLeft = compact
                ? ColumnCount * advance - SeparatorMargin : ColumnCount * advance;
            ContentHeight = compact
                ? RowCount * CellHeight : Math.Max(118f, RowCount * CellHeight);
            cellLefts = new float[skillCount];
            cellTops = new float[skillCount];
            for (int i = 0; i < skillCount; i++)
            {
                cellLefts[i] = lead + (i % ColumnCount) * advance;
                cellTops[i] = (i / ColumnCount) * CellHeight;
            }
            separatorLefts = new float[ColumnCount];
            for (int i = 0; i < ColumnCount; i++)
                separatorLefts[i] = compact
                    ? i * advance : i * advance + columnWidth + SeparatorMargin;
        }

        /// Rows and columns of the compact grid for the vanilla twelve
        /// skills.
        public const int CompactRows = 4;
        public const int CompactColumns = 3;
        /// Columns led by separators, as tall as its rows only.
        public bool Compact { get; }

        public int ColumnCount { get; }
        public int RowCount { get; }
        public float ColumnWidth { get; }
        public float ContentHeight { get; }
        public float RecommendationLeft { get; }
        public float CellLeft(int index) => cellLefts[index];
        public float CellTop(int index) => cellTops[index];
        public float SeparatorLeft(int index) => separatorLefts[index];

        public static ColonistSkillGridLayout Calculate(int skillCount,
            float columnWidth) =>
            new ColonistSkillGridLayout(skillCount, columnWidth, 0);

        /// Compact grid: more skills than three columns of four hold add
        /// columns while they fit availableWidth, and only then rows.
        /// Width of the smallest compact grid (CompactColumns columns); the
        /// blocks left of the grid may only use room beyond it.
        public static float MinimumCompactWidth(float columnWidth) =>
            CompactColumns * (columnWidth + 2f * SeparatorMargin + SeparatorWidth)
                - SeparatorMargin;

        public static ColonistSkillGridLayout CalculateCompact(int skillCount,
            float columnWidth, float availableWidth)
        {
            float advance = columnWidth + 2f * SeparatorMargin + SeparatorWidth;
            int wanted = Math.Max(CompactColumns,
                (skillCount + CompactRows - 1) / CompactRows);
            int columns = wanted;
            // The last column needs no trailing margin.
            if (availableWidth < wanted * advance - SeparatorMargin)
                columns = Math.Max(CompactColumns, (int)Math.Floor(
                    (availableWidth + SeparatorMargin) / advance));
            return new ColonistSkillGridLayout(skillCount, columnWidth, columns);
        }
    }
}
