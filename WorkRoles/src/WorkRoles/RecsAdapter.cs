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
        private static readonly Dictionary<string, int> NoAgeBlocks =
            new Dictionary<string, int>();

        internal static readonly PawnExternalSnapshot Empty = new PawnExternalSnapshot(
            PawnSignalSnapshot.Empty, new PawnView(), WorkTags.None, null);

        internal PawnExternalSnapshot(PawnSignalSnapshot signals,
            PawnView recommendationFacts, WorkTags disabledWorkTags,
            Dictionary<string, int> ageBlockedWorkTypes)
        {
            Signals = signals ?? PawnSignalSnapshot.Empty;
            RecommendationFacts = recommendationFacts ?? new PawnView();
            DisabledWorkTags = disabledWorkTags;
            AgeBlockedWorkTypes = ageBlockedWorkTypes ?? NoAgeBlocks;
        }

        internal PawnSignalSnapshot Signals { get; }
        internal PawnView RecommendationFacts { get; }
        internal WorkTags DisabledWorkTags { get; }
        /// Work types this pawn is currently too young for, with the age
        /// (years) each one unlocks at.
        internal IReadOnlyDictionary<string, int> AgeBlockedWorkTypes { get; }
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

        /// Compatibility probe for the game's effective life-stage work rule.
        /// Its impossible vanilla threshold distinguishes ordinary age-gated
        /// pawns from races/mods that make LifeStageWorkSettings non-disabling.
        private static readonly LifeStageWorkSettings AgeApplicabilityProbe =
            new LifeStageWorkSettings { minAge = int.MaxValue };

        public static ColonyView BuildColonyView(RoleStore store, List<Pawn> pawns)
            => BuildColonyView(store, pawns, pawn => CapturePawnSnapshot(
                pawn, PawnSignalSnapshots.Build(pawn)));

        internal static ColonyView BuildColonyView(
            RoleStore store,
            List<Pawn> pawns,
            Func<Pawn, PawnExternalSnapshot> snapshotFor)
        {
            if (snapshotFor == null) throw new ArgumentNullException(nameof(snapshotFor));
            RecommendationCatalogProjection catalog = BuildRoleCatalog(
                store.roles, PathViewsOf(store));
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

        /// Views with the store's training paths applied, so path-target skill
        /// promotions land exactly as they do in the live recommendation run.
        internal static List<RoleView> RoleViewsOf(RoleStore store)
            => BuildRoleCatalog(store.roles, PathViewsOf(store))
                .Roles.ToList();

        private static RecommendationCatalogProjection BuildRoleCatalog(
            IReadOnlyList<Role> roles,
            IReadOnlyList<PathView> paths = null)
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
                    TemplateDefName = role.templateDefName,
                    MemberRoleIds = role.composite
                        ? LiveMemberIdsOf(role) : null,
                    Entries = role.composite
                        ? CompositeEntriesOf(role)
                        : new List<JobEntry>(role.entries),
                    AutoAssign = role.autoAssign,
                    HasRules = role.HasRules,
                    Blocker = role.blocker,
                    PreserveRecommendationOrder =
                        template?.preserveRecommendationOrder == true,
                    // Tuning lives on the role (seeded from the def, migrated
                    // on load); unmigrated roles fall back to defaults.
                    ChampionPenalty = role.championPenalty,
                    Category = role.category,
                    Time = role.time,
                    MinAge = role.minAge < 0 ? 0 : role.minAge,
                    MaxAge = role.maxAge,
                    DeclaredRequiredSkills = role.tuningSeeded
                        ? role.requiredSkills : null,
                    DeclaredOptionalSkills = role.tuningSeeded
                        ? role.optionalSkills : null,
                    ColonyMin = role.colonyMin,
                    Coverage = role.coverage,
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

        /// The role's own training path; PathView.Id is the owning role id.
        internal static PathView PathViewOf(Role role) => new PathView
        {
            Id = role.id,
            RoleIds = role.trainingRoleIds.ToList(),
            BandMins = role.trainingMins.ToList(),
            BandMaxes = role.trainingMaxes.ToList(),
        };

        /// Every non-empty role-owned training path (empty = the implicit
        /// self-only path, which the engine treats as no path).
        internal static List<PathView> PathViewsOf(RoleStore store)
        {
            var paths = new List<PathView>();
            foreach (Role role in store.roles)
                if (role.trainingRoleIds.Count > 0)
                    paths.Add(PathViewOf(role));
            return paths;
        }

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
                AgeLimitsApply = facts.AgeLimitsApply,
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
                AgeLimitsApply = AgeLimitsApplyTo(pawn),
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
            Dictionary<string, int> ageBlocked = null;
            foreach (WorkTypeDef workType in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                if (!pawn.WorkTypeIsDisabled(workType))
                    facts.CapableWorkTypes.Add(workType.defName);
                else if (pawn.IsWorkTypeDisabledByAge(workType, out int minAgeRequired))
                    (ageBlocked ??= new Dictionary<string, int>())
                        [workType.defName] = minAgeRequired;
            }

            return new PawnExternalSnapshot(
                signalSnapshot, facts, pawn.CombinedDisabledWorkTags, ageBlocked);
        }

        private static bool AgeLimitsApplyTo(Pawn pawn)
        {
            List<LifeStageWorkSettings> settings =
                pawn.RaceProps.lifeStageWorkSettings;
            return settings != null && settings.Count > 0
                && AgeApplicabilityProbe.IsDisabled(pawn);
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

        /// The entries a composite projects for recommendations and derived
        /// tuning: its live members' entries in member order. A blocker member
        /// contributes nothing (its jobs are vetoes) unless the composite is
        /// itself a blocker, in which case the whole bundle is the veto.
        internal static List<JobEntry> CompositeEntriesOf(Role role)
        {
            var entries = new List<JobEntry>();
            var store = RoleStore.Current;
            if (store == null) return entries;
            foreach (int memberId in role.memberRoleIds)
            {
                Role member = store.RoleById(memberId);
                if (member == null || member.composite) continue;
                if (member.blocker && !role.blocker) continue;
                entries.AddRange(member.entries);
            }
            return entries;
        }

        /// The members whose entries CompositeEntriesOf projects, as ids, so
        /// Core band gates can hold the bundle to each member's path band.
        private static List<int> LiveMemberIdsOf(Role role)
        {
            var members = new List<int>();
            var store = RoleStore.Current;
            if (store == null) return members;
            foreach (int memberId in role.memberRoleIds)
            {
                Role member = store.RoleById(memberId);
                if (member == null || member.composite) continue;
                if (member.blocker && !role.blocker) continue;
                members.Add(memberId);
            }
            return members;
        }

        /// Work types a role touches: WorkType entries directly, WorkGiver
        /// entries through their parent work type; composites through their
        /// members' entries.
        internal static HashSet<WorkTypeDef> WorkTypesOf(Role role)
        {
            var workTypes = new HashSet<WorkTypeDef>();
            IReadOnlyList<JobEntry> entries = role.composite
                ? CompositeEntriesOf(role) : role.entries;
            foreach (var entry in entries)
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

        /// Lowest vanilla unlock age (years) across the role's covered work
        /// types: the age at which any of the role's work becomes possible.
        /// 0 when nothing is age-gated (always without Biotech). The derived
        /// default for Role.minAge.
        internal static int MinUnlockAgeOf(Role role)
        {
            int min = int.MaxValue;
            foreach (var workType in WorkTypesOf(role))
            {
                int age = WorkTypeUnlockAge(workType);
                if (age < min) min = age;
            }
            return min == int.MaxValue ? 0 : min;
        }

        /// Age (years) at which every covered work type is unlocked. Cached on
        /// the role; entry edits invalidate with coverage.
        internal static int FullyUnlocksAtAgeOf(Role role)
        {
            if (role.TryGetFullyUnlocksAtAgeCache(out int cached)) return cached;
            int max = 0;
            foreach (var workType in WorkTypesOf(role))
            {
                int age = WorkTypeUnlockAge(workType);
                if (age > max) max = age;
            }
            role.SetFullyUnlocksAtAgeCache(max);
            return max;
        }

        /// Known work types missing from vanilla's lifeStageWorkSettings.
        /// Deliberate deviation: these match the vanilla age of the equivalent
        /// listed work (fishing and finish-off like Hunting, urgent hauling
        /// like Hauling) instead of vanilla's effective "no gate".
        private static readonly Dictionary<string, int> CuratedUnlockAges =
            new Dictionary<string, int>(System.StringComparer.Ordinal)
            {
                ["Fishing"] = 7,
                ["FinishingOff"] = 7,
                ["KAU_FinishingOff"] = 7,
                ["HaulingUrgent"] = 3,
                ["KAU_UrgentHaul"] = 3,
            };

        /// Vanilla per-work-type unlock age from the Human race (Biotech's
        /// lifeStageWorkSettings), then the curated table; 0 when unknown or
        /// Biotech is absent.
        internal static int WorkTypeUnlockAge(WorkTypeDef workType)
        {
            List<LifeStageWorkSettings> settings =
                ThingDefOf.Human.race.lifeStageWorkSettings;
            for (int index = 0; index < settings.Count; index++)
                if (settings[index].workType == workType)
                    return settings[index].minAge;
            if (settings.Count > 0
                && CuratedUnlockAges.TryGetValue(workType.defName, out int curated))
                return curated;
            return 0;
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
