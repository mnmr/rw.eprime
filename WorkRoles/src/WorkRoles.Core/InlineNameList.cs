using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Plans a one-line list of names with a translated remaining-count caption.
    /// Widths are measured by the caller's revision-gated presentation builder.
    public static class InlineNameList
    {
        public static int VisibleCount(float availableWidth, IReadOnlyList<float> nameWidths,
            float separatorWidth, IReadOnlyList<float> overflowWidths)
        {
            if (nameWidths.Count <= 1) return nameWidths.Count;
            float used = 0f;
            int visible = 0;
            for (int i = 0; i < nameWidths.Count; i++)
            {
                if (i > 0) used += separatorWidth;
                used += nameWidths[i];
                int remaining = nameWidths.Count - i - 1;
                float required = used + (remaining > 0
                    ? separatorWidth + overflowWidths[remaining] : 0f);
                if (required <= availableWidth) visible = i + 1;
            }
            return visible;
        }
    }
}
