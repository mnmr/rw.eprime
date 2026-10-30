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
        /// giver that serves the bill's giver (a bench, or the patient pawn for
        /// surgery, judged by vanilla's own ThingIsUsableBillGiver) and matches
        /// the recipe's required giver work type — either the giver itself or
        /// its whole work type, including jobs retained when a mod moves them
        /// to another work type.
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
                if (!(def.Worker is WorkGiver_DoBill giver) || !giver.ThingIsUsableBillGiver(bench)) continue;
                if (bill!.recipe.requiredGiverWorkType != null && bill.recipe.requiredGiverWorkType != def.workType) continue; // bench != null implies bill != null
                givers.Add(def);
            }
            if (givers.Count == 0) return result;

            foreach (var role in store.roles)
            {
                if (role.blocker) continue; // vetoes can't work a bill
                bool matches = false;
                if (role.composite)
                {
                    // Structural coverage intentionally omits historical jobs.
                    // Bill choices must include the work compilation retains.
                    foreach (int memberId in role.memberRoleIds)
                    {
                        Role? member = store.RoleById(memberId);
                        if (member == null || member.composite || member.blocker) continue;
                        if (ProvidesBillGiver(member, givers))
                        {
                            matches = true;
                            break;
                        }
                    }
                }
                else
                    matches = ProvidesBillGiver(role, givers);
                if (matches) result.Add(role);
            }
            return result;
        }

        private static bool ProvidesBillGiver(Role role, List<WorkGiverDef> givers)
        {
            var entries = JobOrderCompiler.WithMovedSnapshotGivers(
                role.entries, role.workTypeSnapshots, GameJobCatalog.Instance);
            foreach (var entry in entries)
                foreach (var giver in givers)
                    if (entry.Kind == JobEntryKind.WorkGiver
                        ? entry.DefName == giver.defName
                        : giver.workType != null && entry.DefName == giver.workType.defName)
                        return true;
            return false;
        }

        /// The role a bill is restricted to, or null.
        public static Role? RestrictionFor(Bill bill)
        {
            var store = RoleStore.Current;
            if (store == null || bill == null || !store.billRoles.TryGetValue(bill, out int roleId)) return null;
            return store.RoleById(roleId);
        }

        /// Whether the pawn satisfies the bill's role restriction: the role must be
        /// ACTIVELY held — assigned, enabled globally and for the pawn, rules passing
        /// (a time-ruled role makes this a shift bill).
        public static bool Allowed(Bill bill, Pawn pawn)
        {
            var store = RoleStore.Current;
            if (store == null || bill == null
                || !store.billRoles.TryGetValue(bill, out int roleId)
                || store.RoleById(roleId) == null)
                return true;
            return store.IsManaged(pawn)
                && CompiledJobOrders.IsRoleActive(pawn, roleId);
        }
    }
}
