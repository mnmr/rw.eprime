using System;
using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core
{
    /// One catalog role as migration sees it (game-independent projection).
    public readonly struct MigrationRole
    {
        public int Id { get; }
        public IReadOnlyList<JobEntry> Entries { get; }
        /// Blockers never migrate from priorities.
        public bool Excluded { get; }

        public MigrationRole(int id, IReadOnlyList<JobEntry> entries, bool excluded)
        {
            Id = id;
            Entries = entries;
            Excluded = excluded;
        }
    }

    /// One ordered assignment produced by migration: an existing role, or a
    /// work type that needs a single-type carrier role materialized for it.
    public readonly struct MigrationSlot
    {
        /// Existing role id; -1 when CarrierWorkType is set.
        public int RoleId { get; }
        /// Enabled work type no existing role can carry at its own rank; the
        /// caller creates a single-type role and assigns it at this position.
        public string CarrierWorkType { get; }

        public MigrationSlot(int roleId)
        {
            RoleId = roleId;
            CarrierWorkType = null;
        }

        public MigrationSlot(string carrierWorkType)
        {
            RoleId = -1;
            CarrierWorkType = carrierWorkType;
        }
    }

    /// Derives the ordered role assignments that reproduce a vanilla priority
    /// grid, losslessly where the catalog allows.
    ///
    /// Rules:
    /// - A multi-type role (Basics, Farmer, Grunt) is used only when every member
    ///   type the pawn is capable of is enabled at ONE shared priority; otherwise
    ///   each enabled type gets its single-type role at its own priority.
    /// - Relaxed fallback, only for roles carrying a member type no single-type
    ///   role covers (urgent-haul compat types): when every capable member is
    ///   enabled and the pawn's priority order agrees with the role's entry
    ///   order, the role is used alone at its first member's priority. One
    ///   assignment beats exact rank fidelity for the members riding along.
    /// - A "single-type role" carries the whole work type and nothing outside it:
    ///   same-type giver entries do not disqualify it, foreign entries do —
    ///   migration must never enable work the grid didn't have.
    /// - An enabled type left with no role at all becomes a carrier slot when
    ///   eligible: the caller materializes a single-type role for it.
    /// - Roles order by vanilla priority; ties keep catalog order, with carrier
    ///   slots after catalog roles.
    public static class MigrationPlanner
    {
        /// Strict planning without relaxed matches or carrier slots — the
        /// vanilla+DLC behavior, where every Basics member has a single-type
        /// role and this equals PlanSlots. Returns role ids in assignment order.
        public static List<int> Plan(
            IReadOnlyList<MigrationRole> roles,
            IReadOnlyDictionary<string, int> priorities,
            IReadOnlyList<string> workTypesInOrder,
            IJobCatalog catalog)
        {
            var slots = PlanSlots(roles, priorities, workTypesInOrder, catalog, null, null);
            var ids = new List<int>(slots.Count);
            foreach (var slot in slots) ids.Add(slot.RoleId);
            return ids;
        }

        /// priorities: capable work types only (absent key = pawn incapable);
        /// value 0 = capable but unassigned. relaxedRoles: see RelaxedMatchRoles;
        /// must be computed once per migration so carrier roles created for
        /// earlier pawns cannot change matching for later ones.
        public static List<MigrationSlot> PlanSlots(
            IReadOnlyList<MigrationRole> roles,
            IReadOnlyDictionary<string, int> priorities,
            IReadOnlyList<string> workTypesInOrder,
            IJobCatalog catalog,
            ISet<int> relaxedRoles,
            Func<string, bool> carrierEligible)
        {
            var picked = new List<(MigrationSlot slot, int priority, int order)>();
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
                picked.Add((new MigrationSlot(role.Id), shared, i));
                foreach (var member in capable) consumed.Add(member);
            }

            // Relaxed pass: all capable members enabled and ordered like the
            // entries — the role rides at its first (highest-ranked) member's
            // priority.
            if (relaxedRoles != null && relaxedRoles.Count > 0)
                for (int i = 0; i < roles.Count; i++)
                {
                    var role = roles[i];
                    if (role.Excluded || !relaxedRoles.Contains(role.Id)) continue;
                    var capable = MemberTypes(role).Where(priorities.ContainsKey).ToList();
                    if (capable.Count < 2 || capable.Any(consumed.Contains)) continue;
                    if (capable.Any(t => priorities[t] == 0)) continue;
                    bool agrees = true;
                    for (int k = 1; k < capable.Count; k++)
                        if (priorities[capable[k]] < priorities[capable[k - 1]])
                        {
                            agrees = false;
                            break;
                        }
                    if (!agrees) continue;
                    picked.Add((new MigrationSlot(role.Id), priorities[capable[0]], i));
                    foreach (var member in capable) consumed.Add(member);
                }

            // Everything still enabled gets its single-type role at its own
            // priority — or a carrier slot when no role can take it.
            for (int w = 0; w < workTypesInOrder.Count; w++)
            {
                var workType = workTypesInOrder[w];
                if (consumed.Contains(workType)) continue;
                if (!priorities.TryGetValue(workType, out int priority) || priority == 0) continue;
                int index = SingleRoleIndexFor(roles, workType, catalog);
                if (index >= 0)
                    picked.Add((new MigrationSlot(roles[index].Id), priority, index));
                else if (carrierEligible != null && carrierEligible(workType))
                    picked.Add((new MigrationSlot(workType), priority, roles.Count + w));
                else
                    continue;
                consumed.Add(workType);
            }

            return picked
                .OrderBy(p => p.priority)
                .ThenBy(p => p.order)
                .Select(p => p.slot)
                .ToList();
        }

        /// Multi-type roles carrying a member type no single-type role covers:
        /// the only candidates for the relaxed match and the colony-majority
        /// entry reorder. Vanilla+DLC catalogs yield an empty set.
        public static HashSet<int> RelaxedMatchRoles(
            IReadOnlyList<MigrationRole> roles, IJobCatalog catalog)
        {
            var result = new HashSet<int>();
            foreach (var role in roles)
            {
                if (role.Excluded) continue;
                var members = MemberTypes(role);
                if (members.Count < 2) continue;
                if (members.Any(m => SingleRoleIndexFor(roles, m, catalog) < 0))
                    result.Add(role.Id);
            }
            return result;
        }

        /// Colony-majority member order per candidate role: each colonist with
        /// every member capable and enabled votes with the members stable-sorted
        /// by its priorities (ties keep entry order); the most common order wins,
        /// with ties preferring the current entry order, then the ordinally
        /// smallest key. Returns only roles whose winner differs from their
        /// current entry order.
        public static List<(int roleId, List<string> memberOrder)> PreferredMemberOrders(
            IReadOnlyList<MigrationRole> roles,
            ISet<int> candidateRoleIds,
            IReadOnlyList<IReadOnlyDictionary<string, int>> colonistPriorities)
        {
            var result = new List<(int, List<string>)>();
            if (candidateRoleIds == null || candidateRoleIds.Count == 0) return result;
            foreach (var role in roles)
            {
                if (!candidateRoleIds.Contains(role.Id)) continue;
                var members = MemberTypes(role);
                var votes = new Dictionary<string, (int count, List<string> order)>();
                foreach (var priorities in colonistPriorities)
                {
                    bool eligible = true;
                    foreach (var member in members)
                        if (!priorities.TryGetValue(member, out int p) || p == 0)
                        {
                            eligible = false;
                            break;
                        }
                    if (!eligible) continue;
                    var order = members.OrderBy(m => priorities[m]).ToList();
                    string key = string.Join("|", order);
                    votes[key] = votes.TryGetValue(key, out var seen)
                        ? (seen.count + 1, order) : (1, order);
                }
                if (votes.Count == 0) continue;
                int best = 0;
                foreach (var vote in votes.Values) best = Math.Max(best, vote.count);
                string currentKey = string.Join("|", members);
                if (votes.TryGetValue(currentKey, out var current) && current.count == best)
                    continue;
                var winner = votes.Where(v => v.Value.count == best)
                    .OrderBy(v => v.Key, StringComparer.Ordinal).First().Value.order;
                result.Add((role.Id, winner));
            }
            return result;
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
