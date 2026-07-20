using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    public static class CatalogNameRules
    {
        public static bool IsAvailable<T>(string candidate, IEnumerable<T> items,
            Func<T, string> nameOf, T self = null) where T : class
        {
            string normalized = candidate?.Trim();
            if (string.IsNullOrEmpty(normalized) || items == null || nameOf == null) return false;
            foreach (T item in items)
            {
                if (item == null || ReferenceEquals(item, self)) continue;
                if (string.Equals(nameOf(item)?.Trim(), normalized,
                        StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        public static string Unique<T>(string preferred, IEnumerable<T> items,
            Func<T, string> nameOf) where T : class
        {
            string root = preferred?.Trim();
            if (string.IsNullOrEmpty(root)) return null;
            if (items == null || nameOf == null || IsAvailable(root, items, nameOf)) return root;

            for (int suffix = 2; ; suffix++)
            {
                string candidate = $"{root} ({suffix})";
                if (IsAvailable(candidate, items, nameOf)) return candidate;
            }
        }
    }

    public static class GroupNameRules
    {
        public const string DefaultName = "Default";

        public static bool IsDefault(string candidate) => string.Equals(
            candidate?.Trim(), DefaultName, StringComparison.OrdinalIgnoreCase);

        public static bool IsAvailable<T>(string candidate, IEnumerable<T> groups,
            Func<T, string> nameOf, T self = null) where T : class =>
            !IsDefault(candidate) && CatalogNameRules.IsAvailable(candidate, groups, nameOf, self);
    }
}
