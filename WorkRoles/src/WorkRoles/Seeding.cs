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

            var defs = DefDatabase<RoleDef>.AllDefsListForReading;
            if (defs.Count == 0)
            {
                // Def-load failure (bad mod interaction). Leave 'seeded' unset so a
                // fixed modlist seeds normally on the next load.
                Log.Error("[WorkRoles] no RoleDefs loaded; seeding skipped and will retry next load");
                return;
            }

            foreach (var def in defs)
                RoleCommands.CreateRoleFromDef(def);
            store.seeded = true;

            EnsureWorkTypeCoverage();

            int assigned = 0;
            foreach (var pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive)
            {
                try
                {
                    if (TryAssignRolesFromVanillaPriorities(pawn))
                        assigned++;
                }
                catch (System.Exception e)
                {
                    // One corrupt pawn must not abort migration for the rest.
                    Log.Error($"[WorkRoles] failed to migrate priorities of {pawn?.LabelShort ?? "unknown pawn"}: {e}");
                }
            }

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
                    if (role.blocker || role.managed) continue;
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
            // Blockers (vetoes, not work) and the managed role (assigned by coverage)
            // never migrate from priorities.
            foreach (var role in store.roles)
            {
                if (role.blocker || role.managed) continue;
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

        /// Visible modded work types that belong to everyone rather than a vocation:
        /// appended to Basics instead of getting a generated role.
        private static readonly HashSet<string> EveryoneWorkTypes = new HashSet<string>
        {
            "HaulingUrgent", // Allow Tool's "haul urgently"
        };

        /// Ensures every work type is reachable through some role. Runs on every load;
        /// each work type is processed once per save (store.knownWorkTypes), so deleting
        /// a generated role sticks. Returns labels of newly generated roles.
        public static List<string> EnsureWorkTypeCoverage()
        {
            var store = RoleStore.Current;
            var result = new List<string>();
            if (store == null || !store.seeded) return result;

            var covered = CoveredWorkTypes(store);

            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (store.knownWorkTypes.Contains(workType.defName)) continue;

                store.knownWorkTypes.Add(workType.defName);

                if (covered.Contains(workType.defName)) continue;

                var basics = EveryoneWorkTypes.Contains(workType.defName)
                    ? store.RoleByTemplate("WS_Basics")
                    : null;
                if (basics != null)
                {
                    RoleCommands.AddEntryDirect(basics.id,
                        new WorkRoles.Core.JobEntry(WorkRoles.Core.JobEntryKind.WorkType, workType.defName));
                    result.Add(basics.label);
                }
                else if (workType.visible)
                {
                    string label = (workType.gerundLabel ?? workType.labelShort ?? workType.defName).CapitalizeFirst();
                    var role = RoleCommands.CreateRoleDirect(label);
                    if (role != null)
                    {
                        // Everyone-work stays in Basics' color family (slate-700,
                        // one step below Basics); other types get a stable hash hue.
                        role.color = EveryoneWorkTypes.Contains(workType.defName)
                            ? new UnityEngine.Color(0.200f, 0.255f, 0.333f)
                            : UnityEngine.Color.HSVToRGB(
                                (workType.defName.GetHashCode() & 0x7FFFFFFF) % 360 / 360f, 0.5f, 0.55f);
                        role.hasCustomColor = true;
                        RoleCommands.AddEntryDirect(role.id, new WorkRoles.Core.JobEntry(WorkRoles.Core.JobEntryKind.WorkType, workType.defName));
                        result.Add(label);
                    }
                }
                else
                {
                    // Invisible work types go to the engine-managed Odd Jobs role: an
                    // ordinary catalog role (reorderable, assignable, auto-assigned to
                    // everyone) whose ENTRIES only the engine writes.
                    var oddJobs = store.ManagedRole;
                    if (oddJobs == null)
                    {
                        oddJobs = new Role
                        {
                            id = store.NextId(),
                            label = "WR_OddJobsRole".Translate(),
                            managed = true,
                            autoAssign = true,
                            hasCustomColor = true,
                            color = new UnityEngine.Color(0.278f, 0.333f, 0.412f), // slate-600
                        };
                        store.roles.Add(oddJobs);
                        foreach (var set in store.pawnSets.Values)
                            if (set.assignments.Count > 0 && set.assignments.All(a => a.roleId != oddJobs.id))
                                set.assignments.Add(new RoleAssignment { roleId = oddJobs.id });
                    }
                    oddJobs.entries.Add(
                        new WorkRoles.Core.JobEntry(WorkRoles.Core.JobEntryKind.WorkType, workType.defName));
                    result.Add(oddJobs.label);
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

        /// WorkType entries contribute directly; WorkGiver entries contribute their parent type.
        private static void AddCoveredEntries(HashSet<string> covered, List<JobEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry.Kind == JobEntryKind.WorkType)
                    covered.Add(entry.DefName);
                else
                {
                    var parentType = GameJobCatalog.Instance.WorkTypeOf(entry.DefName);
                    if (parentType != null) covered.Add(parentType);
                }
            }
        }

        private static HashSet<string> CoveredWorkTypes(RoleStore store)
        {
            var covered = new HashSet<string>();
            foreach (var role in store.roles)
                if (!role.blocker) // a blocker's entries are vetoes, not coverage
                    AddCoveredEntries(covered, role.entries);
            return covered;
        }

        /// Labels of everything RestoreMissingRoles would recreate: catalog roles
        /// whose template def has no role in the store, plus visible work types that
        /// no role — current or about-to-be-restored — covers.
        public static List<string> MissingSeededRoles()
        {
            var store = RoleStore.Current;
            var result = new List<string>();
            if (store == null) return result;

            var covered = CoveredWorkTypes(store);
            foreach (var def in DefDatabase<RoleDef>.AllDefsListForReading)
            {
                if (store.RoleByTemplate(def.defName) != null) continue;
                result.Add(def.label);
                if (!def.blocker) // a blocker's entries are vetoes, not coverage
                    AddCoveredEntries(covered, def.ParsedEntries());
            }
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                if (workType.visible && !covered.Contains(workType.defName))
                    result.Add((workType.gerundLabel ?? workType.labelShort ?? workType.defName).CapitalizeFirst());
            return result;
        }

        /// Recreates whatever MissingSeededRoles reports: catalog roles from their
        /// defs, then regenerated coverage for still-uncovered work types (their
        /// knownWorkTypes entries are forgotten so EnsureWorkTypeCoverage reprocesses
        /// them). Existing roles are never touched. Returns labels of restored roles.
        public static List<string> RestoreMissingRoles()
        {
            var store = RoleStore.Current;
            var result = new List<string>();
            if (store == null) return result;

            foreach (var def in DefDatabase<RoleDef>.AllDefsListForReading)
            {
                if (store.RoleByTemplate(def.defName) != null) continue;
                var role = RoleCommands.CreateRoleFromDef(def);
                if (role != null) result.Add(role.label);
            }

            var covered = CoveredWorkTypes(store);
            store.knownWorkTypes.RemoveAll(wt => !covered.Contains(wt));
            result.AddRange(EnsureWorkTypeCoverage());
            return result;
        }
    }
}
