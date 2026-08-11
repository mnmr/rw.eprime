using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core.Recs
{
    public enum RecommendationSpecialRoleKind
    {
        None,
        Hunter,
        FireBlocker,
    }

    /// Detached role facts supplied by game and offline adapters. Job-derived
    /// recommendation facts are deliberately absent: RecommendationCatalogBuilder
    /// owns those derivations for every consumer.
    public sealed class RecommendationRoleSource
    {
        public int Id;
        public List<JobEntry> Entries = new List<JobEntry>();
        public bool AutoAssign;
        public bool HasRules;
        public bool Blocker;
        public bool PreserveRecommendationOrder;
        /// False = repeat championships use the occasional-work penalty.
        public bool ChampionPenalty = true;
        public RoleCategory Category;
        public RoleTime Time;
        /// Minimum biological age (years) for holding the role; 0 = no gate.
        public int MinAge;
        /// Maximum biological age (years, inclusive) for holding the role; 0 = no gate.
        public int MaxAge;
        /// Authored skill classification; null = no authored data.
        public List<string> DeclaredRequiredSkills;
        public List<string> DeclaredOptionalSkills;
        public HolderScale Scale;
        public ScaleMode Mode = ScaleMode.Skilled;
        public bool Available = true;
        public bool Enabled = true;
        public RecommendationSpecialRoleKind SpecialRole;
    }

    /// Complete role-catalog projection consumed by RecommendationPlan. The
    /// role views and work-type map are owned by this projection.
    public sealed class RecommendationCatalogProjection
    {
        internal RecommendationCatalogProjection(
            List<RoleView> roles,
            List<PathView> paths,
            Dictionary<string, IReadOnlyList<string>> workTypeSkills,
            int hunterRoleId,
            int fireBlockerRoleId)
        {
            Roles = new ReadOnlyCollection<RoleView>(roles);
            Paths = new ReadOnlyCollection<PathView>(paths);
            WorkTypeSkills = new ReadOnlyDictionary<string, IReadOnlyList<string>>(
                workTypeSkills);
            HunterRoleId = hunterRoleId;
            FireBlockerRoleId = fireBlockerRoleId;
        }

        public IReadOnlyList<RoleView> Roles { get; }
        public IReadOnlyList<PathView> Paths { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<string>> WorkTypeSkills { get; }
        public int HunterRoleId { get; }
        public int FireBlockerRoleId { get; }

        public ColonyView CreateColony(
            IEnumerable<int> orderTemplate,
            IEnumerable<PawnView> pawns)
        {
            var colony = new ColonyView
            {
                Roles = new List<RoleView>(Roles),
                Paths = new List<PathView>(Paths),
                OrderTemplate = orderTemplate == null
                    ? new List<int>()
                    : new List<int>(orderTemplate),
                WorkTypeSkills = WorkTypeSkills,
                HunterRoleId = HunterRoleId,
                FireBlockerRoleId = FireBlockerRoleId,
            };
            if (pawns == null) return colony;
            foreach (PawnView pawn in pawns)
            {
                if (pawn == null) continue;
                colony.Pawns.Add(pawn);
                foreach (KeyValuePair<string, int> skill in pawn.SkillLevels)
                    if (!colony.SkillMaxLevels.TryGetValue(
                            skill.Key, out int maximum)
                        || skill.Value > maximum)
                        colony.SkillMaxLevels[skill.Key] = skill.Value;
            }
            return colony;
        }
    }

    /// Shared catalog projection for the game adapter and offline tools.
    public static class RecommendationCatalogBuilder
    {
        public static RecommendationCatalogProjection Build(
            IReadOnlyList<RecommendationRoleSource> sources,
            IReadOnlyList<PathView> paths,
            IJobCatalog jobs,
            IReadOnlyDictionary<string, int> naturalPriorities,
            JobProfileIndex jobProfiles)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            if (jobs == null) throw new ArgumentNullException(nameof(jobs));
            if (naturalPriorities == null)
                throw new ArgumentNullException(nameof(naturalPriorities));
            if (jobProfiles == null)
                throw new ArgumentNullException(nameof(jobProfiles));

            var ownedPaths = new List<PathView>(paths?.Count ?? 0);
            if (paths != null)
                for (int index = 0; index < paths.Count; index++)
                    ownedPaths.Add(CopyPath(paths[index]));
            var views = new List<RoleView>(sources.Count);
            var projections = new List<RecommendationRoleProjection>(sources.Count);
            var scratch = new RoleSkillEvidenceAccumulator();
            for (int index = 0; index < sources.Count; index++)
            {
                RecommendationRoleSource source = sources[index]
                    ?? throw new ArgumentException(
                        "Role sources cannot contain null.", nameof(sources));
                RecommendationRoleProjection projection = Project(
                    source,
                    CoverageMath.CoverageOf(source.Entries, jobs),
                    jobs,
                    naturalPriorities,
                    jobProfiles.Givers,
                    scratch);
                projections.Add(projection);
                views.Add(ViewOf(source, projection, jobs));
            }

            IReadOnlyDictionary<int, HashSet<string>> excludedByRole =
                TrainingRoleSkillRequirements.ExcludedCoverageByRole(
                    views, ownedPaths);
            for (int index = 0; index < sources.Count; index++)
            {
                RecommendationRoleSource source = sources[index];
                if (!excludedByRole.TryGetValue(
                        source.Id, out HashSet<string> excluded))
                    continue;
                var skillCoverage = new HashSet<string>(views[index].Coverage);
                skillCoverage.ExceptWith(excluded);
                RecommendationRoleProjection projection = Project(
                    source,
                    skillCoverage,
                    jobs,
                    naturalPriorities,
                    jobProfiles.Givers,
                    scratch);
                projections[index] = projection;
                views[index] = ViewOf(source, projection, jobs);
            }
            TrainingRoleSkillRequirements.ApplyTargetRequirements(
                views, ownedPaths);

            int hunterRoleId = ResolveHunterRoleId(
                sources, projections);
            int fireBlockerRoleId = ResolveFireBlockerRoleId(
                sources, projections);

            return new RecommendationCatalogProjection(
                views,
                ownedPaths,
                WorkTypeSkills(jobProfiles.WorkTypes),
                hunterRoleId,
                fireBlockerRoleId);
        }

        private static RecommendationRoleProjection Project(
            RecommendationRoleSource source,
            IEnumerable<string> skillCoverage,
            IJobCatalog jobs,
            IReadOnlyDictionary<string, int> naturalPriorities,
            IReadOnlyDictionary<string, JobProfileGiverFacts> giverFacts,
            RoleSkillEvidenceAccumulator scratch)
        {
            var workTypes = new List<RecommendationWorkTypeEvidence>();
            var literalWorkTypes = new List<string>();
            for (int entryIndex = 0;
                 entryIndex < source.Entries.Count;
                 entryIndex++)
            {
                JobEntry entry = source.Entries[entryIndex];
                string workType;
                if (entry.Kind == JobEntryKind.WorkType)
                {
                    literalWorkTypes.Add(entry.DefName);
                    workType = entry.DefName;
                }
                else
                    workType = jobs.WorkTypeOf(entry.DefName);
                if (workType == null) continue;
                naturalPriorities.TryGetValue(workType, out int priority);
                workTypes.Add(new RecommendationWorkTypeEvidence(
                    workType, priority));
            }
            IReadOnlyList<RoleSkillEvidence> evidence =
                RoleSkillEvidenceSource.ForCoverage(
                    skillCoverage, giverFacts, scratch);
            return new RecommendationRoleProjection(
                workTypes, literalWorkTypes, evidence);
        }

        private static RoleView ViewOf(
            RecommendationRoleSource source,
            RecommendationRoleProjection projection,
            IJobCatalog jobs)
        {
            var view = new RoleView
            {
                Id = source.Id,
                Coverage = CoverageMath.CoverageOf(source.Entries, jobs),
                OrderedCoverage = CoverageMath.OrderedCoverageOf(
                    source.Entries, jobs),
                AutoAssign = source.AutoAssign,
                HasRules = source.HasRules,
                Blocker = source.Blocker,
                Hunting = projection.Hunting,
                PreserveRecommendationOrder =
                    source.PreserveRecommendationOrder,
                ChampionPenalty = source.ChampionPenalty,
                Category = source.Category,
                Time = source.Time,
                MinAge = source.MinAge,
                MaxAge = source.MaxAge,
                DeclaredRequiredSkills = source.DeclaredRequiredSkills == null
                    ? null : new List<string>(source.DeclaredRequiredSkills),
                DeclaredOptionalSkills = source.DeclaredOptionalSkills == null
                    ? null : new List<string>(source.DeclaredOptionalSkills),
                NaturalPriority = projection.MaxNaturalPriority,
                WorkTypes = projection.CopyWorkTypes(),
                Scale = source.Scale?.Copy(),
                Mode = source.Mode,
                Skills = projection.CopySkillViews(),
                PrimarySkill = projection.PrimarySkill,
                Unskilled = !source.AutoAssign
                    && !source.HasRules
                    && !projection.HasSkillEvidence,
                Available = source.Available,
                Enabled = source.Enabled,
            };
            return view;
        }

        private static PathView CopyPath(PathView path)
        {
            if (path == null)
                throw new ArgumentException(
                    "Paths cannot contain null.", nameof(path));
            return new PathView
            {
                Id = path.Id,
                RoleIds = new List<int>(path.RoleIds),
                BandMins = new List<int>(path.BandMins),
                BandMaxes = new List<int>(path.BandMaxes),
            };
        }

        private static Dictionary<string, IReadOnlyList<string>> WorkTypeSkills(
            IReadOnlyDictionary<string, JobProfileWorkTypeFacts> workTypes)
        {
            var result = new Dictionary<string, IReadOnlyList<string>>(
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, JobProfileWorkTypeFacts> pair in workTypes)
                if (pair.Value.RelevantSkillDefNames.Count > 0)
                    result[pair.Key] = new List<string>(
                        pair.Value.RelevantSkillDefNames);
            return result;
        }

        private static int ResolveHunterRoleId(
            IReadOnlyList<RecommendationRoleSource> sources,
            IReadOnlyList<RecommendationRoleProjection> projections)
        {
            for (int index = 0; index < sources.Count; index++)
            {
                RecommendationRoleSource source = sources[index];
                if (source.SpecialRole == RecommendationSpecialRoleKind.Hunter
                    && source.Enabled
                    && !source.HasRules
                    && !source.Blocker)
                    return source.Id;
            }

            RecommendationRoleSource best = null;
            for (int index = 0; index < sources.Count; index++)
            {
                RecommendationRoleSource source = sources[index];
                if (!source.Enabled
                    || source.HasRules
                    || source.Blocker
                    || !projections[index].HasLiteralWorkType("Hunting"))
                    continue;
                if (best == null
                    || source.Entries.Count < best.Entries.Count)
                    best = source;
            }
            return best?.Id ?? -1;
        }

        private static int ResolveFireBlockerRoleId(
            IReadOnlyList<RecommendationRoleSource> sources,
            IReadOnlyList<RecommendationRoleProjection> projections)
        {
            for (int index = 0; index < sources.Count; index++)
            {
                RecommendationRoleSource source = sources[index];
                if (source.SpecialRole
                        == RecommendationSpecialRoleKind.FireBlocker
                    && source.Enabled
                    && !source.HasRules
                    && source.Blocker)
                    return source.Id;
            }

            for (int index = 0; index < sources.Count; index++)
            {
                RecommendationRoleSource source = sources[index];
                if (source.Enabled
                    && !source.HasRules
                    && source.Blocker
                    && projections[index].HasLiteralWorkType("Firefighter"))
                    return source.Id;
            }
            return -1;
        }
    }
}
