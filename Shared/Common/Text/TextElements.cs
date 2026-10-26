using System;
using System.Collections.Generic;
using System.Globalization;

namespace RimShared.Common
{
    /// <summary>
    /// Allocation-free boundaries for the text handled by our captions and
    /// names, including combining marks, surrogate pairs and conjoining Hangul.
    /// This deliberately uses the same rules on Mono/net472 and modern .NET:
    /// legacy StringInfo does not join Hangul or emoji sequences. It is a
    /// conservative profile, not a complete Unicode segmentation database:
    /// marks, variation selectors, emoji modifiers and joined sequences stay
    /// together; regional indicators pair. A ZWJ always keeps its neighbors.
    /// </summary>
    public static class TextElements
    {
        /// <summary>Returns the exclusive end of the element at start.
        /// Start must be an element boundary; the end of the string is valid.</summary>
        public static int NextBoundary(string text, int start)
        {
            if (start < 0 || start > text.Length)
                throw new ArgumentOutOfRangeException(nameof(start));
            if (start == text.Length) return start;

            int previous = ScalarAt(text, start);
            int next = start + ScalarLength(previous);
            int regionalCount = IsRegionalIndicator(previous) ? 1 : 0;
            while (next < text.Length)
            {
                int current = ScalarAt(text, next);
                bool join = previous == '\r' && current == '\n';
                if (!join && (IsControl(previous) || IsControl(current))) break;
                join = join || IsExtender(text, next, current)
                    || current == 0x200D || previous == 0x200D
                    || JoinsHangul(previous, current)
                    || (IsRegionalIndicator(current) && regionalCount == 1);
                if (!join) break;
                next += ScalarLength(current);
                regionalCount = IsRegionalIndicator(current) ? regionalCount + 1 : 0;
                previous = current;
            }
            return next;
        }

        /// <summary>Limits input by complete display elements, preserving
        /// the original string reference when it already fits. Call only
        /// on changed input or in a revision-gated builder.</summary>
        public static string Prefix(string text, int maxElements)
        {
            if (maxElements < 0) throw new ArgumentOutOfRangeException(nameof(maxElements));
            if (text.Length <= maxElements) return text;
            int end = 0;
            for (int count = 0; count < maxElements && end < text.Length; count++)
                end = NextBoundary(text, end);
            return end == text.Length ? text : text.Substring(0, end);
        }

        /// <summary>Build-time index for fitting/searching by complete elements.</summary>
        public static int[] GetStarts(string text)
        {
            var starts = new List<int>();
            for (int i = 0; i < text.Length; i = NextBoundary(text, i))
                starts.Add(i);
            return starts.ToArray();
        }

        public static int ScalarAt(string text, int index) =>
            char.IsHighSurrogate(text[index]) && index + 1 < text.Length
                && char.IsLowSurrogate(text[index + 1])
                ? char.ConvertToUtf32(text[index], text[index + 1]) : text[index];

        private static int ScalarLength(int scalar) => scalar > 0xFFFF ? 2 : 1;

        private static bool IsExtender(string text, int index, int scalar)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(text, index);
            return category == UnicodeCategory.NonSpacingMark
                || category == UnicodeCategory.SpacingCombiningMark
                || category == UnicodeCategory.EnclosingMark
                || scalar == 0x200C
                || scalar == 0xFF9E || scalar == 0xFF9F // halfwidth kana voicing
                || (scalar >= 0xFE00 && scalar <= 0xFE0F)
                || (scalar >= 0x1F3FB && scalar <= 0x1F3FF)
                || (scalar >= 0xE0020 && scalar <= 0xE007F)
                || (scalar >= 0xE0100 && scalar <= 0xE01EF);
        }

        private static bool IsControl(int scalar) =>
            scalar <= 0x1F || (scalar >= 0x7F && scalar <= 0x9F)
            || scalar == 0x2028 || scalar == 0x2029;

        private static bool IsRegionalIndicator(int scalar) =>
            scalar >= 0x1F1E6 && scalar <= 0x1F1FF;

        private static bool JoinsHangul(int previous, int current)
        {
            int a = HangulKind(previous), b = HangulKind(current);
            return (a == 1 && (b == 1 || b == 2 || b == 4 || b == 5))
                || ((a == 2 || a == 4) && (b == 2 || b == 3))
                || ((a == 3 || a == 5) && b == 3);
        }

        private static int HangulKind(int scalar)
        {
            if ((scalar >= 0x1100 && scalar <= 0x115F)
                || (scalar >= 0xA960 && scalar <= 0xA97C)) return 1; // L
            if ((scalar >= 0x1160 && scalar <= 0x11A7)
                || (scalar >= 0xD7B0 && scalar <= 0xD7C6)) return 2; // V
            if ((scalar >= 0x11A8 && scalar <= 0x11FF)
                || (scalar >= 0xD7CB && scalar <= 0xD7FB)) return 3; // T
            if (scalar >= 0xAC00 && scalar <= 0xD7A3)
                return (scalar - 0xAC00) % 28 == 0 ? 4 : 5; // LV/LVT
            return 0;
        }
    }
}
