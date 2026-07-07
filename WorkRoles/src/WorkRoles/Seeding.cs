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

            EnsureWorkTypeCoverage();

            int assigned = 0;
            foreach (var pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive)
                if (TryAssignRolesFromVanillaPriorities(pawn))
                    assigned++;

            Log.Message($"[WorkRoles] seeded {store.roles.Count} roles, assigned role sets to {assigned} pawns");
        }

        /// Derives a pawn's role set from its vanilla work priorities, losslessly where
        /// the catalog allows. Must read priorities BEFORE assigning anything: an
        /// unmanaged pawn's GetPriority passes through to vanilla values; the first
        /// assignment makes the pawn managed and reads then return WorkRoles ranks.
        ///
        /// Rules:
        /// - A multi-type role (Basics, Farmer, Grunt) is used only when every member
        ///   type the pawn is capable of is enabled at ONE shared priority; otherwise
        ///   each enabled member gets its single-type role at its own priority.
        /// - Roles with no work-type entries are never assigned here.
        /// - Roles are ordered by vanilla priority; ties keep catalog order.
        /// The result reproduces the pawn's vanilla priority grid exactly.
        public static bool TryAssignRolesFromVanillaPriorities(Pawn pawn)
        {
            var store = RoleStore.Current;
            if (store == null || !store.seeded) return false;
            if (pawn == null || !(pawn.IsColonist || pawn.IsSlaveOfColony)) return false;
            if (store.IsManaged(pawn)) return false;

            var workSettings = pawn.workSettings;
            bool everWork = workSettings != null && workSettings.EverWork;

            int PriorityOf(WorkTypeDef workType)
                => everWork && !pawn.WorkTypeIsDisabled(workType) ? workSettings.GetPriority(workType) : 0;

            List<WorkTypeDef> MemberTypes(Role role)
            {
                var types = new List<WorkTypeDef>();
                foreach (var entry in role.entries)
                {
                    if (entry.Kind != JobEntryKind.WorkType) continue;
                    var workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(entry.DefName);
                    if (workType != null) types.Add(workType);
                }
                return types;
            }

            Role SingleRoleFor(WorkTypeDef workType)
            {
                foreach (var role in store.roles)
                {
                    if (role.entries.Count != 1) continue;
                    var entry = role.entries[0];
                    if (entry.Kind == JobEntryKind.WorkType && entry.DefName == workType.defName)
                        return role;
                }
                return null;
            }

            var picked = new List<(Role role, int score)>();
            var consumed = new HashSet<string>();

            // Multi-type roles: only when all capable members share one enabled priority.
            foreach (var role in store.roles)
            {
                var capable = MemberTypes(role).Where(t => !pawn.WorkTypeIsDisabled(t)).ToList();
                if (capable.Count < 2 || capable.Any(t => consumed.Contains(t.defName))) continue;
                int shared = PriorityOf(capable[0]);
                if (shared == 0 || capable.Any(t => PriorityOf(t) != shared)) continue;
                picked.Add((role, shared));
                foreach (var member in capable) consumed.Add(member.defName);
            }

            // Everything still enabled gets its single-type role at its own priority.
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (consumed.Contains(workType.defName)) continue;
                int priority = PriorityOf(workType);
                if (priority == 0) continue;
                var single = SingleRoleFor(workType);
                if (single == null) continue;
                picked.Add((single, priority));
                consumed.Add(workType.defName);
            }

            if (picked.Count == 0) return false;

            var catalogIndex = new Dictionary<int, int>();
            for (int i = 0; i < store.roles.Count; i++) catalogIndex[store.roles[i].id] = i;

            foreach (var (role, _) in picked.OrderBy(t => t.score).ThenBy(t => catalogIndex[t.role.id]))
                RoleCommands.AssignRoleDirect(pawn, role.id);
            return true;
        }

        /// Assigns only the auto-assign roles (Basics) — used for pawns joining
        /// mid-game, mirroring vanilla's minimal auto-enable; vocational roles are the
        /// player's call (the Recommended Roles panel covers it).
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
        /// a generated role sticks. Returns labels of newly generated roles.
        public static List<string> EnsureWorkTypeCoverage()
        {
            var store = RoleStore.Current;
            var result = new List<string>();
            if (store == null || !store.seeded) return result;

            // Build covered set: WorkType entries contribute directly; WorkGiver entries contribute their parent type.
            var covered = new HashSet<string>();
            void AddCovered(Role role)
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
            foreach (var role in store.roles)
                AddCovered(role);
            if (store.allRole != null)
                AddCovered(store.allRole);

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
                    // Invisible work types go to the engine-internal All role — every
                    // colonist does them implicitly; not reported (the role is secret).
                    store.EnsureAllRole().entries.Add(
                        new WorkRoles.Core.JobEntry(WorkRoles.Core.JobEntryKind.WorkType, workType.defName));
                    CompiledJobOrders.InvalidateAll();
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

    }
}
