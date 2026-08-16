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
        /// Template def the role was seeded from; null for player-made roles.
        public string TemplateDefName;
        /// Live non-composite members of a composite role; null for ordinary roles.
        public List<int> MemberRoleIds;
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
        /// User-authored hard skill gates; null or empty = no additional gate.
        public List<string> DeclaredRequiredSkills;
        /// Authored demand: minimum assignment count and ideal colonist percentage.
        public int ColonyMin;
        public int Coverage;
        public bool Available = true;
        public bool Enabled = true;
        public RecommendationSpecialRoleKind SpecialRole;
    }

    /// Complete role-catalog projection consumed by RecommendationPlan. The
    /// role views and their work specs are owned by this projection.
    public sealed class RecommendationCatalogProjection
    {
        internal RecommendationCatalogProjection(
            List<RoleView> roles,
            List<PathView> paths,
            int hunterRoleId,
            int fireBlockerRoleId)
        {
            Roles = new ReadOnlyCollection<RoleView>(roles);
            Paths = new ReadOnlyCollection<PathView>(paths);
            HunterRoleId = hunterRoleId;
            FireBlockerRoleId = fireBlockerRoleId;
        }

        public IReadOnlyList<RoleView> Roles { get; }
        public IReadOnlyList<PathView> Paths { get; }
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
            for (int index = 0; index < sources.Count; index++)
            {
                RecommendationRoleSource source = sources[index]
                    ?? throw new ArgumentException(
                        "Role sources cannot contain null.", nameof(sources));
                views.Add(ViewOf(
                    source,
                    SpecOf(source, jobs, naturalPriorities, jobProfiles, null),
                    jobs));
            }
            ApplyCompositeSpecs(sources, views);

            IReadOnlyDictionary<int, HashSet<string>> excludedByRole =
                TrainingCoverageExclusion.ExcludedCoverageByRole(
                    views, ownedPaths);
            for (int index = 0; index < sources.Count; index++)
            {
                RecommendationRoleSource source = sources[index];
                if (!excludedByRole.TryGetValue(
                        source.Id, out HashSet<string> excluded))
                    continue;
                views[index].WorkSpec = SpecOf(
                    source, jobs, naturalPriorities, jobProfiles, excluded);
            }
            ApplyCompositeSpecs(sources, views);

            return new RecommendationCatalogProjection(
                views,
                ownedPaths,
                ResolveHunterRoleId(sources, views),
                ResolveFireBlockerRoleId(sources, views));
        }

        private static RoleWorkSpec SpecOf(
            RecommendationRoleSource source,
            IJobCatalog jobs,
            IReadOnlyDictionary<string, int> naturalPriorities,
            JobProfileIndex jobProfiles,
            ISet<string> excludedProfileGivers)
        {
            var seedWorkTypes = new List<string>();
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
                if (workType != null) seedWorkTypes.Add(workType);
            }
            return RoleWorkSpecBuilder.Build(
                source.Id,
                CoverageMath.OrderedCoverageOf(source.Entries, jobs),
                seedWorkTypes,
                literalWorkTypes,
                jobs.WorkTypeOf,
                naturalPriorities,
                jobProfiles.Givers,
                jobProfiles,
                source.DeclaredRequiredSkills,
                excludedProfileGivers);
        }

        private static RoleView ViewOf(
            RecommendationRoleSource source,
            RoleWorkSpec spec,
            IJobCatalog jobs)
        {
            return new RoleView
            {
                Id = source.Id,
                TemplateDefName = source.TemplateDefName,
                MemberRoleIds = source.MemberRoleIds == null
                    ? null : new List<int>(source.MemberRoleIds),
                Coverage = CoverageMath.CoverageOf(source.Entries, jobs),
                OrderedCoverage = CoverageMath.OrderedCoverageOf(
                    source.Entries, jobs),
                AutoAssign = source.AutoAssign,
                HasRules = source.HasRules,
                Blocker = source.Blocker,
                PreserveRecommendationOrder =
                    source.PreserveRecommendationOrder,
                ChampionPenalty = source.ChampionPenalty,
                Category = source.Category,
                Time = source.Time,
                MinAge = source.MinAge,
                MaxAge = source.MaxAge,
                ColonyMin = source.ColonyMin,
                CoveragePercent = source.Coverage,
                Available = source.Available,
                Enabled = source.Enabled,
                WorkSpec = spec,
            };
        }

        /// Composite roles publish the deduplicated union of member givers;
        /// gates union the composite's own and every member's gates.
        private static void ApplyCompositeSpecs(
            IReadOnlyList<RecommendationRoleSource> sources,
            IList<RoleView> views)
        {
            var indexById = new Dictionary<int, int>(sources.Count);
            for (int index = 0; index < sources.Count; index++)
                indexById[sources[index].Id] = index;

            for (int index = 0; index < sources.Count; index++)
            {
                RecommendationRoleSource source = sources[index];
                if (source.MemberRoleIds == null) continue;
                var memberSpecs = new List<RoleWorkSpec>(
                    source.MemberRoleIds.Count);
                var gates = new List<string>();
                var seenGates = new HashSet<string>(StringComparer.Ordinal);
                AddGates(source.DeclaredRequiredSkills, gates, seenGates);
                for (int memberIndex = 0;
                     memberIndex < source.MemberRoleIds.Count;
                     memberIndex++)
                {
                    if (!indexById.TryGetValue(
                            source.MemberRoleIds[memberIndex], out int resolved))
                        continue;
                    memberSpecs.Add(views[resolved].WorkSpec);
                    AddGates(
                        views[resolved].WorkSpec.AssignmentSkillGates,
                        gates,
                        seenGates);
                }
                views[index].WorkSpec = RoleWorkSpecBuilder.Merge(
                    source.Id, memberSpecs, gates);
            }
        }

        private static void AddGates(
            IReadOnlyList<string> source,
            ICollection<string> target,
            ISet<string> seen)
        {
            if (source == null) return;
            for (int index = 0; index < source.Count; index++)
            {
                string skill = source[index];
                if (!string.IsNullOrEmpty(skill) && seen.Add(skill))
                    target.Add(skill);
            }
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

        private static int ResolveHunterRoleId(
            IReadOnlyList<RecommendationRoleSource> sources,
            IReadOnlyList<RoleView> views)
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
                    || !views[index].WorkSpec.HasLiteralWorkType("Hunting"))
                    continue;
                if (best == null
                    || source.Entries.Count < best.Entries.Count)
                    best = source;
            }
            return best?.Id ?? -1;
        }

        private static int ResolveFireBlockerRoleId(
            IReadOnlyList<RecommendationRoleSource> sources,
            IReadOnlyList<RoleView> views)
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
                    && views[index].WorkSpec.HasLiteralWorkType("Firefighter"))
                    return source.Id;
            }
            return -1;
        }
    }
}
