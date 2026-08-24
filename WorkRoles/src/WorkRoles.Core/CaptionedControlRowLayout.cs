using System;

namespace WorkRoles.Core
{
    public readonly struct CaptionedControlRowLayout
    {
        private CaptionedControlRowLayout(float captionVisualHeight,
            float captionAdvance, float rowHeight)
        {
            CaptionVisualHeight = captionVisualHeight;
            CaptionAdvance = captionAdvance;
            RowHeight = rowHeight;
        }

        public float CaptionVisualHeight { get; }
        public float CaptionAdvance { get; }
        public float RowHeight { get; }

        public static CaptionedControlRowLayout Calculate(
            float captionLineHeight, float captionAdvance,
            float controlHeight, float captionGap)
        {
            float visualHeight = Math.Max(captionAdvance,
                (float)Math.Ceiling(captionLineHeight));
            return new CaptionedControlRowLayout(visualHeight,
                captionAdvance, captionAdvance + captionGap + controlHeight);
        }
    }
}
