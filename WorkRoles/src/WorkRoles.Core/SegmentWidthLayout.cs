using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Widths for a row of single-line segments sharing a fixed space (gaps
    /// already excluded). Equal segments while every label fits one; else
    /// each label gets its desired width plus an even share of the slack;
    /// an overfull row caps only the widest labels at the largest width the
    /// rest leave free (callers truncate those labels).
    public static class SegmentWidthLayout
    {
        public static float[] Allocate(IReadOnlyList<float> desired, float available)
        {
            int count = desired.Count;
            var widths = new float[count];
            if (count == 0) return widths;
            float equal = available / count;
            float total = 0f;
            bool allFit = true;
            for (int i = 0; i < count; i++)
            {
                total += desired[i];
                allFit &= desired[i] <= equal;
            }
            if (allFit || total <= available)
            {
                for (int i = 0; i < count; i++)
                    widths[i] = allFit ? equal
                        : desired[i] + (available - total) / count;
                return widths;
            }
            // Raise the cap while labels under it free room for the others;
            // at most count passes (each adds a label under the cap).
            float cap = equal;
            for (int pass = 0; pass < count; pass++)
            {
                float fitted = 0f;
                int under = 0;
                for (int i = 0; i < count; i++)
                    if (desired[i] <= cap) { fitted += desired[i]; under++; }
                if (under == count) break;
                float raised = (available - fitted) / (count - under);
                if (raised <= cap) break;
                cap = raised;
            }
            for (int i = 0; i < count; i++)
                widths[i] = Math.Min(desired[i], cap);
            return widths;
        }
    }
}
