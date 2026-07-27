using System;

namespace EPrimeReadouts.Core
{
    public static class SearchMatcher
    {
        public static bool IsActive(string query) => !string.IsNullOrWhiteSpace(query);

        public static bool Matches(string label, string query)
        {
            if (!IsActive(query) || string.IsNullOrEmpty(label)) return false;
            return label.IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
