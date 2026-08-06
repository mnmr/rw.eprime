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
    /// One explicit-generation projection of every mutable pawn fact consumed
    /// by recommendation and capability calculations. WorkRoles state (role
    /// assignments, roles and paths) is deliberately overlaid later so edits
    /// made inside the window can rebuild from this external snapshot.
    internal sealed class PawnExternalSnapshot
    {
        internal static readonly PawnExternalSnapshot Empty = new PawnExternalSnapshot(
            PawnSignalSnapshot.Empty, new PawnView(), WorkTags.None);

        internal PawnExternalSnapshot(PawnSignalSnapshot signals,
            PawnView recommendationFacts, WorkTags disabledWorkTags)
        {
            Signals = signals ?? PawnSignalSnapshot.Empty;
            RecommendationFacts = recommendationFacts ?? new PawnView();
            DisabledWorkTags = disabledWorkTags;
        }

        internal PawnSignalSnapshot Signals { get; }
        internal PawnView RecommendationFacts { get; }
        internal WorkTags DisabledWorkTags { get; }
        internal bool HasRangedWeapon => RecommendationFacts.HasRangedWeapon;
        internal bool CanDo(WorkGiverDef giver) => giver != null
            && (giver.workType == null || RecommendationFacts.CapableWorkTypes
                .Contains(giver.workType.defName))
            && (DisabledWorkTags & giver.workTags) == WorkTags.None;
    }

    /// Projects game state into the Core recommendation engine's views and
    /// resolves the content-based special roles (Hunter, fire blocker). Pure
    /// projection: no UI state, no commands. Callers own caching (the colony
    /// plan snapshot keys on UiVersion).
    public static class RecsAdapter
    {
        /// Genes that make a pawn terrified of fire (Biotech's pyrophobia;
        /// extend here if mods add equivalents).
        internal static readonly HashSet<string> FireFearGenes = new HashSet<string> { "FireTerror" };

        public static ColonyView BuildColonyView(RoleStore store, List<Pawn> pawns)
            => BuildColonyView(store, pawns, pawn => CapturePawnSnapshot(
                pawn, PawnSignalSnapshots.Build(pawn)));

        internal static ColonyView BuildColonyView(
            RoleStore store,
            List<Pawn> pawns,
            Func<Pawn, PawnExternalSnapshot> snapshotFor)
        {
            if (snapshotFor == null) throw new ArgumentNullException(nameof(snapshotFor));
            List<PathView> paths = store.trainingPaths
                .Select(PathViewOf)
                .ToList();
            RecommendationCatalogProjection catalog = BuildRoleCatalog(
                store.roles, paths, store);
            var pawnViews = new List<PawnView>(pawns.Count);
            foreach (var pawn in pawns)
                pawnViews.Add(PawnViewOf(
                    pawn, store, snapshotFor(pawn)));
            List<int> orderTemplate = OrderTemplate.ResolveTemplate(
                store.recommendationOrder, catalog.Roles);
            return catalog.CreateColony(orderTemplate, pawnViews);
        }

        internal static RoleView RoleViewOf(Role role) =>
            BuildRoleCatalog(new[] { role }).Roles[0];

        internal static List<RoleView> RoleViewsOf(IReadOnlyList<Role> roles)
            => BuildRoleCatalog(roles).Roles.ToList();

        private static RecommendationCatalogProjection BuildRoleCatalog(
            IReadOnlyList<Role> roles,
            IReadOnlyList<PathView> paths = null,
            RoleStore store = null)
        {
            var sources = new List<RecommendationRoleSource>(roles.Count);
            for (int index = 0; index < roles.Count; index++)
            {
                Role role = roles[index];
                RoleDef template = role.templateDefName == null
                    ? null
                    : DefDatabase<RoleDef>.GetNamedSilentFail(
                        role.templateDefName);
                sources.Add(new RecommendationRoleSource
                {
                    Id = role.id,
                    Entries = new List<JobEntry>(role.entries),
                    AutoAssign = role.autoAssign,
                    HasRules = role.HasRules,
                    Blocker = role.blocker,
                    PreserveRecommendationOrder =
                        template?.preserveRecommendationOrder == true,
                    UsesOccasionalRepeatChampionPenalty = template?
                        .usesOccasionalRepeatChampionPenalty == true,
                    HolderMode = role.holderMode,
                    Scale = (store ?? RoleStore.Current)?.ScaleFor(role),
                    RequiredTotal = role.ResolvedRequiredTotal(),
                    MaxHolders = role.ResolvedMaxHolders(),
                    TrainingWaivers = role.ResolvedTrainingWaivers(),
                    Available = RoleAvailable(role),
                    Enabled = role.enabled,
                    SpecialRole = template?.recommendationSpecialRole
                        ?? RecommendationSpecialRoleKind.None,
                });
            }
            Dictionary<string, int> naturalPriorities = DefDatabase<WorkTypeDef>
                .AllDefsListForReading.ToDictionary(
                    workType => workType.defName,
                    workType => workType.naturalPriority);
            return RecommendationCatalogBuilder.Build(
                sources,
                paths ?? Array.Empty<PathView>(),
                GameJobCatalog.Instance,
                naturalPriorities,
                JobSkillProfiles.RecommendationIndex());
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
            => PawnViewOf(pawn, store, CapturePawnSnapshot(
                pawn, PawnSignalSnapshots.Build(pawn)));

        internal static PawnView PawnViewOf(
            Pawn pawn,
            RoleStore store,
            PawnExternalSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            PawnView facts = snapshot.RecommendationFacts;
            var view = new PawnView
            {
                BiologicalAgeTicks = facts.BiologicalAgeTicks,
                HasRangedWeapon = facts.HasRangedWeapon,
                ShootingLevel = facts.ShootingLevel,
                FireFear = facts.FireFear,
                SkillLevels = new Dictionary<string, int>(facts.SkillLevels),
                CapableWorkTypes = new HashSet<string>(facts.CapableWorkTypes),
            };
            PawnSignalViewProjection.Apply(snapshot.Signals, view);
            if (store.pawnSets.TryGetValue(pawn, out var set))
                foreach (var a in set.assignments)
                    view.Existing.Add(new AssignmentView
                    // ForceOn counts as enabled: the engine judges the role's
                    // global toggle separately.
                    { RoleId = a.roleId, Enabled = a.state != AssignmentState.Disabled,
                        Pinned = a.pinned });
            return view;
        }

        /// The only live-pawn recommendation/capability read. The window calls
        /// this eagerly for its complete pawn cohort when opening or handling an
        /// explicit UiVersion refresh; later calculations consume the result.
        internal static PawnExternalSnapshot CapturePawnSnapshot(
            Pawn pawn, PawnSignalSnapshot signalSnapshot)
        {
            if (pawn == null) return PawnExternalSnapshot.Empty;
            signalSnapshot = signalSnapshot ?? PawnSignalSnapshot.Empty;
            var facts = new PawnView
            {
                BiologicalAgeTicks = pawn.ageTracker?.AgeBiologicalTicks ?? long.MaxValue,
                HasRangedWeapon = pawn.equipment?.Primary?.def?.IsRangedWeapon == true,
                ShootingLevel = pawn.skills?.GetSkill(SkillDefOf.Shooting)?.Level ?? 0,
                FireFear = pawn.genes != null
                    && pawn.genes.GenesListForReading.Any(g =>
                        FireFearGenes.Contains(g.def.defName)),
            };
            if (pawn.skills != null)
                foreach (SkillRecord skill in pawn.skills.skills)
                {
                    if (skill.TotallyDisabled) continue;
                    facts.SkillLevels[skill.def.defName] = skill.Level;
                }
            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                if (!pawn.WorkTypeIsDisabled(workType))
                    facts.CapableWorkTypes.Add(workType.defName);

            return new PawnExternalSnapshot(
                signalSnapshot, facts, pawn.CombinedDisabledWorkTags);
        }

        /// The recommendation order template resolved over the live catalog.
        internal static List<int> ResolvedRecommendationOrder(RoleStore store)
            => OrderTemplate.ResolveTemplate(
                store.recommendationOrder, RoleViewsOf(store.roles));

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

    }
}
