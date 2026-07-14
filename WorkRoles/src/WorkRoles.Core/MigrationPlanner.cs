using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core
{
    /// One catalog role as migration sees it (game-independent projection).
    public readonly struct MigrationRole
    {
        public int Id { get; }
        public IReadOnlyList<JobEntry> Entries { get; }
        /// Blockers and the engine-managed role never migrate from priorities.
        public bool Excluded { get; }

        public MigrationRole(int id, IReadOnlyList<JobEntry> entries, bool excluded)
        {
            Id = id;
            Entries = entries;
            Excluded = excluded;
        }
    }

    /// Derives the ordered role assignments that reproduce a vanilla priority
    /// grid, losslessly where the catalog allows.
    ///
    /// Rules:
    /// - A multi-type role (Basics, Farmer, Grunt) is used only when every member
    ///   type the pawn is capable of is enabled at ONE shared priority; otherwise
    ///   each enabled type gets its single-type role at its own priority.
    /// - A "single-type role" carries the whole work type and nothing outside it:
    ///   same-type giver entries do not disqualify it, foreign entries do —
    ///   migration must never enable work the grid didn't have.
    /// - Roles order by vanilla priority; ties keep catalog order.
    public static class MigrationPlanner
    {
        /// priorities: capable work types only (absent key = pawn incapable);
        /// value 0 = capable but unassigned. Returns role ids in assignment order.
        public static List<int> Plan(
            IReadOnlyList<MigrationRole> roles,
            IReadOnlyDictionary<string, int> priorities,
            IReadOnlyList<string> workTypesInOrder,
            IJobCatalog catalog)
        {
            var picked = new List<(int id, int priority, int catalogIndex)>();
            var consumed = new HashSet<string>();

            // Multi-type roles: only when all capable members share one enabled priority.
            for (int i = 0; i < roles.Count; i++)
            {
                var role = roles[i];
                if (role.Excluded) continue;
                var capable = MemberTypes(role).Where(priorities.ContainsKey).ToList();
                if (capable.Count < 2 || capable.Any(consumed.Contains)) continue;
                int shared = priorities[capable[0]];
                if (shared == 0 || capable.Any(t => priorities[t] != shared)) continue;
                picked.Add((role.Id, shared, i));
                foreach (var member in capable) consumed.Add(member);
            }

            // Everything still enabled gets its single-type role at its own priority.
            foreach (var workType in workTypesInOrder)
            {
                if (consumed.Contains(workType)) continue;
                if (!priorities.TryGetValue(workType, out int priority) || priority == 0) continue;
                int index = SingleRoleIndexFor(roles, workType, catalog);
                if (index < 0) continue;
                picked.Add((roles[index].Id, priority, index));
                consumed.Add(workType);
            }

            return picked
                .OrderBy(p => p.priority)
                .ThenBy(p => p.catalogIndex)
                .Select(p => p.id)
                .ToList();
        }

        private static List<string> MemberTypes(MigrationRole role) =>
            role.Entries
                .Where(e => e.Kind == JobEntryKind.WorkType)
                .Select(e => e.DefName)
                .Distinct()
                .ToList();

        private static int SingleRoleIndexFor(
            IReadOnlyList<MigrationRole> roles, string workType, IJobCatalog catalog)
        {
            for (int i = 0; i < roles.Count; i++)
            {
                var role = roles[i];
                if (role.Excluded) continue;
                bool hasType = false, foreign = false;
                foreach (var entry in role.Entries)
                {
                    if (entry.Kind == JobEntryKind.WorkType)
                    {
                        if (entry.DefName == workType) hasType = true;
                        else { foreign = true; break; }
                    }
                    else
                    {
                        var parentType = catalog.WorkTypeOf(entry.DefName);
                        if (parentType != null && parentType != workType) { foreign = true; break; }
                    }
                }
                if (hasType && !foreign) return i;
            }
            return -1;
        }
    }
}
