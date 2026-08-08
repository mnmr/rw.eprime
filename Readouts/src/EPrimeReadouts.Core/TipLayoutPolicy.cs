using System;

namespace EPrimeReadouts.Core
{
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
