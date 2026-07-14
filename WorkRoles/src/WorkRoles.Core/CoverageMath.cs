using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Job-coverage relations: what a role actually covers (work-type entries
    /// expanded through the catalog), independent of how its entries spell it.
    /// This is the ONE definition of "role A covers role B" — nesting and
    /// redundancy everywhere compare coverage, never literal entries.
    public static class CoverageMath
    {
        /// The set of known givers the entries cover. Entries the catalog
        /// doesn't know (absent DLC, removed mods) cover nothing.
        public static HashSet<string> CoverageOf(IEnumerable<JobEntry> entries, IJobCatalog catalog)
        {
            var coverage = new HashSet<string>();
            foreach (var entry in entries)
            {
                if (entry.Kind == JobEntryKind.WorkGiver)
                {
                    if (catalog.WorkTypeOf(entry.DefName) != null)
                        coverage.Add(entry.DefName);
                }
                else
                {
                    foreach (var giver in catalog.WorkGiversOf(entry.DefName))
                        coverage.Add(giver);
                }
            }
            return coverage;
        }

        /// a strictly covers b: proper superset. Equal coverage does NOT cover —
        /// equals are siblings in the role tree.
        public static bool Covers(HashSet<string> a, HashSet<string> b)
            => b.Count > 0 && a.Count > b.Count && b.IsSubsetOf(a);

        /// a covers b or matches it exactly (capability queries, nothing dropped).
        public static bool CoversOrMatches(HashSet<string> a, HashSet<string> b)
            => b.Count > 0 && b.IsSubsetOf(a);

        /// a makes b redundant: strictly covers it, or matches it with the lower
        /// id — the deterministic winner, so equals never drop each other both ways.
        public static bool MakesRedundant(HashSet<string> a, int aId, HashSet<string> b, int bId)
            => Covers(a, b)
               || (aId < bId && b.Count == a.Count && b.Count > 0 && b.SetEquals(a));
    }
}
