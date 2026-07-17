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

            // Groups first, in authored order — roles then land in them by label.
            foreach (var groupDef in DefDatabase<RoleGroupDef>.AllDefsListForReading
                         .OrderBy(d => d.order))
                RoleCommands.EnsureGroup(groupDef.label);
            foreach (var def in defs)
                RoleCommands.CreateRoleFromDef(def);
            // Second pass: train targets may reference defs seeded later.
            foreach (var role in store.roles)
                RoleCommands.ResolveTemplateTrainTargets(role);
            store.seeded = true;
            SeedTrainingPaths(store);

            // Role ranks read as numbers; default vanilla's per-save "manual
            // priorities" switch on at adoption. Applied once — turning it off
            // in Options afterwards sticks. Direct write, not the SyncMethod:
            // seeding already runs in the synced simulation on every client.
            var playSettings = Current.Game?.playSettings;
            if (playSettings != null && !playSettings.useWorkPriorities)
            {
                playSettings.useWorkPriorities = true;
                foreach (var pawn in PawnsFinder.AllMapsWorldAndTemporary_Alive)
                    if (pawn.Faction == Faction.OfPlayer && pawn.workSettings != null)
                        pawn.workSettings.Notify_UseWorkPrioritiesChanged();
            }

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
                        // Invisible modded types can legitimately drop (no catch-all
                        // role); the unused-jobs warning surfaces them instead.
                        if (workType != null && !workType.visible) continue;
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

        /// Runs only alongside role seeding (members resolve by RoleDef template):
        /// older saves adopt the paths via Restore Defaults — auto-seeding there
        /// could duplicate player-made paths.
        private static void SeedTrainingPaths(RoleStore store)
        {
            if (store.pathsSeeded) return;
            foreach (var def in DefDatabase<TrainingPathDef>.AllDefsListForReading
                         .OrderBy(d => d.order))
                CreatePathFromDef(store, def);
            store.pathsSeeded = true;
        }

        /// RoleDef reference -> live role: template link first, then a unique
        /// case-insensitive match on the def's label (survives player renames).
        private static Role ResolvePathRole(RoleStore store, string roleDefName)
        {
            if (roleDefName.NullOrEmpty()) return null;
            var role = store.RoleByTemplate(roleDefName);
            if (role != null) return role;
            string label = DefDatabase<RoleDef>.GetNamedSilentFail(roleDefName)?.label;
            if (label.NullOrEmpty()) return null;
            Role match = null;
            foreach (var candidate in store.roles)
                if (string.Equals(candidate.label, label, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (match != null) return null; // ambiguous: no match
                    match = candidate;
                }
            return match;
        }

        /// Builds one path from its def, skipping entries whose role is missing
        /// (DLC/mod gated); null when fewer than 2 entries resolve (a 0-1 role
        /// path is a no-op) or bands are invalid.
        internal static TrainingPath CreatePathFromDef(RoleStore store, TrainingPathDef def)
        {
            var roleIds = new List<int>();
            var mins = new List<int>();
            var maxes = new List<int>();
            var unresolved = new List<string>();
            foreach (var entry in def.entries)
            {
                if (entry.role.NullOrEmpty()) continue;
                var role = ResolvePathRole(store, entry.role);
                if (role == null) { unresolved.Add(entry.role); continue; }
                if (roleIds.Contains(role.id)) continue;
                roleIds.Add(role.id);
                mins.Add(entry.min);
                maxes.Add(entry.max);
            }
            if (roleIds.Count < 2)
            {
                Log.Message($"[WorkRoles] training path '{def.label}' not created: "
                    + $"only {roleIds.Count} role(s) resolved (unresolved: {unresolved.ToCommaList()})");
                return null;
            }
            if (!SkillProgressionMath.Validate(roleIds.Count, mins, maxes))
            {
                // Load sanitize would silently drop it next session — refuse loudly.
                Log.Warning($"[WorkRoles] TrainingPathDef {def.defName}: invalid bands; skipped");
                return null;
            }
            var path = new TrainingPath
            {
                id = store.NextPathId(),
                name = def.label,
                roleIds = roleIds,
                bandMins = mins,
                bandMaxes = maxes,
                anchorRoleId = ResolvePathRole(store, def.anchorRole)?.id ?? -1,
                anchorBefore = def.anchorBefore,
            };
            if (!def.colorRef.NullOrEmpty())
            {
                var palette = DefDatabase<PaletteDef>.GetNamedSilentFail(def.colorRef);
                if (palette != null)
                {
                    path.hasCustomColor = true;
                    path.color = palette.color;
                }
            }
            store.trainingPaths.Add(path);
            return path;
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
                store.roles.Select(r => new MigrationRole(r.id, MigratableEntries(r), r.blocker)).ToList(),
                CapablePriorities(pawn),
                DefDatabase<WorkTypeDef>.AllDefsListForReading.Select(wt => wt.defName).ToList(),
                GameJobCatalog.Instance);
            if (plan.Count == 0) return false;

            foreach (var roleId in plan)
                RoleCommands.AssignRoleDirect(pawn, roleId);
            return true;
        }

        /// Entries whose work type resolves and is visible: invisible modded
        /// types (e.g. Allow Tool's FinishingOff) never appear in the vanilla
        /// grid, so they must not disqualify a role's visible types as foreign.
        private static IReadOnlyList<JobEntry> MigratableEntries(Role role) =>
            role.entries.Where(e =>
            {
                var type = e.Kind == JobEntryKind.WorkType
                    ? DefDatabase<WorkTypeDef>.GetNamedSilentFail(e.DefName)
                    : DefDatabase<WorkGiverDef>.GetNamedSilentFail(e.DefName)?.workType;
                return type != null && type.visible;
            }).ToList();

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

        /// Index in the role where a work type belongs by naturalPriority
        /// (before the first entry of lower effective priority), so
        /// everyone-types slot where vanilla's Work tab would put them
        /// instead of trailing the role.
        private static int NaturalInsertIndex(Role role, WorkTypeDef workType)
        {
            for (int i = 0; i < role.entries.Count; i++)
            {
                var entry = role.entries[i];
                var type = entry.Kind == WorkRoles.Core.JobEntryKind.WorkType
                    ? DefDatabase<WorkTypeDef>.GetNamedSilentFail(entry.DefName)
                    : DefDatabase<WorkGiverDef>.GetNamedSilentFail(entry.DefName)?.workType;
                if (type != null && type.naturalPriority < workType.naturalPriority)
                    return i;
            }
            return role.entries.Count;
        }

        /// Stable string hash (FNV-1a): string.GetHashCode is not guaranteed
        /// identical across runtimes, and seeded colors and def fingerprints
        /// must match in MP.
        internal static uint Fnv1a(string text)
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

        /// Snaps an arbitrary color to the nearest editor swatch (RGB distance)
        /// — the exact values the palette grid highlights against, and a wider
        /// gamut than the shipped PaletteDefs.
        private static UnityEngine.Color NearestPaletteColor(UnityEngine.Color target)
        {
            var best = new UnityEngine.Color(0.200f, 0.255f, 0.333f);
            float bestDist = float.MaxValue;
            foreach (var swatch in SwatchPalette.Swatches)
            {
                float dr = swatch.r - target.r;
                float dg = swatch.g - target.g;
                float db = swatch.b - target.b;
                float dist = dr * dr + dg * dg + db * db;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = swatch;
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
                        new WorkRoles.Core.JobEntry(WorkRoles.Core.JobEntryKind.WorkType, workType.defName),
                        NaturalInsertIndex(basics, workType));
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
                // Invisible uncovered types stay uncovered (only marked known);
                // the Roles tab's unused-jobs warning surfaces them.
            }

            // Return distinct labels in encounter order.
            var seen = new HashSet<string>();
            var distinct = new List<string>();
            foreach (var label in result)
                if (seen.Add(label))
                    distinct.Add(label);
            return distinct;
        }

        /// Saves from 1.1.2 and earlier can carry empty role sets (last role removed
        /// or deleted), whose save-time fallback sync zeroed the pawn's real vanilla
        /// priorities — the pawn idled with no work enabled. Drops the sets and
        /// re-initializes any pawn the wipe left with nothing enabled. Runs in
        /// FinalizeInit: map pawns aren't loaded yet at world PostLoadInit, and
        /// ProgramState isn't Playing yet so EnableAndInitialize's auto-assign
        /// postfix stays inert.
        public static void SweepEmptyRoleSets()
        {
            var store = RoleStore.Current;
            if (store == null) return;
            var empty = store.pawnSets
                .Where(kv => kv.Value.assignments.Count == 0)
                .Select(kv => kv.Key).ToList();
            foreach (var pawn in empty)
            {
                store.pawnSets.Remove(pawn);
                CompiledJobOrders.Invalidate(pawn);
                var workSettings = pawn?.workSettings;
                if (pawn == null || pawn.Destroyed || pawn.Dead
                    || workSettings == null || !workSettings.EverWork) continue;
                bool anyEnabled = DefDatabase<WorkTypeDef>.AllDefsListForReading
                    .Any(wt => workSettings.GetPriority(wt) > 0);
                if (!anyEnabled)
                {
                    workSettings.EnableAndInitialize();
                    Log.Message($"[WorkRoles] restored work priorities of {pawn.LabelShort} (zeroed by empty role set)");
                }
            }
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

        /// One selectable line in the Restore Defaults preview. Exactly one of the
        /// payload fields is set: a missing template to recreate, an uncovered work
        /// type to regenerate, a role whose snapshots gain moved vanilla givers,
        /// a role whose group or color drifted from its def, a missing default
        /// training path, or the recommendation-order reset.
        public class RestoreItem
        {
            public string label;
            /// What applying this item does (preview row tooltip).
            public string explanation;
            public string templateDef;
            public string workType;
            public int backfillRoleId = -1;
            public int groupRoleId = -1;
            public int colorRoleId = -1;
            public string pathDef;
            public bool recommendationOrder;

            /// Applying would undo a deliberate player change (drift/opt-out
            /// types): the preview highlights these with a warning tint.
            public bool UndoesUserChange =>
                groupRoleId != -1 || colorRoleId != -1 || recommendationOrder;
        }

        /// The role's def-declared group differs from where it sits now, by
        /// label (empty def group = Default).
        private static bool GroupDrifted(RoleStore store, Role role, RoleDef def)
        {
            if (def.group.NullOrEmpty())
                return role.groupId != RoleGroup.DefaultId;
            var current = store.GroupById(role.groupId);
            return current == null || !string.Equals(current.label, def.group.Trim(),
                System.StringComparison.OrdinalIgnoreCase);
        }

        private static string DefGroupLabel(RoleDef def) => def.group.NullOrEmpty()
            ? "WR_GroupDefault".Translate().ToString() : def.group.Trim();

        /// The role's color differs from what its def resolves today.
        private static bool ColorDrifted(Role role, RoleDef def)
        {
            var (has, color) = def.ResolvedColor();
            if (has != role.hasCustomColor) return true;
            return has && !role.color.IndistinguishableFrom(color);
        }

        /// Everything Restore Defaults could do right now: recreate missing
        /// template roles and default training paths, regenerate coverage for work
        /// types nothing covers, recover vanilla jobs that mods moved out of roles'
        /// work types, return drifted roles to their def's group and color, and
        /// reset the recommendation order.
        public static List<RestoreItem> ComputeRestoreItems()
        {
            var store = RoleStore.Current;
            var result = new List<RestoreItem>();
            if (store == null) return result;

            // Coverage items come from LIVE roles only: a missing seeded def's
            // work types stay listed, so declining its role item still offers a
            // generated role. Applying both is safe — RestoreSelected creates
            // roles first, then coverage skips types they cover.
            var covered = CoveredWorkTypes(store);
            foreach (var def in DefDatabase<RoleDef>.AllDefsListForReading)
            {
                if (store.RoleByTemplate(def.defName) != null) continue;
                bool labelTaken = store.roles.Any(r => string.Equals(r.label, def.label,
                    System.StringComparison.OrdinalIgnoreCase));
                result.Add(new RestoreItem
                {
                    label = labelTaken
                        ? def.label + " " + "WR_RestoreDuplicateHint".Translate()
                        : def.label,
                    explanation = "WR_RestoreExplainRole".Translate(),
                    templateDef = def.defName,
                });
            }
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                if (workType.visible && !covered.Contains(workType.defName))
                    result.Add(new RestoreItem
                    {
                        label = (workType.gerundLabel ?? workType.labelShort ?? workType.defName).CapitalizeFirst(),
                        explanation = "WR_RestoreExplainCoverage".Translate(),
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
                    explanation = "WR_RestoreExplainBackfill".Translate(count),
                    backfillRoleId = role.id,
                });
            }
            // Seeded roles whose group drifted from their def; restoring moves
            // them back (recreating the group if the player deleted it).
            foreach (var role in store.roles)
            {
                var def = role.templateDefName == null ? null
                    : DefDatabase<RoleDef>.GetNamedSilentFail(role.templateDefName);
                if (def == null || !GroupDrifted(store, role, def)) continue;
                result.Add(new RestoreItem
                {
                    label = "WR_RestoreGroupItem".Translate(role.label, DefGroupLabel(def)),
                    explanation = "WR_RestoreExplainGroup".Translate(DefGroupLabel(def)),
                    groupRoleId = role.id,
                });
            }
            // Seeded roles whose color drifted from their def; restoring recolors
            // them back to the def's resolved color.
            foreach (var role in store.roles)
            {
                var def = role.templateDefName == null ? null
                    : DefDatabase<RoleDef>.GetNamedSilentFail(role.templateDefName);
                if (def == null || !ColorDrifted(role, def)) continue;
                result.Add(new RestoreItem
                {
                    label = "WR_RestoreColorItem".Translate(role.label),
                    explanation = "WR_RestoreExplainColor".Translate(),
                    colorRoleId = role.id,
                });
            }
            // Default paths missing by NAME (deleted or pre-paths save); existing
            // same-name paths are the player's and stay untouched. Paths that
            // would resolve < 2 entries even after the missing-role items apply
            // are suppressed — they could never be created (perpetual no-ops).
            foreach (var def in DefDatabase<TrainingPathDef>.AllDefsListForReading
                         .OrderBy(d => d.order))
                if (!store.trainingPaths.Any(p => string.Equals(p.name, def.label,
                        System.StringComparison.OrdinalIgnoreCase))
                    && RestorablePathEntryCount(store, def) >= 2)
                    result.Add(new RestoreItem
                    {
                        label = "WR_RestorePathItem".Translate(def.label),
                        explanation = "WR_RestoreExplainPath".Translate(),
                        pathDef = def.defName,
                    });
            if (store.recommendationOrder.Count > 0)
                result.Add(new RestoreItem
                {
                    label = "WR_RestoreRecOrder".Translate(),
                    explanation = "WR_RestoreExplainRecOrder".Translate(),
                    recommendationOrder = true,
                });
            return result;
        }

        /// Entries a Restore Defaults pass could resolve for this path def:
        /// live-resolvable roles plus missing seeded defs (offered as their own
        /// restore items, which apply before paths).
        private static int RestorablePathEntryCount(RoleStore store, TrainingPathDef def)
        {
            var counted = new HashSet<string>();
            int count = 0;
            foreach (var entry in def.entries)
            {
                if (entry.role.NullOrEmpty() || !counted.Add(entry.role)) continue;
                if (ResolvePathRole(store, entry.role) != null
                    || (DefDatabase<RoleDef>.GetNamedSilentFail(entry.role) != null
                        && store.RoleByTemplate(entry.role) == null))
                    count++;
            }
            return count;
        }

        /// Moved-giver detection lives in Core (WorkTypeCoverage) with tests.
        private static Dictionary<string, List<string>> MovedVanillaGiversFor(Role role) =>
            WorkTypeCoverage.MovedGivers(role.entries, role.workTypeSnapshots,
                VanillaGiverBaseline.GiverWorkType, GameJobCatalog.Instance);

        /// Applies the selected restore items. Each application self-guards against
        /// staleness (an already-present template, path name or covered work type
        /// no-ops). Returns labels of what was actually restored.
        public static List<string> RestoreSelected(RestoreSelection selection)
        {
            var store = RoleStore.Current;
            var result = new List<string>();
            if (store == null || selection == null) return result;
            var templateDefs = selection.templateDefs;
            var workTypes = selection.workTypes;
            var backfillRoleIds = selection.backfillRoleIds;
            var pathDefs = selection.pathDefs;
            var groupRoleIds = selection.groupRoleIds;
            var colorRoleIds = selection.colorRoleIds;

            if (templateDefs != null)
            {
                var restored = new List<Role>();
                foreach (var defName in templateDefs)
                {
                    if (store.RoleByTemplate(defName) != null) continue;
                    var role = RoleCommands.CreateRoleFromDef(DefDatabase<RoleDef>.GetNamedSilentFail(defName));
                    if (role != null) { result.Add(role.label); restored.Add(role); }
                }
                // Only the restored roles get their def targets resolved —
                // existing roles' (possibly player-edited) targets stay put.
                foreach (var role in restored)
                    RoleCommands.ResolveTemplateTrainTargets(role);
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

            if (groupRoleIds != null)
            {
                bool anyMoved = false;
                foreach (var roleId in groupRoleIds)
                {
                    var role = store.RoleById(roleId);
                    var def = role?.templateDefName == null ? null
                        : DefDatabase<RoleDef>.GetNamedSilentFail(role.templateDefName);
                    if (def == null || !GroupDrifted(store, role, def)) continue;
                    role.groupId = RoleCommands.EnsureGroup(def.group).id;
                    result.Add("WR_RestoreGroupItem".Translate(role.label, DefGroupLabel(def)));
                    anyMoved = true;
                }
                if (anyMoved) RoleCommands.SweepEmptyGroups();
            }

            if (colorRoleIds != null)
                foreach (var roleId in colorRoleIds)
                {
                    var role = store.RoleById(roleId);
                    var def = role?.templateDefName == null ? null
                        : DefDatabase<RoleDef>.GetNamedSilentFail(role.templateDefName);
                    if (def == null || !ColorDrifted(role, def)) continue;
                    var (has, color) = def.ResolvedColor();
                    role.hasCustomColor = has;
                    role.color = color;
                    result.Add("WR_RestoreColorItem".Translate(role.label));
                }

            // After the roles: a path's members may have been restored just above.
            if (pathDefs != null)
                foreach (var defName in pathDefs)
                {
                    var def = DefDatabase<TrainingPathDef>.GetNamedSilentFail(defName);
                    if (def == null) continue;
                    if (store.trainingPaths.Any(p => string.Equals(p.name, def.label,
                            System.StringComparison.OrdinalIgnoreCase))) continue;
                    var path = CreatePathFromDef(store, def);
                    if (path != null) result.Add(path.name);
                }

            if (selection.recommendationOrder && store.recommendationOrder.Count > 0)
            {
                // Empty = the derived default order.
                store.recommendationOrder = new List<int>();
                result.Add("WR_RestoreRecOrder".Translate());
            }
            return result;
        }
    }
}
