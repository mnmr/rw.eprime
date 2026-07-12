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

            var generated = EnsureWorkTypeCoverage();

            int assigned = 0;
            var failures = new List<string>();
            foreach (var pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive)
            {
                try
                {
                    // Capture the pre-migration grid: after the first assignment the
                    // pawn is managed and priority reads answer from roles.
                    var before = pawn.IsColonist || pawn.IsSlaveOfColony ? CapablePriorities(pawn) : null;
                    if (!TryAssignRolesFromVanillaPriorities(pawn)) continue;
                    assigned++;

                    // Self-check: every work type the pawn had enabled must survive
                    // migration. A drop here is a catalog/planner bug — scream.
                    foreach (var pair in before)
                    {
                        if (pair.Value == 0) continue;
                        // Giver-less work types (Patient, Bed rest) never rank in the
                        // compiled order; they can't be checked this way.
                        if (!GameJobCatalog.Instance.WorkGiversOf(pair.Key).Any()) continue;
                        var workType = DefDatabase<WorkTypeDef>.GetNamedSilentFail(pair.Key);
                        if (workType != null && CompiledJobOrders.PriorityFor(pawn, workType) == 0)
                        {
                            Log.Error($"[WorkRoles] migration dropped {pair.Key} (was priority {pair.Value}) for {pawn.LabelShort}");
                            failures.Add("WR_SeedDropFailure".Translate(
                                pawn.LabelShort, workType.labelShort ?? pair.Key, pair.Value));
                        }
                    }
                }
                catch (System.Exception e)
                {
                    // One corrupt pawn must not abort migration for the rest.
                    Log.Error($"[WorkRoles] failed to migrate priorities of {pawn?.LabelShort ?? "unknown pawn"}: {e}");
                    failures.Add("WR_SeedPawnFailure".Translate(
                        pawn?.LabelShort ?? "?", e.Message));
                }
            }

            Log.Message($"[WorkRoles] seeded {store.roles.Count} roles, assigned role sets to {assigned} pawns");
            ShowSeedReport(store.roles.Count, assigned, generated, failures);
        }

        /// Once-per-save seeding summary. Adding the mod to an existing save
        /// always reports (the player should see what migration did); a fresh
        /// game only surfaces failures — seeding is the expected path there.
        /// Client-local UI: the report strings are never stored or synced.
        private static void ShowSeedReport(int roleCount, int assigned,
            List<string> generated, List<string> failures)
        {
            bool newGame = Find.TickManager.TicksGame == 0;
            if (newGame && failures.Count == 0) return;
            var body = new System.Text.StringBuilder();
            body.Append("WR_SeedReportBody".Translate(roleCount, assigned));
            if (generated.Count > 0)
                body.Append("\n\n").Append("WR_SeedReportGenerated".Translate(generated.ToCommaList()));
            if (failures.Count > 0)
            {
                body.Append("\n\n<color=#ff6666>").Append("WR_SeedReportFailures".Translate());
                foreach (var failure in failures)
                    body.Append("\n  - ").Append(failure);
                body.Append("</color>");
            }
            // Deferred like the SetPriority watcher: seeding runs during load,
            // the dialog must appear once loading ends.
            LongEventHandler.ExecuteWhenFinished(() =>
                Find.WindowStack.Add(new Dialog_MessageBox(body.ToString(),
                    title: "WR_SeedReportTitle".Translate())));
        }

        /// Derives a pawn's role set from its vanilla work priorities via the
        /// Core MigrationPlanner (see its doc for the rules — the planner is
        /// unit-tested against the shipped Roles.xml). Must read priorities BEFORE
        /// assigning anything: an unmanaged pawn's GetPriority passes through to
        /// vanilla values; the first assignment makes the pawn managed and reads
        /// then return WorkRoles ranks.
        public static bool TryAssignRolesFromVanillaPriorities(Pawn pawn)
        {
            var store = RoleStore.Current;
            if (store == null || !store.seeded) return false;
            if (pawn == null || !(pawn.IsColonist || pawn.IsSlaveOfColony)) return false;
            if (store.IsManaged(pawn)) return false;

            var plan = MigrationPlanner.Plan(
                store.roles.Select(r => new MigrationRole(r.id, r.entries, r.blocker || r.managed)).ToList(),
                CapablePriorities(pawn),
                DefDatabase<WorkTypeDef>.AllDefsListForReading.Select(wt => wt.defName).ToList(),
                GameJobCatalog.Instance);
            if (plan.Count == 0) return false;

            foreach (var roleId in plan)
                RoleCommands.AssignRoleDirect(pawn, roleId);
            // The planner excludes the managed role, but invisible modded work
            // (e.g. Allow Tool's Finish Off) was active in vanilla and must keep
            // running — every migrated pawn carries Odd Jobs.
            AssignOddJobs(pawn);
            return true;
        }

        /// Assigns Odd Jobs below the everyone tier (Basics and other auto-assign
        /// roles) and doctoring, above vocations: its jobs (finish off, urgent
        /// hauls) are short, urgent work — but never ahead of the essentials.
        internal static void AssignOddJobs(Pawn pawn)
        {
            var store = RoleStore.Current;
            var oddJobs = store?.ManagedRole;
            if (oddJobs == null) return;
            var set = store.SetFor(pawn);
            if (set.assignments.Any(a => a.roleId == oddJobs.id)) return;
            RoleCommands.AssignRoleDirect(pawn, oddJobs.id, OddJobsInsertIndex(store, set.assignments));
        }

        /// Creates Odd Jobs if missing; pawns already managed (the mod-added-to-
        /// existing-save load where it appears) get it at its ranked position.
        /// Null while the player-deleted opt-out is in force.
        private static Role EnsureOddJobsRole(RoleStore store)
        {
            var oddJobs = store.ManagedRole;
            if (oddJobs != null) return oddJobs;
            if (store.oddJobsDeleted) return null;
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
                    set.assignments.Insert(OddJobsInsertIndex(store, set.assignments),
                        new RoleAssignment { roleId = oddJobs.id });
            return oddJobs;
        }

        private static int OddJobsInsertIndex(RoleStore store, List<RoleAssignment> assignments)
        {
            int index = 0;
            for (int i = 0; i < assignments.Count; i++)
            {
                var role = store.RoleById(assignments[i].roleId);
                if (role == null || role.managed) continue;
                if (role.autoAssign
                    || role.templateDefName == "WS_Doctor" || role.templateDefName == "WS_Medic")
                    index = i + 1;
            }
            return index;
        }

        /// The pawn's vanilla priorities for CAPABLE work types only (absent key =
        /// incapable, value 0 = capable but unassigned).
        private static Dictionary<string, int> CapablePriorities(Pawn pawn)
        {
            var workSettings = pawn.workSettings;
            bool everWork = workSettings != null && workSettings.EverWork;
            var priorities = new Dictionary<string, int>();
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                if (!pawn.WorkTypeIsDisabled(workType))
                    priorities[workType.defName] = everWork ? workSettings.GetPriority(workType) : 0;
            return priorities;
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
                if (role.autoAssign && !role.managed)
                    RoleCommands.AssignRoleDirect(pawn, role.id);
            }
            AssignOddJobs(pawn); // placed by its rule, not catalog order
        }

        /// Visible modded work types that belong to everyone rather than a vocation:
        /// appended to Basics instead of getting a generated role.
        private static readonly HashSet<string> EveryoneWorkTypes = new HashSet<string>
        {
            "HaulingUrgent", // Allow Tool's "haul urgently"
        };

        /// Stable string hash (FNV-1a): string.GetHashCode is not guaranteed
        /// identical across runtimes, and seeded colors must match in MP.
        private static uint Fnv1a(string text)
        {
            uint hash = 2166136261u;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return hash;
        }

        private static UnityEngine.Color PaletteColor(string defName)
        {
            var def = DefDatabase<PaletteDef>.GetNamedSilentFail(defName);
            return def?.color ?? new UnityEngine.Color(0.200f, 0.255f, 0.333f);
        }

        /// Snaps an arbitrary color to the nearest palette entry (RGB distance).
        private static UnityEngine.Color NearestPaletteColor(UnityEngine.Color target)
        {
            var best = new UnityEngine.Color(0.200f, 0.255f, 0.333f);
            float bestDist = float.MaxValue;
            foreach (var def in DefDatabase<PaletteDef>.AllDefsListForReading)
            {
                float dr = def.color.r - target.r;
                float dg = def.color.g - target.g;
                float db = def.color.b - target.b;
                float dist = dr * dr + dg * dg + db * db;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = def.color;
                }
            }
            return best;
        }

        /// Ensures every work type is reachable through some role. Runs on every load;
        /// each work type is processed once per save (store.knownWorkTypes), so deleting
        /// a generated role sticks. Returns labels of newly generated roles.
        public static List<string> EnsureWorkTypeCoverage()
        {
            var store = RoleStore.Current;
            var result = new List<string>();
            if (store == null || !store.seeded) return result;

            var covered = CoveredWorkTypes(store);

            // Odd Jobs exists from seeding on — a stable anchor for the Locked
            // group and for mods that add/remove hidden jobs mid-game, even with
            // 0 jobs — unless the player deleted it (null then; an opt-out that
            // holds until Restore Roles brings it back).
            var oddJobs = EnsureOddJobsRole(store);

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
                        // Palette colors only, chosen deterministically across MP
                        // clients: Everyone-work stays in Basics' family
                        // (slate-700); other types hash (FNV-1a — stable, unlike
                        // string.GetHashCode) to a hue and snap to the nearest
                        // palette color.
                        role.color = EveryoneWorkTypes.Contains(workType.defName)
                            ? PaletteColor("slate-700")
                            : NearestPaletteColor(UnityEngine.Color.HSVToRGB(
                                Fnv1a(workType.defName) % 360u / 360f, 0.5f, 0.55f));
                        role.hasCustomColor = true;
                        RoleCommands.AddEntryDirect(role.id, new WorkRoles.Core.JobEntry(WorkRoles.Core.JobEntryKind.WorkType, workType.defName));
                        result.Add(label);
                    }
                }
                else if (oddJobs != null)
                {
                    // Invisible work types go to the engine-managed Odd Jobs role: an
                    // ordinary catalog role (reorderable, assignable, auto-assigned to
                    // everyone) whose ENTRIES only the engine writes. With Odd Jobs
                    // deleted the type is only marked known; restoring the role
                    // re-opens these types and collects them again.
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

        /// Union-only snapshot maintenance, every load: remember each giver ever
        /// seen under a role's work-type entries, so jobs a mod later moves to a
        /// different work type stay in the role (compile-time expansion in
        /// CompiledJobOrders via JobOrderCompiler.WithMovedSnapshotGivers).
        public static void RefreshWorkTypeSnapshots()
        {
            var store = RoleStore.Current;
            if (store == null) return;
            bool changed = false;
            foreach (var role in store.roles)
                foreach (var entry in role.entries)
                {
                    if (entry.Kind != JobEntryKind.WorkType) continue;
                    if (!role.workTypeSnapshots.TryGetValue(entry.DefName, out var known))
                        role.workTypeSnapshots[entry.DefName] = known = new List<string>();
                    foreach (var giver in GameJobCatalog.Instance.WorkGiversOf(entry.DefName))
                        if (!known.Contains(giver))
                        {
                            known.Add(giver);
                            changed = true;
                        }
                }
            if (changed) CompiledJobOrders.InvalidateAll();
        }

        /// Coverage math lives in Core (WorkTypeCoverage) with tests.
        private static HashSet<string> CoveredWorkTypes(RoleStore store) =>
            WorkTypeCoverage.CoveredWorkTypes(
                store.roles.Select(r => ((IReadOnlyList<JobEntry>)r.entries, r.blocker)),
                GameJobCatalog.Instance);

        /// One selectable line in the Restore Roles preview. Exactly one of the
        /// payload fields is set: a missing template to recreate, an uncovered work
        /// type to regenerate, a role whose snapshots gain moved vanilla givers, or
        /// the deleted Odd Jobs role to bring back.
        public class RestoreItem
        {
            public string label;
            public string templateDef;
            public string workType;
            public int backfillRoleId = -1;
            public bool oddJobs;
        }

        /// Everything Restore Roles could do right now: recreate missing template
        /// roles, regenerate coverage for work types nothing covers, and recover
        /// vanilla jobs that mods moved out of roles' work types.
        public static List<RestoreItem> ComputeRestoreItems()
        {
            var store = RoleStore.Current;
            var result = new List<RestoreItem>();
            if (store == null) return result;

            if (store.oddJobsDeleted && store.ManagedRole == null)
                result.Add(new RestoreItem { label = "WR_OddJobsRole".Translate(), oddJobs = true });

            var covered = CoveredWorkTypes(store);
            foreach (var def in DefDatabase<RoleDef>.AllDefsListForReading)
            {
                if (store.RoleByTemplate(def.defName) != null) continue;
                result.Add(new RestoreItem { label = def.label, templateDef = def.defName });
                if (!def.blocker) // a blocker's entries are vetoes, not coverage
                    WorkTypeCoverage.AddCoveredEntries(covered, def.ParsedEntries(), GameJobCatalog.Instance);
            }
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                if (workType.visible && !covered.Contains(workType.defName))
                    result.Add(new RestoreItem
                    {
                        label = (workType.gerundLabel ?? workType.labelShort ?? workType.defName).CapitalizeFirst(),
                        workType = workType.defName,
                    });
            foreach (var role in store.roles)
            {
                var moved = MovedVanillaGiversFor(role);
                if (moved == null) continue;
                int count = moved.Sum(kv => kv.Value.Count);
                result.Add(new RestoreItem
                {
                    label = "WR_RestoreMovedJobs".Translate(role.label, count),
                    backfillRoleId = role.id,
                });
            }
            return result;
        }

        /// Moved-giver detection lives in Core (WorkTypeCoverage) with tests.
        private static Dictionary<string, List<string>> MovedVanillaGiversFor(Role role) =>
            WorkTypeCoverage.MovedGivers(role.entries, role.workTypeSnapshots,
                VanillaGiverBaseline.GiverWorkType, GameJobCatalog.Instance);

        /// Applies the selected restore items. Each application self-guards against
        /// staleness (an already-present template or covered work type no-ops).
        /// Returns labels of what was actually restored.
        public static List<string> RestoreSelected(
            List<string> templateDefs, List<string> workTypes, List<int> backfillRoleIds,
            bool oddJobs = false)
        {
            var store = RoleStore.Current;
            var result = new List<string>();
            if (store == null) return result;

            if (oddJobs && store.oddJobsDeleted)
            {
                store.oddJobsDeleted = false;
                // Re-open the invisible work types skipped while deleted, then let
                // coverage recreate the role, its entries and its assignments.
                var stillCovered = CoveredWorkTypes(store);
                store.knownWorkTypes.RemoveAll(wt =>
                {
                    var def = DefDatabase<WorkTypeDef>.GetNamedSilentFail(wt);
                    return def != null && !def.visible && !stillCovered.Contains(wt);
                });
                var labels = EnsureWorkTypeCoverage();
                result.AddRange(labels);
                var managed = store.ManagedRole;
                if (managed != null && !labels.Contains(managed.label))
                    result.Add(managed.label); // recreated with no hidden jobs around
            }

            if (templateDefs != null)
                foreach (var defName in templateDefs)
                {
                    if (store.RoleByTemplate(defName) != null) continue;
                    var role = RoleCommands.CreateRoleFromDef(DefDatabase<RoleDef>.GetNamedSilentFail(defName));
                    if (role != null) result.Add(role.label);
                }

            if (workTypes != null && workTypes.Count > 0)
            {
                var covered = CoveredWorkTypes(store);
                store.knownWorkTypes.RemoveAll(wt => workTypes.Contains(wt) && !covered.Contains(wt));
                result.AddRange(EnsureWorkTypeCoverage());
            }

            if (backfillRoleIds != null)
                foreach (var roleId in backfillRoleIds)
                {
                    var role = store.RoleById(roleId);
                    if (role == null) continue;
                    var moved = MovedVanillaGiversFor(role);
                    if (moved == null) continue;
                    foreach (var kv in moved)
                    {
                        if (!role.workTypeSnapshots.TryGetValue(kv.Key, out var known))
                            role.workTypeSnapshots[kv.Key] = known = new List<string>();
                        known.AddRange(kv.Value);
                    }
                    result.Add("WR_RestoreMovedJobs".Translate(role.label, moved.Sum(kv => kv.Value.Count)));
                    CompiledJobOrders.InvalidateRole(roleId);
                }
            return result;
        }
    }
}
