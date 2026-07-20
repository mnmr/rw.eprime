using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// <summary>Pure ownership and assignment validation rules for the UI clipboard.</summary>
    public static class ClipboardRules
    {
        public static List<TResult> SnapshotForOwner<TOwner, TSource, TResult>(
            TOwner storedOwner,
            TOwner currentOwner,
            IEnumerable<TSource> source,
            Func<TSource, TResult> snapshot)
            where TOwner : class
        {
            var result = new List<TResult>();
            if (storedOwner == null || !ReferenceEquals(storedOwner, currentOwner) || source == null)
                return result;
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            foreach (var item in source)
                result.Add(snapshot(item));
            return result;
        }

        public static List<TResult> FilterValidDistinct<TSource, TResult>(
            IEnumerable<TSource> source,
            Func<TSource, int?> roleId,
            IEnumerable<int> validRoleIds,
            Func<TSource, TResult> snapshot)
        {
            var result = new List<TResult>();
            if (source == null) return result;
            if (roleId == null) throw new ArgumentNullException(nameof(roleId));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var valid = validRoleIds == null
                ? new HashSet<int>()
                : new HashSet<int>(validRoleIds);
            var seen = new HashSet<int>();
            foreach (var item in source)
            {
                int? id = roleId(item);
                if (!id.HasValue || !valid.Contains(id.Value) || !seen.Add(id.Value))
                    continue;
                result.Add(snapshot(item));
            }
            return result;
        }
    }
}
