using System;

namespace RimShared.Common
{
    /// <summary>
    /// Single-line truncation that keeps the END of the text. Captions such
    /// as "overrides Bionic leg" carry their meaning at the end, so a slot
    /// too narrow for the whole text shows "...ionic leg" rather than the
    /// useless "overrides Bio...". Pure: the caller supplies the width
    /// measurement, which runs O(log n) times per call, so this belongs in a
    /// revision-gated cache builder, never in a steady draw pass.
    /// </summary>
    public static class TailTruncation
    {
        public const string Ellipsis = "...";

        /// <summary>
        /// Returns <paramref name="text"/> unchanged when it fits
        /// <paramref name="maxWidth"/>; otherwise the ellipsis followed by
        /// the longest tail that fits. A tail never starts with whitespace.
        /// A slot too narrow even for one character still yields the
        /// ellipsis plus the last character (best effort; the caller
        /// decides how to clip). <paramref name="width"/> receives the
        /// measured width of the returned text.
        /// </summary>
        public static string Fit(string text, float maxWidth,
            Func<string, float> measure, out float width)
        {
            width = measure(text);
            if (width <= maxWidth || text.Length <= 1) return text;

            // Binary search over the tail length: widths are monotone in
            // the tail length (trimming a leading space only shrinks).
            int low = 1;
            int high = text.Length - 1;
            string best = Tail(text, 1);
            float bestWidth = measure(best);
            if (bestWidth > maxWidth)
            {
                width = bestWidth;
                return best;
            }
            while (low < high)
            {
                int mid = low + (high - low + 1) / 2;
                string candidate = Tail(text, mid);
                float candidateWidth = measure(candidate);
                if (candidateWidth <= maxWidth)
                {
                    best = candidate;
                    bestWidth = candidateWidth;
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }
            width = bestWidth;
            return best;
        }

        private static string Tail(string text, int length) =>
            Ellipsis + text.Substring(text.Length - length).TrimStart();
    }
}
