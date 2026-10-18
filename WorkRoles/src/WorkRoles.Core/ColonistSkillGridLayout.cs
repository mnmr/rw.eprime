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

        private ColonistSkillGridLayout(int skillCount, float columnWidth)
        {
            ColumnCount = skillCount > 12 ? 3 : 2;
            RowCount = (skillCount + ColumnCount - 1) / ColumnCount;
            // Keep the familiar six-row panel footprint when extra skills use
            // a third column. Larger skill packs may still grow downwards.
            if (skillCount > 12) RowCount = Math.Max(6, RowCount);
            ColumnWidth = columnWidth;
            float advance = columnWidth + 2f * SeparatorMargin + SeparatorWidth;
            RecommendationLeft = ColumnCount * advance;
            ContentHeight = Math.Max(118f, RowCount * CellHeight);
            cellLefts = new float[skillCount];
            cellTops = new float[skillCount];
            for (int i = 0; i < skillCount; i++)
            {
                cellLefts[i] = (i % ColumnCount) * advance;
                cellTops[i] = (i / ColumnCount) * CellHeight;
            }
            separatorLefts = new float[ColumnCount];
            for (int i = 0; i < ColumnCount; i++)
                separatorLefts[i] = i * advance + columnWidth + SeparatorMargin;
        }

        public int ColumnCount { get; }
        public int RowCount { get; }
        public float ColumnWidth { get; }
        public float ContentHeight { get; }
        public float RecommendationLeft { get; }
        public float CellLeft(int index) => cellLefts[index];
        public float CellTop(int index) => cellTops[index];
        public float SeparatorLeft(int index) => separatorLefts[index];

        public static ColonistSkillGridLayout Calculate(int skillCount,
            float columnWidth) => new ColonistSkillGridLayout(skillCount, columnWidth);
    }
}
