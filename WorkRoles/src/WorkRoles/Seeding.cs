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
            store.basicsRoleId = store.RoleByTemplate("WS_Basics")?.id ?? -1;

            EnsureWorkTypeCoverage();

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
                if (role.autoAssign)
                {
                    autoAssign.Add(role);
                    continue;
                }
                int score = BestVanillaPriorityFor(pawn, role);
                if (score > 0) scored.Add((role, score));
            }

            if (autoAssign.Count == 0 && scored.Count == 0) return false;

            foreach (var role in autoAssign)
                RoleCommands.AssignRoleDirect(pawn, role.id);
            // OrderBy is stable: equal priorities keep catalog (work tab) order.
            foreach (var (role, _) in scored.OrderBy(t => t.score))
                RoleCommands.AssignRoleDirect(pawn, role.id);
            return true;
        }

        /// Assigns only the auto-assign roles (Basics) — used for pawns joining mid-game,
        /// mirroring vanilla's minimal auto-enable; vocational roles are the player's call
        /// (the Recommended Roles panel covers it).
        public static void TryAutoAssignBasics(Pawn pawn)
        {
            var store = RoleStore.Current;
            if (store == null || !store.seeded) return;
            if (pawn == null || !(pawn.IsColonist || pawn.IsSlaveOfColony)) return;
            if (store.IsManaged(pawn)) return;

            foreach (var role in store.roles)
            {
                if (role.autoAssign)
                    RoleCommands.AssignRoleDirect(pawn, role.id);
            }
        }

        /// Ensures every work type is reachable through some role. Runs on every load;
        /// each work type is processed once per save (store.knownWorkTypes), so deleting
        /// a generated role sticks. Returns labels of newly generated/extended roles.
        public static List<string> EnsureWorkTypeCoverage()
        {
            var store = RoleStore.Current;
            var result = new List<string>();
            if (store == null || !store.seeded) return result;

            // Build covered set: WorkType entries contribute directly; WorkGiver entries contribute their parent type.
            var covered = new HashSet<string>();
            foreach (var role in store.roles)
            {
                foreach (var entry in role.entries)
                {
                    if (entry.Kind == WorkRoles.Core.JobEntryKind.WorkType)
                        covered.Add(entry.DefName);
                    else
                    {
                        var parentType = GameJobCatalog.Instance.WorkTypeOf(entry.DefName);
                        if (parentType != null) covered.Add(parentType);
                    }
                }
            }

            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (store.knownWorkTypes.Contains(workType.defName)) continue;

                store.knownWorkTypes.Add(workType.defName);

                if (covered.Contains(workType.defName)) continue;

                if (workType.visible)
                {
                    string label = (workType.gerundLabel ?? workType.labelShort ?? workType.defName).CapitalizeFirst();
                    var role = RoleCommands.CreateRoleDirect(label);
                    if (role != null)
                    {
                        role.color = UnityEngine.Color.HSVToRGB(
                            (workType.defName.GetHashCode() & 0x7FFFFFFF) % 360 / 360f, 0.5f, 0.55f);
                        role.hasCustomColor = true;
                        RoleCommands.AddEntryDirect(role.id, new WorkRoles.Core.JobEntry(WorkRoles.Core.JobEntryKind.WorkType, workType.defName));
                        result.Add(label);
                    }
                }
                else
                {
                    var basics = store.BasicsRole;
                    if (basics == null)
                    {
                        Log.Warning($"[WorkRoles] EnsureWorkTypeCoverage: BasicsRole is null, cannot assign invisible work type '{workType.defName}'");
                        continue;
                    }
                    RoleCommands.AddEntryDirect(basics.id, new WorkRoles.Core.JobEntry(WorkRoles.Core.JobEntryKind.WorkType, workType.defName));
                    result.Add(basics.label);
                }
            }

            // Return distinct labels in encounter order.
            var seen = new HashSet<string>();
            var distinct = new List<string>();
            foreach (var label in result)
                if (seen.Add(label))
                    distinct.Add(label);
            return distinct;
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
