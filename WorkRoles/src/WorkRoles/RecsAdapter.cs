using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.Core.Signals;
using WorkRoles.Signals;

namespace WorkRoles
{
    /// Projects game state into the Core recommendation engine's views and
    /// resolves the content-based special roles (Hunter, fire blocker). Pure
    /// projection: no UI state, no commands. Callers own caching (the colony
    /// plan snapshot keys on UiVersion).
    public static class RecsAdapter
    {
        /// Genes that make a pawn terrified of fire (Biotech's pyrophobia;
        /// extend here if mods add equivalents).
        internal static readonly HashSet<string> FireFearGenes = new HashSet<string> { "FireTerror" };
        internal static readonly HashSet<string> FixedRecommendationOrderTemplates =
            new HashSet<string> { "WS_Researcher", "WS_DarkStudier" };

        public static ColonyView BuildColonyView(RoleStore store, List<Pawn> pawns)
            => BuildColonyView(store, pawns, PawnSignalSnapshots.Build);

        internal static ColonyView BuildColonyView(
            RoleStore store,
            List<Pawn> pawns,
            Func<Pawn, PawnSignalSnapshot> snapshotFor)
        {
            if (snapshotFor == null) throw new ArgumentNullException(nameof(snapshotFor));
            var colony = new ColonyView
            {
                Roles = store.roles.Select(RoleViewOf).ToList(),
                Paths = store.trainingPaths.Select(PathViewOf).ToList(),
                WorkTypeSkills = WorkTypeSkillMap(),
                HunterRoleId = RoleProviding(store, "Hunting", "WS_Hunter")?.id ?? -1,
                FireBlockerRoleId = FireBlocker(store)?.id ?? -1,
            };
            colony.OrderTemplate = OrderTemplate.ResolveTemplate(store.recommendationOrder, colony.Roles);
            foreach (var pawn in pawns)
                colony.Pawns.Add(PawnViewOf(pawn, store, snapshotFor(pawn)));
            foreach (var pawn in pawns)
            {
                if (pawn.skills == null) continue;
                foreach (var sr in pawn.skills.skills)
                    if (!colony.SkillMaxLevels.TryGetValue(sr.def.defName, out int max) || sr.Level > max)
                        colony.SkillMaxLevels[sr.def.defName] = sr.Level;
            }
            return colony;
        }

        internal static RoleView RoleViewOf(Role role)
        {
            var skills = RoleSkillProfiles.ForRole(role);
            return new RoleView
            {
                Id = role.id,
                Coverage = role.Coverage(),
                OrderedCoverage = CoverageMath.OrderedCoverageOf(role.entries, GameJobCatalog.Instance),
                AutoAssign = role.autoAssign,
                HasRules = role.HasRules,
                Blocker = role.blocker,
                Hunting = ProvidesHunting(role),
                PreserveRecommendationOrder = FixedRecommendationOrderTemplates
                    .Contains(role.templateDefName),
                NaturalPriority = MaxNaturalPriority(role),
                WorkTypes = WorkTypesOf(role).Select(wt => wt.defName).ToList(),
                HolderMode = role.holderMode,
                MinHolders = role.ResolvedMinHolders(),
                MaxHolders = role.ResolvedMaxHolders(),
                TrainingWaivers = role.ResolvedTrainingWaivers(),
                Skills = skills,
                PrimarySkill = skills.FirstOrDefault(s => s.Primary)?.SkillDefName,
                Unskilled = !role.autoAssign && !role.HasRules && skills.Count == 0,
                Available = RoleAvailable(role),
                Enabled = role.enabled,
            };
        }

        internal static PathView PathViewOf(TrainingPath path) => new PathView
        {
            Id = path.id,
            RoleIds = path.roleIds.ToList(),
            BandMins = path.bandMins.ToList(),
            BandMaxes = path.bandMaxes.ToList(),
            AnchorRoleId = path.anchorRoleId,
            AnchorBefore = path.anchorBefore,
        };

        internal static PawnView PawnViewOf(Pawn pawn, RoleStore store)
            => PawnViewOf(pawn, store, PawnSignalSnapshots.Build(pawn));

        internal static PawnView PawnViewOf(
            Pawn pawn,
            RoleStore store,
            PawnSignalSnapshot signalSnapshot)
        {
            if (signalSnapshot == null)
                throw new ArgumentNullException(nameof(signalSnapshot));
            var view = new PawnView
            {
                BiologicalAgeTicks = pawn.ageTracker?.AgeBiologicalTicks ?? long.MaxValue,
                HasRangedWeapon = pawn.equipment?.Primary?.def?.IsRangedWeapon == true,
                ShootingLevel = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0,
                FireFear = pawn.genes != null
                    && pawn.genes.GenesListForReading.Any(g => FireFearGenes.Contains(g.def.defName)),
            };
            if (pawn.skills != null)
                foreach (var sr in pawn.skills.skills)
                {
                    if (sr.TotallyDisabled) continue;
                    view.SkillLevels[sr.def.defName] = sr.Level;
                }
            foreach (SkillBucketSignal signal in signalSnapshot.SkillBuckets.All)
                view.SignalBuckets[signal.SkillDefName] = signal.Bucket;
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                if (!pawn.WorkTypeIsDisabled(workType))
                    view.CapableWorkTypes.Add(workType.defName);
            if (store.pawnSets.TryGetValue(pawn, out var set))
                foreach (var a in set.assignments)
                    view.Existing.Add(new AssignmentView
                    { RoleId = a.roleId, Enabled = a.enabled, Pinned = a.pinned });
            return view;
        }

        /// The recommendation order template resolved over the live catalog.
        internal static List<int> ResolvedRecommendationOrder(RoleStore store)
            => OrderTemplate.ResolveTemplate(store.recommendationOrder,
                store.roles.Select(RoleViewOf).ToList());

        /// The role's measured skill for band gating: the most XP-frequent
        /// skill across its covered givers (accurate per-giver data), ties
        /// alphabetical; null when no giver trains anything (never gates).
        /// Cached on the role; entry edits invalidate with coverage.
        internal static string PrimarySkillOf(Role role)
        {
            if (role.TryGetPrimarySkillCache(out string cached)) return cached;
            string primary = RoleSkillProfiles.ForRole(role)
                .FirstOrDefault(skill => skill.Primary)?.SkillDefName;
            role.SetPrimarySkillCache(primary);
            return primary;
        }

        /// Work types a role touches: WorkType entries directly, WorkGiver
        /// entries through their parent work type.
        internal static HashSet<WorkTypeDef> WorkTypesOf(Role role)
        {
            var workTypes = new HashSet<WorkTypeDef>();
            foreach (var entry in role.entries)
            {
                if (entry.Kind == JobEntryKind.WorkType)
                {
                    var wt = DefDatabase<WorkTypeDef>.GetNamedSilentFail(entry.DefName);
                    if (wt != null) workTypes.Add(wt);
                }
                else
                {
                    var wg = DefDatabase<WorkGiverDef>.GetNamedSilentFail(entry.DefName);
                    if (wg?.workType != null) workTypes.Add(wg.workType);
                }
            }
            return workTypes;
        }

        /// Distinct relevant skills across a role's member work types.
        internal static List<SkillDef> RelevantSkillsOf(Role role)
        {
            var skills = new List<SkillDef>();
            foreach (var wt in WorkTypesOf(role))
            {
                if (wt.relevantSkills == null) continue;
                foreach (var skillDef in wt.relevantSkills)
                    if (!skills.Contains(skillDef)) skills.Add(skillDef);
            }
            return skills;
        }

        internal static bool IsUnskilledRole(Role role)
            => !role.autoAssign && !role.HasRules
            && RoleSkillProfiles.ForRole(role).Count == 0;

        internal static bool ProvidesHunting(Role role)
            => WorkTypesOf(role).Any(wt => wt.defName == "Hunting");

        /// The highest vanilla work-tab priority among the role's work types.
        internal static int MaxNaturalPriority(Role role)
        {
            int max = 0;
            foreach (var wt in WorkTypesOf(role))
                if (wt.naturalPriority > max) max = wt.naturalPriority;
            return max;
        }

        /// A role is unavailable while every covered giver is bench work
        /// behind unfinished research. Built status is ignored on purpose —
        /// recommendations must not flap per bench built or destroyed.
        internal static bool RoleAvailable(Role role)
        {
            bool sawGiver = false;
            foreach (var giverName in role.Coverage())
            {
                var giver = DefDatabase<WorkGiverDef>.GetNamedSilentFail(giverName);
                if (giver == null) continue;
                sawGiver = true;
                if (giver.fixedBillGiverDefs.NullOrEmpty()) return true;
                foreach (var bench in giver.fixedBillGiverDefs)
                    if (bench != null && bench.IsResearchFinished)
                        return true;
            }
            return !sawGiver;
        }

        /// workType defName -> relevant skill defNames.
        internal static Dictionary<string, IReadOnlyList<string>> WorkTypeSkillMap()
        {
            var map = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                if (workType.relevantSkills != null && workType.relevantSkills.Count > 0)
                    map[workType.defName] = workType.relevantSkills.Select(s => s.defName).ToList();
            return map;
        }

        internal static bool HasWorkTypeEntry(Role role, string workType)
            => role.entries.Any(e => e.Kind == JobEntryKind.WorkType && e.DefName == workType);

        /// The role providing a work type: the shipped template when usable,
        /// else the smallest enabled rule-free role carrying the whole type.
        internal static Role RoleProviding(RoleStore store, string workType, string template)
        {
            var shipped = store.roles.FirstOrDefault(r => r.templateDefName == template);
            if (shipped != null && shipped.enabled && !shipped.HasRules && !shipped.blocker)
                return shipped;
            Role best = null;
            foreach (var role in store.roles)
            {
                if (!role.enabled || role.HasRules || role.blocker
                    || !HasWorkTypeEntry(role, workType)) continue;
                if (best == null || role.entries.Count < best.entries.Count) best = role;
            }
            return best;
        }

        /// The fire blocker: the shipped template when usable, else any
        /// enabled rule-free blocker carrying the Firefighter work type.
        internal static Role FireBlocker(RoleStore store)
        {
            var blocker = store.RoleByTemplate("WS_NoFirefighting");
            if (blocker != null && (!blocker.enabled || blocker.HasRules || !blocker.blocker))
                blocker = null;
            return blocker ?? store.roles.FirstOrDefault(r =>
                r.enabled && !r.HasRules && r.blocker && HasWorkTypeEntry(r, "Firefighter"));
        }
    }
}
