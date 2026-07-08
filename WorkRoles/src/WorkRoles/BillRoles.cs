using System.Collections.Generic;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    /// Per-bill role restrictions: a bill can require its worker to actively hold a
    /// role. Enforced in Bill.PawnAllowedToStartAnew (the single gate WorkGiver_DoBill
    /// consults), configured from a "Worker role" row in the bill dialog.
    public static class BillRoles
    {
        /// Roles that make sense for this bill: those providing a bill-doing work
        /// giver that serves the bill's bench and matches the recipe's required
        /// giver work type — either the giver itself or its whole work type.
        public static List<Role> EligibleRoles(Bill bill)
        {
            var result = new List<Role>();
            var store = RoleStore.Current;
            var bench = bill?.billStack?.billGiver as Thing;
            if (store == null || bench == null) return result;

            var givers = new List<WorkGiverDef>();
            foreach (var def in DefDatabase<WorkGiverDef>.AllDefsListForReading)
            {
                if (def.giverClass == null || !typeof(WorkGiver_DoBill).IsAssignableFrom(def.giverClass)) continue;
                if (def.fixedBillGiverDefs == null || !def.fixedBillGiverDefs.Contains(bench.def)) continue;
                if (bill.recipe.requiredGiverWorkType != null && bill.recipe.requiredGiverWorkType != def.workType) continue;
                givers.Add(def);
            }
            if (givers.Count == 0) return result;

            foreach (var role in store.roles)
            {
                bool matches = false;
                foreach (var entry in role.entries)
                {
                    foreach (var giver in givers)
                    {
                        if (entry.Kind == JobEntryKind.WorkGiver
                            ? entry.DefName == giver.defName
                            : giver.workType != null && entry.DefName == giver.workType.defName)
                        {
                            matches = true;
                            break;
                        }
                    }
                    if (matches) break;
                }
                if (matches) result.Add(role);
            }
            return result;
        }

        /// The role a bill is restricted to, or null.
        public static Role RestrictionFor(Bill bill)
        {
            var store = RoleStore.Current;
            if (store == null || bill == null || !store.billRoles.TryGetValue(bill, out int roleId)) return null;
            var role = store.RoleById(roleId);
            if (role == null) store.billRoles.Remove(bill); // role deleted: restriction lapses
            return role;
        }

        /// Whether the pawn satisfies the bill's role restriction: the role must be
        /// ACTIVELY held — assigned, enabled globally and for the pawn, rules passing
        /// (a time-ruled role makes this a shift bill).
        public static bool Allowed(Bill bill, Pawn pawn)
        {
            var role = RestrictionFor(bill);
            if (role == null) return true;
            var store = RoleStore.Current;
            if (pawn == null || !store.pawnSets.TryGetValue(pawn, out var set)) return false;
            foreach (var assignment in set.assignments)
                if (assignment.roleId == role.id)
                    return assignment.enabled && role.enabled && RoleRules.Pass(role, pawn);
            return false;
        }
    }
}
