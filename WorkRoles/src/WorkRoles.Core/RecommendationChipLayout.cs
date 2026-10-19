using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Detached recommendation-chip geometry, built only when the owning
    /// preview's measured widths or panel dimensions change.
    public sealed class RecommendationChipLayout
    {
        private readonly float[] lefts;
        private readonly float[] tops;
        private readonly float[] chipWidths;
        private readonly float viewportHeight;

        private RecommendationChipLayout(float[] lefts, float[] tops, float[] chipWidths,
            float width, float contentHeight, float viewportHeight, bool scrollable)
        {
            this.lefts = lefts;
            this.tops = tops;
            this.chipWidths = chipWidths;
            this.viewportHeight = viewportHeight;
            ContentWidth = width;
            ContentHeight = contentHeight;
            Scrollable = scrollable;
        }

        public int ChipCount => lefts.Length;
        public float ChipLeft(int index) => lefts[index];
        public float ChipTop(int index) => tops[index];
        public float ChipWidth(int index) => chipWidths[index];
        public float ContentWidth { get; }
        public float ContentHeight { get; }
        public bool Scrollable { get; }
        public float ClampScroll(float scroll) => Math.Max(0f,
            Math.Min(scroll, Math.Max(0f, ContentHeight - viewportHeight)));

        public static RecommendationChipLayout Calculate(IReadOnlyList<float> widths,
            float width, float viewportHeight, float chipHeight, float gap, float scrollbarWidth)
        {
            var chipWidths = new float[widths.Count];
            for (int i = 0; i < widths.Count; i++) chipWidths[i] = widths[i];
            var lefts = new float[widths.Count];
            var tops = new float[widths.Count];
            float contentHeight = Flow(widths, width, chipHeight, gap, lefts, tops);
            bool scrollable = contentHeight > viewportHeight;
            if (scrollable)
            {
                width = Math.Max(0f, width - scrollbarWidth);
                for (int i = 0; i < chipWidths.Length; i++)
                    chipWidths[i] = Math.Min(chipWidths[i], width);
                contentHeight = Flow(chipWidths, width, chipHeight, gap, lefts, tops);
            }
            return new RecommendationChipLayout(lefts, tops, chipWidths, width,
                contentHeight, Math.Max(0f, viewportHeight), scrollable);
        }

        private static float Flow(IReadOnlyList<float> widths, float width,
            float chipHeight, float gap, float[] lefts, float[] tops)
        {
            float x = 0f, y = 0f;
            for (int i = 0; i < widths.Count; i++)
            {
                if (x + widths[i] > width && x > 0f)
                {
                    x = 0f;
                    y += chipHeight + gap;
                }
                lefts[i] = x;
                tops[i] = y;
                x += widths[i] + gap;
            }
            return widths.Count == 0 ? 0f : y + chipHeight;
        }
    }
}
