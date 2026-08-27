using System;

namespace RimShared.Common
{
    public static class SearchMatcher
    {
        /// Queries shorter than this (after trimming) do not filter or match.
        public const int MinQueryLength = 2;

        public static bool IsActive(string? query)
        {
            if (query == null) return false;
            int start = 0, end = query.Length - 1;
            while (start <= end && char.IsWhiteSpace(query[start])) start++;
            while (end > start && char.IsWhiteSpace(query[end])) end--;
            return end - start + 1 >= MinQueryLength;
        }

        public static bool Matches(string? label, string? query)
        {
            if (!IsActive(query) || string.IsNullOrEmpty(label)) return false;
            return label!.IndexOf(query!.Trim(), StringComparison.OrdinalIgnoreCase) >= 0; // Both guarded above (no NotNullWhen on netstandard2.0).
        }
    }
}
