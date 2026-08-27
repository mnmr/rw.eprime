using System;

namespace EPrimeReadouts.Core
{
    /// <summary>
    /// Whole-pixel line-box geometry for text requested as Tiny. RimWorld may
    /// resolve that request to Small; callers still allocate the measured line
    /// height and may shift caption ink down by two pixels to overlap unused
    /// Small-font leading with the following control.
    /// </summary>
    public readonly struct ResolvedTinyTextMetrics
    {
        public ResolvedTinyTextMetrics(float measuredLineHeight,
            bool substitutedSmall)
        {
            LineHeight = (float)Math.Ceiling(Math.Max(0f, measuredLineHeight));
            CaptionOffsetY = substitutedSmall ? 2f : 0f;
        }

        public float LineHeight { get; }

        public float CaptionOffsetY { get; }

        public float MinHeight(float requestedHeight) =>
            requestedHeight > LineHeight ? requestedHeight : LineHeight;
    }
}
