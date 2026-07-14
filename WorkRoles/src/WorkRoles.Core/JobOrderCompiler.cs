using System;
using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core
{
    /// Work-type category sets (defNames) steering the vanilla projection's
    /// pin-and-spread fallbacks.
    public sealed class VanillaProjectionCategories
    {
        /// The Basics role's work types: pinned to 1 when four numbers can't hold the order.
        public HashSet<string> Basics = new HashSet<string>();
        /// Types using any skill; first spread target when numbers go unused.
        public HashSet<string> Skilled = new HashSet<string>();
        /// Unskilled non-basics types (hauling, cleaning); second spread target.
        public HashSet<string> Grunt = new HashSet<string>();
        /// Intellectual types; last spread target.
        public HashSet<string> Research = new HashSet<string>();
    }

    public static class JobOrderCompiler
    {
        public static CompiledOrder Compile(
            IEnumerable<IReadOnlyList<JobEntry>> orderedEnabledRoleEntries,
            IJobCatalog catalog,
            Func<string, bool> pawnCanDo)
        {
            var slices = new List<(IReadOnlyList<JobEntry> entries, bool blocker)>();
            foreach (var entries in orderedEnabledRoleEntries)
                slices.Add((entries, false));
            return Compile(slices, catalog, pawnCanDo);
        }

        /// First claim on a job wins: a blocker role's claim marks the job blocked
        /// (never done, vetoed in all later roles); a normal role's claim ranks it.
        public static CompiledOrder Compile(
            IEnumerable<(IReadOnlyList<JobEntry> entries, bool blocker)> orderedEnabledRoles,
            IJobCatalog catalog,
            Func<string, bool> pawnCanDo)
        {
            var result = new CompiledOrder();
            var seen = new HashSet<string>();

            void TryAdd(string giverDefName, bool block)
            {
                if (seen.Contains(giverDefName)) return;
                if (catalog.WorkTypeOf(giverDefName) == null) return;
                if (!pawnCanDo(giverDefName)) return;
                seen.Add(giverDefName);
                if (!block) result.AllInOrder.Add(giverDefName);
            }

            foreach (var (roleEntries, blocker) in orderedEnabledRoles)
            {
                foreach (var entry in roleEntries)
                {
                    if (entry.Kind == JobEntryKind.WorkGiver)
                    {
                        TryAdd(entry.DefName, blocker);
                    }
                    else
                    {
                        foreach (var giver in catalog.WorkGiversOf(entry.DefName))
                            TryAdd(giver, blocker);
                    }
                }
            }
            int nextRank = 0;
            for (int i = 0; i < result.AllInOrder.Count; i++)
            {
                string workType = catalog.WorkTypeOf(result.AllInOrder[i]);
                if (!result.WorkTypePriorities.ContainsKey(workType))
                    result.WorkTypePriorities[workType] = ++nextRank;
            }

            // Role membership already decides WHETHER a pawn does emergency work
            // (omission = off), so every emergency-flagged job present goes to the
            // emergency list — no priority gate.
            foreach (var giver in result.AllInOrder)
                (catalog.IsEmergency(giver) ? result.Emergency : result.Normal).Add(giver);
            return result;
        }

        /// Indexes of entries with no effect under first-claim-wins: a giver
        /// already claimed above it (by its work type or a duplicate), or a
        /// duplicate work type. A work type BELOW its own givers is not dead —
        /// it claims the type's remaining jobs and catches future ones. Unknown
        /// entries (absent DLC/mods) are never dead: they wake when their def returns.
        public static HashSet<int> DeadEntryIndexes(IReadOnlyList<JobEntry> entries, IJobCatalog catalog)
        {
            var dead = new HashSet<int>();
            var claimed = new HashSet<string>();
            var seenTypes = new HashSet<string>();
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Kind == JobEntryKind.WorkType)
                {
                    if (!seenTypes.Add(entry.DefName)) { dead.Add(i); continue; }
                    foreach (var giver in catalog.WorkGiversOf(entry.DefName))
                        claimed.Add(giver);
                }
                else
                {
                    if (catalog.WorkTypeOf(entry.DefName) == null) continue;
                    if (!claimed.Add(entry.DefName)) dead.Add(i);
                }
            }
            return dead;
        }

        /// Entries plus, after each work-type entry, synthetic giver entries for
        /// snapshot members a mod has MOVED to another work type — so the role
        /// keeps jobs it was built around. Members still under the type expand
        /// dynamically as usual; givers that no longer exist are skipped.
        public static IReadOnlyList<JobEntry> WithMovedSnapshotGivers(
            IReadOnlyList<JobEntry> entries,
            IReadOnlyDictionary<string, List<string>> workTypeSnapshots,
            IJobCatalog catalog)
        {
            if (workTypeSnapshots == null || workTypeSnapshots.Count == 0) return entries;
            var expanded = new List<JobEntry>(entries.Count + 4);
            foreach (var entry in entries)
            {
                expanded.Add(entry);
                if (entry.Kind != JobEntryKind.WorkType) continue;
                if (!workTypeSnapshots.TryGetValue(entry.DefName, out var known) || known == null) continue;
                foreach (var giver in known)
                {
                    var currentType = catalog.WorkTypeOf(giver);
                    if (currentType != null && currentType != entry.DefName)
                        expanded.Add(new JobEntry(JobEntryKind.WorkGiver, giver));
                }
            }
            return expanded;
        }

        /// Projects ranked work types onto vanilla's 1-4 scale so that vanilla's
        /// replay — all 1s in Work-tab column order, then 2s, 3s, 4s — reproduces
        /// the internal rank order: a new number starts whenever a type sits LEFT
        /// of its rank-predecessor (the same number would run it first).
        ///
        /// Four numbers can't always suffice. When the tail lumps into 4, the
        /// always-on basics head is pinned to 1 and the projection redone —
        /// numbers spent ordering the head buy back order where it matters.
        /// When numbers go UNUSED, category bumps spread the range instead:
        /// skilled work (and everything ranked after it) moves down a number,
        /// then grunt work, then research — approximating vanilla's feel.
        public static Dictionary<string, int> ToVanillaPriorities(
            IReadOnlyDictionary<string, int> workTypeRanks,
            Func<string, int> columnOf,
            VanillaProjectionCategories categories = null)
        {
            var buckets = Project(workTypeRanks, columnOf, null, out bool lumped);

            HashSet<string> pinned = null;
            if (lumped && categories != null)
            {
                pinned = new HashSet<string>(workTypeRanks.Keys.Where(categories.Basics.Contains));
                if (pinned.Count == 0)
                    pinned = null;
                else if (pinned.Any(t => buckets[t] != 1))
                    buckets = Project(workTypeRanks, columnOf, pinned, out _);
            }

            int Max() => buckets.Count == 0 ? 0 : buckets.Values.Max();
            void BumpFromFirst(HashSet<string> category)
            {
                int firstRank = int.MaxValue;
                foreach (var pair in workTypeRanks)
                    if (category.Contains(pair.Key) && pair.Value < firstRank)
                        firstRank = pair.Value;
                if (firstRank == int.MaxValue) return;
                foreach (var pair in workTypeRanks)
                    if (pair.Value >= firstRank && (pinned == null || !pinned.Contains(pair.Key)))
                        buckets[pair.Key] = Math.Min(4, buckets[pair.Key] + 1);
            }

            if (categories != null && buckets.Count > 0 && Max() < 4)
            {
                BumpFromFirst(categories.Skilled);
                if (Max() < 4) BumpFromFirst(categories.Grunt);
                if (Max() < 4)
                    foreach (var workType in buckets.Keys.ToList())
                        if (categories.Research.Contains(workType)
                            && (pinned == null || !pinned.Contains(workType)))
                            buckets[workType] = Math.Min(4, buckets[workType] + 1);
            }
            return buckets;
        }

        /// One greedy pass. Pinned types take 1 and act as the walk's already-
        /// consumed head: the remaining types continue from the pinned block's
        /// rightmost column, so nothing sorts ahead of it inside number 1.
        private static Dictionary<string, int> Project(
            IReadOnlyDictionary<string, int> workTypeRanks,
            Func<string, int> columnOf,
            HashSet<string> pinnedToOne,
            out bool lumped)
        {
            var buckets = new Dictionary<string, int>();
            int bucket = 1;
            int previousColumn = int.MinValue;
            lumped = false;
            if (pinnedToOne != null)
                foreach (var workType in pinnedToOne)
                {
                    buckets[workType] = 1;
                    previousColumn = Math.Max(previousColumn, columnOf(workType));
                }
            foreach (var pair in workTypeRanks.OrderBy(kv => kv.Value))
            {
                if (pinnedToOne != null && pinnedToOne.Contains(pair.Key)) continue;
                int column = columnOf(pair.Key);
                if (column < previousColumn)
                {
                    if (bucket < 4) bucket++;
                    else lumped = true;
                }
                buckets[pair.Key] = bucket;
                previousColumn = column;
            }
            return buckets;
        }
    }
}
