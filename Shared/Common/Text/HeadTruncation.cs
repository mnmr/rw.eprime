using System;

namespace RimShared.Common
{
    /// Single-line caption fitting that never splits a text element or draws
    /// outside the supplied width. Call only from a revision-gated builder.
    public static class HeadTruncation
    {
        public static string Fit(string text, float maxWidth,
            Func<string, float> measure, out float width)
        {
            width = measure(text);
            if (width <= maxWidth) return text;
            string best = TailTruncation.Ellipsis;
            width = measure(best);
            if (width > maxWidth) { width = 0f; return ""; }
            int[] starts = TextElements.GetStarts(text);
            int low = 1, high = starts.Length - 1;
            while (low <= high)
            {
                int count = low + (high - low) / 2;
                string candidate = text.Substring(0, starts[count]).TrimEnd() + TailTruncation.Ellipsis;
                float candidateWidth = measure(candidate);
                if (candidateWidth <= maxWidth)
                {
                    best = candidate; width = candidateWidth; low = count + 1;
                }
                else high = count - 1;
            }
            return best;
        }

        public static string FitWithSubstitution(string format, string value, float maxWidth,
            Func<string, float> measure, out float width)
        {
            string full = string.Format(format, value);
            width = measure(full);
            if (width <= maxWidth) return full;
            // The identifying value takes priority over the surrounding words.
            // Keeping the verb plus one letter ("replaces B...") wastes a slot
            // that could show "Bionic leg". This is independent of whether the
            // language puts the verb before or after the value. The caller's
            // tooltip retains the complete relation and value.
            return Fit(value, maxWidth, measure, out width);
        }
    }
}
