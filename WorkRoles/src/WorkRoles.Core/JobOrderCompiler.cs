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
            var result = new CompiledOrder();
            var seen = new HashSet<string>();

            void TryAdd(string giverDefName)
            {
                if (seen.Contains(giverDefName)) return;
                if (catalog.WorkTypeOf(giverDefName) == null) return;
                if (!pawnCanDo(giverDefName)) return;
                seen.Add(giverDefName);
                result.AllInOrder.Add(giverDefName);
            }

            foreach (var roleEntries in orderedEnabledRoleEntries)
            {
                foreach (var entry in roleEntries)
                {
                    if (entry.Kind == JobEntryKind.WorkGiver)
                    {
                        TryAdd(entry.DefName);
                    }
                    else
                    {
                        foreach (var giver in catalog.WorkGiversOf(entry.DefName))
                            TryAdd(giver);
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

        /// Projects a work type's unique rank onto the vanilla 1..4 priority scale
        /// (quartiles over the ranked count). Callers map absent work types to 0.
        public static int ToVanillaPriority(int rank, int rankedCount)
        {
            if (rank < 1 || rankedCount < 1) return 0;
            return 1 + (rank - 1) * 4 / rankedCount;
        }
    }
}
