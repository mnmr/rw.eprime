using System;
using System.Globalization;

namespace RimShared.Common
{
    public static class SearchMatcher
    {
        /// Queries shorter than this (after trimming) do not filter or match:
        /// a single character already searches (owner, 2026-09-07); only
        /// whitespace is inactive.
        public const int MinQueryLength = 1;

        public static bool IsActive(string? query)
        {
            if (query == null) return false;
            int start = 0, end = query.Length - 1;
            while (start <= end && char.IsWhiteSpace(query[start])) start++;
            while (end > start && char.IsWhiteSpace(query[end])) end--;
            return end - start + 1 >= MinQueryLength;
        }

        public static bool Matches(string? label, string? query, CultureInfo? culture = null)
        {
            if (!IsActive(query) || string.IsNullOrEmpty(label)) return false;
            // Display language is explicit; never inherit the operating system's
            // culture, and keep the established ordinal behavior for callers
            // without a language-specific casing rule.
            string needle = query!.Trim();
            // Unity's Mono collation tables do not reliably implement Turkish
            // IgnoreCase. Apply its two I pairs explicitly, comparing all other
            // runs ordinally. No normalized string copies or OS culture needed.
            if (culture?.TwoLetterISOLanguageName == "tr")
            {
                for (int start = 0; start <= label!.Length - needle.Length; start++)
                    if (TurkishMatchAt(label, needle, start)) return true;
                return false;
            }
            return culture == null || culture.Equals(CultureInfo.InvariantCulture)
                ? label!.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                : culture.CompareInfo.IndexOf(label!, needle, CompareOptions.IgnoreCase) >= 0;
        }

        private static bool TurkishMatchAt(string label, string needle, int start)
        {
            int offset = 0;
            while (offset < needle.Length)
            {
                char left = label[start + offset], right = needle[offset];
                if (IsTurkishI(left) || IsTurkishI(right))
                {
                    if (FoldTurkishI(left) != FoldTurkishI(right)) return false;
                    offset++;
                }
                else
                {
                    int end = offset + 1;
                    while (end < needle.Length && !IsTurkishI(label[start + end])
                        && !IsTurkishI(needle[end])) end++;
                    if (string.Compare(label, start + offset, needle, offset,
                        end - offset, StringComparison.OrdinalIgnoreCase) != 0) return false;
                    offset = end;
                }
            }
            return true;
        }

        private static bool IsTurkishI(char c) => c == 'I' || c == 'ı' || c == 'İ' || c == 'i';
        private static char FoldTurkishI(char c) => c == 'I' ? 'ı' : c == 'İ' ? 'i' : c;
    }
}
