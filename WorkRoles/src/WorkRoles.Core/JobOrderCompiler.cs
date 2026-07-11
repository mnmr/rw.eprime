using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
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

        /// Projects a work type's unique rank onto the vanilla 1..4 priority scale
        /// (quartiles over the ranked count). Callers map absent work types to 0.
        public static int ToVanillaPriority(int rank, int rankedCount)
        {
            if (rank < 1 || rankedCount < 1) return 0;
            return 1 + (rank - 1) * 4 / rankedCount;
        }
    }
}
