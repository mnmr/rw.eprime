using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WorkRoles.Core;

namespace WorkRoles
{
    public static class Seeding
    {
        public static void SeedIfNeeded()
        {
            var store = RoleStore.Current;
            if (store == null || store.seeded) return;

            foreach (var def in DefDatabase<RoleDef>.AllDefsListForReading)
                RoleCommands.CreateRoleFromDef(def);
            store.seeded = true;

            int assigned = 0;
            foreach (var pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive)
                if (TryAssignRolesFromVanillaPriorities(pawn))
                    assigned++;

            Log.Message($"[WorkRoles] seeded {store.roles.Count} roles, assigned role sets to {assigned} pawns");
        }

        /// Derives a pawn's role set from its vanilla work priorities. Must read priorities
        /// BEFORE assigning anything: an unmanaged pawn's GetPriority passes through to
        /// vanilla values; the first assignment makes the pawn managed and reads then
        /// return WorkRoles ranks. Auto-assign roles go first (catalog order), then one
        /// role per vocation the pawn had enabled, best vanilla priority first (ties keep
        /// catalog order).
        public static bool TryAssignRolesFromVanillaPriorities(Pawn pawn)
        {
            var store = RoleStore.Current;
            if (store == null || !store.seeded) return false;
            if (pawn == null || !(pawn.IsColonist || pawn.IsSlaveOfColony)) return false;
            if (store.IsManaged(pawn)) return false;

            var autoAssign = new List<Role>();
            var scored = new List<(Role role, int score)>();
            foreach (var role in store.roles)
            {
                var template = role.templateDefName != null
                    ? DefDatabase<RoleDef>.GetNamedSilentFail(role.templateDefName)
                    : null;
                if (template != null && template.autoAssign)
                {
                    autoAssign.Add(role);
                    continue;
                }
                int score = BestVanillaPriorityFor(pawn, role);
                if (score > 0) scored.Add((role, score));
            }

            if (autoAssign.Count == 0 && scored.Count == 0) return false;

            foreach (var role in autoAssign)
                RoleCommands.AssignRole(pawn, role.id);
            // OrderBy is stable: equal priorities keep catalog (work tab) order.
            foreach (var (role, _) in scored.OrderBy(t => t.score))
                RoleCommands.AssignRole(pawn, role.id);
            return true;
        }

        /// Best (lowest non-zero) vanilla priority across the role's work-type entries;
        /// 0 when the pawn had none of them enabled.
        private static int BestVanillaPriorityFor(Pawn pawn, Role role)
        {
            var workSettings = pawn.workSettings;
            if (workSettings == null || !workSettings.EverWork) return 0;
            int best = 0;
            foreach (var entry in role.entries)
            {
                if (entry.Kind != JobEntryKind.WorkType) continue;
                var workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(entry.DefName);
                if (workType == null || pawn.WorkTypeIsDisabled(workType)) continue;
                int priority = workSettings.GetPriority(workType);
                if (priority > 0 && (best == 0 || priority < best)) best = priority;
            }
            return best;
        }
    }
}
