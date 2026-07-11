using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Coverage math shared by seeding and Restore Roles: which work types the
    /// catalog reaches, and which vanilla givers a mod has moved out of a role's
    /// work types.
    public static class WorkTypeCoverage
    {
        /// Work types reachable through the given roles. WorkType entries
        /// contribute directly; WorkGiver entries contribute their parent type.
        /// Blocker roles contribute NOTHING: their entries are vetoes.
        public static HashSet<string> CoveredWorkTypes(
            IEnumerable<(IReadOnlyList<JobEntry> entries, bool blocker)> roles,
            IJobCatalog catalog)
        {
            var covered = new HashSet<string>();
            foreach (var (entries, blocker) in roles)
            {
                if (blocker) continue;
                AddCoveredEntries(covered, entries, catalog);
            }
            return covered;
        }

        public static void AddCoveredEntries(
            HashSet<string> covered, IReadOnlyList<JobEntry> entries, IJobCatalog catalog)
        {
            foreach (var entry in entries)
            {
                if (entry.Kind == JobEntryKind.WorkType)
                {
                    covered.Add(entry.DefName);
                }
                else
                {
                    var parentType = catalog.WorkTypeOf(entry.DefName);
                    if (parentType != null) covered.Add(parentType);
                }
            }
        }

        /// Baseline givers that originally lived under one of the role's work-type
        /// entries but currently sit under a DIFFERENT work type (moved by a mod)
        /// and aren't yet remembered in the role's snapshots. Missing givers are
        /// skipped. Null when there is nothing to recover.
        public static Dictionary<string, List<string>> MovedGivers(
            IReadOnlyList<JobEntry> entries,
            IReadOnlyDictionary<string, List<string>> snapshots,
            IReadOnlyDictionary<string, string> baseline,
            IJobCatalog catalog)
        {
            Dictionary<string, List<string>> result = null;
            foreach (var entry in entries)
            {
                if (entry.Kind != JobEntryKind.WorkType) continue;
                foreach (var pair in baseline)
                {
                    if (pair.Value != entry.DefName) continue;                    // not originally this type
                    var currentType = catalog.WorkTypeOf(pair.Key);
                    if (currentType == null || currentType == entry.DefName) continue; // gone, or not moved
                    if (snapshots.TryGetValue(entry.DefName, out var known)
                        && known != null && known.Contains(pair.Key)) continue;   // already remembered
                    result ??= new Dictionary<string, List<string>>();
                    if (!result.TryGetValue(entry.DefName, out var list))
                        result[entry.DefName] = list = new List<string>();
                    list.Add(pair.Key);
                }
            }
            return result;
        }
    }
}
