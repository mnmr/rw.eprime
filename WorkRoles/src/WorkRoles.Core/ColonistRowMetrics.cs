using System;

namespace WorkRoles.Core
{
    /// Colonist-table row heights. A row's first pixel is its separator
    /// line, so the chip strip — centered with the odd pixel kept on top —
    /// shows equal visible padding above and below only when the row height
    /// minus the strip height is odd. Even slack is normalized: shrink one
    /// pixel while the text block still fits, otherwise grow one.
    public static class ColonistRowMetrics
    {
        /// Padding around a strip-driven row: three pixels above and below
        /// the strip plus the one-pixel separator.
        public const float StripPadding = 7f;

        public static float Height(
            float minRowHeight, float textBlockHeight, float stripHeight)
        {
            float height = Math.Max(minRowHeight,
                (float)Math.Ceiling(stripHeight + StripPadding));
            if ((int)(height - stripHeight) % 2 == 0)
                height = height - 1f >= textBlockHeight
                    ? height - 1f : height + 1f;
            return height;
        }
    }
}
