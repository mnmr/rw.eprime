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
        /// Only mechanitors (mechlink implant) are eligible; otherwise an
        /// ordinary demand-planned role.
        Mechanitor,
    }

    /// Detached role facts supplied by game and offline adapters. Job-derived
    /// recommendation facts are deliberately absent: RecommendationCatalogBuilder
    /// owns those derivations for every consumer.
    public sealed class RecommendationRoleSource
    {
        public int Id;
        /// Template def the role was seeded from; null for player-made roles.
        public string? TemplateDefName;
        /// Live non-composite members of a composite role; null for ordinary roles.
        public List<int>? MemberRoleIds;
        public List<JobEntry> Entries = new List<JobEntry>();
        public bool AutoAssign;
        public bool HasRules;
        public bool Blocker;
        public bool PreserveRecommendationOrder;
        /// False = repeat championships use the occasional-work penalty.
        public bool ChampionPenalty = true;
        /// True = a skilled role accepts a pawn who can do only some of its work.
        public bool PartialCapability;
        public RoleCategory Category;
        public RoleTime Time;
        /// Minimum biological age (years) for holding the role; 0 = no gate.
        public int MinAge;
        /// Maximum biological age (years, inclusive) for holding the role; 0 = no gate.
        public int MaxAge;
        /// User-authored hard skill gates; null or empty = no additional gate.
        public List<string>? DeclaredRequiredSkills;
        /// Authored demand: minimum assignment count and ideal colonist percentage.
        public int ColonyMin;
        public int Coverage;
        public bool Available = true;
        public bool Enabled = true;
        public bool ExcludedFromRecommendations;
        public RecommendationSpecialRoleKind SpecialRole;
        /// Optional immutable full-fidelity work projection from the shared
        /// producer. Training exclusions derive a separate spec when necessary.
        public RoleWorkSpec? FullWorkSpec;
    }

    public struct RecommendationRolePolicy
    {
        public bool AutoAssign, HasRules, Blocker, ChampionPenalty, PartialCapability, Available, Enabled, ExcludedFromRecommendations;
        public RoleCategory Category;
        public RoleTime Time;
        public int MinAge, MaxAge, ColonyMin, CoveragePercent;

        public static RecommendationRolePolicy From(RoleView role) => new RecommendationRolePolicy
        {
            AutoAssign = role.AutoAssign, HasRules = role.HasRules, Blocker = role.Blocker,
            ChampionPenalty = role.ChampionPenalty, PartialCapability = role.PartialCapability,
            Available = role.Available, Enabled = role.Enabled, Category = role.Category,
            ExcludedFromRecommendations = role.ExcludedFromRecommendations,
            Time = role.Time, MinAge = role.MinAge, MaxAge = role.MaxAge,
            ColonyMin = role.ColonyMin, CoveragePercent = role.CoveragePercent,
        };

        internal bool Matches(RoleView role) => AutoAssign == role.AutoAssign && HasRules == role.HasRules
            && Blocker == role.Blocker && ChampionPenalty == role.ChampionPenalty
            && PartialCapability == role.PartialCapability && Available == role.Available
            && Enabled == role.Enabled && Category == role.Category && Time == role.Time
            && ExcludedFromRecommendations == role.ExcludedFromRecommendations
            && MinAge == role.MinAge && MaxAge == role.MaxAge && ColonyMin == role.ColonyMin
            && CoveragePercent == role.CoveragePercent;

        internal RoleView Apply(RoleView role) => new RoleView
        {
            Id = role.Id, TemplateDefName = role.TemplateDefName,
            MemberRoleIds = role.MemberRoleIds, Coverage = role.Coverage, OrderedCoverage = role.OrderedCoverage,
            PreserveRecommendationOrder = role.PreserveRecommendationOrder, RequiresMechanitor = role.RequiresMechanitor,
            WorkSpec = role.WorkSpec, AutoAssign = AutoAssign, HasRules = HasRules, Blocker = Blocker,
            ChampionPenalty = ChampionPenalty, PartialCapability = PartialCapability, Available = Available,
            Enabled = Enabled, Category = Category, Time = Time, MinAge = MinAge, MaxAge = MaxAge,
            ExcludedFromRecommendations = ExcludedFromRecommendations,
            ColonyMin = ColonyMin, CoveragePercent = CoveragePercent,
        };
    }

    /// Complete role-catalog projection consumed by RecommendationPlan. The
    /// role views and their work specs are owned by this projection.
    public sealed class RecommendationCatalogProjection
    {
        private readonly RecommendationSpecialRoleKind[] specialRoles;
        private readonly int[] entryCounts;
        // Owner: this detached catalog; key: role id. Immutable training work
        // inputs retain full-work identity, profile identity and exact excluded
        // giver sets. Path edits compare these before resolving jobs; equal
        // exclusions reuse ordinary/composite work identities. Scalar/order
        // overlays share this metadata. Dropping the catalog releases it.
        private readonly IReadOnlyList<RoleView>? trainingBaseRoles;
        private readonly JobProfileIndex? trainingProfiles;
        private readonly IReadOnlyDictionary<int, HashSet<string>>? trainingExclusions;

        internal RecommendationCatalogProjection(
            IReadOnlyList<RoleView> roles,
            IReadOnlyList<PathView> paths,
            RecommendationSpecialRoleKind[] specialRoles,
            int[] entryCounts,
            IReadOnlyList<RoleView>? trainingBaseRoles = null,
            JobProfileIndex? trainingProfiles = null,
            IReadOnlyDictionary<int, HashSet<string>>? trainingExclusions = null)
        {
            Roles = roles;
            Paths = paths;
            this.specialRoles = specialRoles;
            this.entryCounts = entryCounts;
            this.trainingBaseRoles = trainingBaseRoles;
            this.trainingProfiles = trainingProfiles;
            this.trainingExclusions = trainingExclusions;
            HunterRoleId = RecommendationCatalogBuilder.ResolveHunterRoleId(roles, specialRoles, entryCounts);
            FireBlockerRoleId = RecommendationCatalogBuilder.ResolveFireBlockerRoleId(roles, specialRoles);
        }

        public IReadOnlyList<RoleView> Roles { get; }
        public IReadOnlyList<PathView> Paths { get; }
        public int HunterRoleId { get; }
        public int FireBlockerRoleId { get; }

        internal RecommendationCatalogProjection WithTrainingViews(IReadOnlyList<RoleView> roles, IReadOnlyList<PathView> paths,
            JobProfileIndex profiles, IReadOnlyDictionary<int, HashSet<string>> exclusions)
            => new RecommendationCatalogProjection(roles, paths, specialRoles, entryCounts, Roles, profiles, exclusions);

        internal RoleView? PreviousTrainingRole(RoleView fullRole, JobProfileIndex profiles)
        {
            if (!ReferenceEquals(profiles, trainingProfiles) || trainingBaseRoles == null) return null;
            for (int i = 0; i < trainingBaseRoles.Count; i++)
            {
                RoleView prior = trainingBaseRoles[i];
                if (prior.Id != fullRole.Id) continue;
                if (!ReferenceEquals(prior.WorkSpec, fullRole.WorkSpec)
                    || !ReferenceEquals(prior.MemberRoleIds, fullRole.MemberRoleIds)
                    || !ReferenceEquals(prior.Coverage, fullRole.Coverage)) return null;
                for (int role = 0; role < Roles.Count; role++)
                    if (Roles[role].Id == fullRole.Id) return Roles[role];
                return null;
            }
            return null;
        }

        internal bool SameExclusion(int roleId, HashSet<string> excluded)
            => trainingExclusions != null && trainingExclusions.TryGetValue(roleId, out HashSet<string> prior)
                && prior.SetEquals(excluded);

        /// Catalog order breaks ties in derived recommendation defaults. Reuse
        /// each detached role/work/path object when only that order changes.
        public RecommendationCatalogProjection WithRoleOrder(IReadOnlyList<int> roleIds)
        {
            if (roleIds.Count != Roles.Count) throw new ArgumentException("Role count must match the catalog.", nameof(roleIds));
            bool same = true;
            for (int i = 0; i < roleIds.Count; i++) if (roleIds[i] != Roles[i].Id) { same = false; break; }
            if (same) return this;
            var byId = new Dictionary<int, int>(Roles.Count);
            for (int i = 0; i < Roles.Count; i++) byId.Add(Roles[i].Id, i);
            var roles = new RoleView[Roles.Count];
            var special = new RecommendationSpecialRoleKind[Roles.Count];
            var counts = new int[Roles.Count];
            for (int i = 0; i < roleIds.Count; i++)
            {
                if (!byId.TryGetValue(roleIds[i], out int previous))
                    throw new ArgumentException("Role identities must match the catalog.", nameof(roleIds));
                byId.Remove(roleIds[i]);
                roles[i] = Roles[previous];
                special[i] = specialRoles[previous];
                counts[i] = entryCounts[previous];
            }
            var paths = new List<PathView>(Paths.Count);
            for (int i = 0; i < roleIds.Count; i++)
                for (int path = 0; path < Paths.Count; path++)
                    if (Paths[path].Id == roleIds[i]) paths.Add(Paths[path]);
            for (int path = 0; path < Paths.Count; path++)
                if (!paths.Contains(Paths[path])) paths.Add(Paths[path]);
            return new RecommendationCatalogProjection(roles, paths, special, counts,
                trainingBaseRoles, trainingProfiles, trainingExclusions);
        }

        /// Policy changes reuse every work/path buffer. Equal policies preserve
        /// the catalog itself, so unchanged HasRules/research outcomes stop here.
        public RecommendationCatalogProjection WithPolicy(IReadOnlyList<RecommendationRolePolicy> policies)
        {
            if (policies.Count != Roles.Count) throw new ArgumentException("Policy count must match the catalog.", nameof(policies));
            List<RoleView>? changed = null;
            for (int index = 0; index < Roles.Count; index++)
            {
                RoleView role = Roles[index];
                if (policies[index].Matches(role)) { changed?.Add(role); continue; }
                if (changed == null)
                {
                    changed = new List<RoleView>(Roles.Count);
                    for (int prior = 0; prior < index; prior++) changed.Add(Roles[prior]);
                }
                changed.Add(policies[index].Apply(role));
            }
            return changed == null ? this : new RecommendationCatalogProjection(
                new ReadOnlyCollection<RoleView>(changed), Paths, specialRoles, entryCounts,
                trainingBaseRoles, trainingProfiles, trainingExclusions);
        }

        /// Reuse the already-resolved work/training artifact when only global
        /// switches changed. Shared role/path collections are immutable by
        /// producer ownership; changed roles receive detached scalar overlays.
        public RecommendationCatalogProjection WithActivation(IReadOnlyList<bool> enabled)
        {
            if (enabled.Count != Roles.Count) throw new ArgumentException("Activation count must match the catalog.", nameof(enabled));
            List<RoleView>? changed = null;
            for (int index = 0; index < Roles.Count; index++)
            {
                RoleView role = Roles[index];
                if (role.Enabled == enabled[index])
                {
                    changed?.Add(role);
                    continue;
                }
                if (changed == null)
                {
                    changed = new List<RoleView>(Roles.Count);
                    for (int prior = 0; prior < index; prior++) changed.Add(Roles[prior]);
                }
                changed.Add(new RoleView
                {
                    Id = role.Id, TemplateDefName = role.TemplateDefName,
                    MemberRoleIds = role.MemberRoleIds, Coverage = role.Coverage,
                    OrderedCoverage = role.OrderedCoverage, AutoAssign = role.AutoAssign,
                    HasRules = role.HasRules, Blocker = role.Blocker,
                    PreserveRecommendationOrder = role.PreserveRecommendationOrder,
                    ChampionPenalty = role.ChampionPenalty, PartialCapability = role.PartialCapability,
                    Category = role.Category, Time = role.Time, MinAge = role.MinAge, MaxAge = role.MaxAge,
                    ColonyMin = role.ColonyMin, CoveragePercent = role.CoveragePercent,
                    Available = role.Available, Enabled = enabled[index],
                    ExcludedFromRecommendations = role.ExcludedFromRecommendations,
                    RequiresMechanitor = role.RequiresMechanitor, WorkSpec = role.WorkSpec,
                });
            }
            return changed == null ? this : new RecommendationCatalogProjection(
                new ReadOnlyCollection<RoleView>(changed), Paths, specialRoles, entryCounts,
                trainingBaseRoles, trainingProfiles, trainingExclusions);
        }

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

            var views = new List<RoleView>(sources.Count);
            for (int index = 0; index < sources.Count; index++)
            {
                RecommendationRoleSource source = sources[index]
                    ?? throw new ArgumentException(
                        "Role sources cannot contain null.", nameof(sources));
                views.Add(ViewOf(
                    source,
                    source.FullWorkSpec ?? (source.MemberRoleIds != null ? RoleWorkSpec.Empty
                        : SpecOf(source, jobs, naturalPriorities, jobProfiles, null)),
                    jobs));
            }
            ApplyCompositeSpecs(sources, views);

            var specialRoles = new RecommendationSpecialRoleKind[sources.Count];
            var entryCounts = new int[sources.Count];
            for (int index = 0; index < sources.Count; index++)
            {
                specialRoles[index] = sources[index].SpecialRole;
                entryCounts[index] = sources[index].Entries.Count;
            }
            var full = new RecommendationCatalogProjection(
                new ReadOnlyCollection<RoleView>(views),
                Array.Empty<PathView>(), specialRoles, entryCounts);
            return WithTraining(full, sources, paths ?? Array.Empty<PathView>(), jobs, naturalPriorities, jobProfiles);
        }

        /// Derive only exclusion-dependent work. The full work producer and
        /// ordering consumers continue to own/share the untouched full catalog.
        public static RecommendationCatalogProjection WithTraining(RecommendationCatalogProjection full,
            IReadOnlyList<RecommendationRoleSource> sources, IReadOnlyList<PathView> paths,
            IJobCatalog jobs, IReadOnlyDictionary<string, int> naturalPriorities, JobProfileIndex profiles,
            RecommendationCatalogProjection? previous = null)
        {
            if (paths.Count == 0) return full;
            var ownedPaths = new List<PathView>(paths.Count);
            for (int index = 0; index < paths.Count; index++) ownedPaths.Add(CopyPath(paths[index]));
            var excluded = TrainingCoverageExclusion.ExcludedCoverageByRole(full.Roles, ownedPaths);
            var views = new List<RoleView>(full.Roles);
            bool workChanged = false;
            for (int index = 0; index < sources.Count; index++)
            {
                RoleView? prior = previous?.PreviousTrainingRole(full.Roles[index], profiles);
                // Composite work is always the union of the adjusted member
                // work. Building a standalone exclusion spec is discarded by
                // the merge and must not change composite semantics.
                if (sources[index].MemberRoleIds != null)
                {
                    if (prior != null) views[index] = WithWork(full.Roles[index], prior.WorkSpec, prior);
                    continue;
                }
                if (!excluded.TryGetValue(sources[index].Id, out HashSet<string> givers)) continue;
                RoleWorkSpec spec = prior != null && previous!.SameExclusion(sources[index].Id, givers)
                    ? prior.WorkSpec : SpecOf(sources[index], jobs, naturalPriorities, profiles, givers);
                if (RoleWorkSpec.StructurallyEqual(spec, views[index].WorkSpec)) continue;
                views[index] = WithWork(full.Roles[index], spec, prior);
                workChanged = true;
            }
            if (workChanged || previous != null) ApplyCompositeSpecs(sources, views, previous?.Roles ?? full.Roles);
            return full.WithTrainingViews(new ReadOnlyCollection<RoleView>(views), new ReadOnlyCollection<PathView>(ownedPaths), profiles, excluded);
        }

        private static RoleView WithWork(RoleView full, RoleWorkSpec spec, RoleView? prior)
        {
            if (ReferenceEquals(spec, full.WorkSpec)) return full;
            RecommendationRolePolicy policy = RecommendationRolePolicy.From(full);
            if (prior != null && ReferenceEquals(spec, prior.WorkSpec) && policy.Matches(prior)) return prior;
            RoleView changed = policy.Apply(full);
            changed.WorkSpec = spec;
            return changed;
        }

        private static RoleWorkSpec SpecOf(
            RecommendationRoleSource source,
            IJobCatalog jobs,
            IReadOnlyDictionary<string, int> naturalPriorities,
            JobProfileIndex jobProfiles,
            ISet<string>? excludedProfileGivers)
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
                PartialCapability = source.PartialCapability,
                Category = source.Category,
                Time = source.Time,
                MinAge = source.MinAge,
                MaxAge = source.MaxAge,
                ColonyMin = source.ColonyMin,
                CoveragePercent = source.Coverage,
                Available = source.Available,
                Enabled = source.Enabled,
                ExcludedFromRecommendations = source.ExcludedFromRecommendations,
                RequiresMechanitor = source.SpecialRole
                    == RecommendationSpecialRoleKind.Mechanitor,
                WorkSpec = spec,
            };
        }

        /// Composite roles publish the deduplicated union of member givers;
        /// gates union the composite's own and every member's gates.
        private static void ApplyCompositeSpecs(
            IReadOnlyList<RecommendationRoleSource> sources,
            IList<RoleView> views, IReadOnlyList<RoleView>? previous = null)
        {
            var indexById = new Dictionary<int, int>(sources.Count);
            for (int index = 0; index < sources.Count; index++)
                indexById[sources[index].Id] = index;
            Dictionary<int, RoleView>? previousById = null;
            if (previous != null)
            {
                previousById = new Dictionary<int, RoleView>(previous.Count);
                for (int index = 0; index < previous.Count; index++) previousById[previous[index].Id] = previous[index];
            }

            for (int index = 0; index < sources.Count; index++)
            {
                RecommendationRoleSource source = sources[index];
                if (source.MemberRoleIds == null) continue;
                if (previous == null && source.FullWorkSpec != null) continue;
                if (previous != null)
                {
                    bool changed = !previousById!.TryGetValue(source.Id, out RoleView priorComposite)
                        || !ReferenceEquals(priorComposite.WorkSpec, views[index].WorkSpec);
                    for (int member = 0; member < source.MemberRoleIds.Count; member++)
                        if (indexById.TryGetValue(source.MemberRoleIds[member], out int at)
                            && (!previousById.TryGetValue(source.MemberRoleIds[member], out RoleView priorMember)
                                || !ReferenceEquals(priorMember.WorkSpec, views[at].WorkSpec))) { changed = true; break; }
                    if (!changed) continue;
                }
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
                RoleWorkSpec merged = RoleWorkSpecBuilder.Merge(source.Id, memberSpecs, gates);
                if (RoleWorkSpec.StructurallyEqual(merged, views[index].WorkSpec)) continue;
                RoleView replacement = RecommendationRolePolicy.From(views[index]).Apply(views[index]);
                replacement.WorkSpec = merged;
                views[index] = replacement;
            }
        }

        private static void AddGates(
            IReadOnlyList<string>? source,
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

        internal static int ResolveHunterRoleId(
            IReadOnlyList<RoleView> views,
            IReadOnlyList<RecommendationSpecialRoleKind> specialRoles,
            IReadOnlyList<int> entryCounts)
        {
            for (int index = 0; index < views.Count; index++)
            {
                RoleView source = views[index];
                if (specialRoles[index] == RecommendationSpecialRoleKind.Hunter
                    && source.Enabled
                    && !source.HasRules
                    && !source.Blocker)
                    return source.Id;
            }

            int best = -1;
            for (int index = 0; index < views.Count; index++)
            {
                RoleView source = views[index];
                if (!source.Enabled
                    || source.HasRules
                    || source.Blocker
                    || !views[index].WorkSpec.HasLiteralWorkType("Hunting"))
                    continue;
                if (best < 0 || entryCounts[index] < entryCounts[best])
                    best = index;
            }
            return best < 0 ? -1 : views[best].Id;
        }

        internal static int ResolveFireBlockerRoleId(
            IReadOnlyList<RoleView> views,
            IReadOnlyList<RecommendationSpecialRoleKind> specialRoles)
        {
            for (int index = 0; index < views.Count; index++)
            {
                RoleView source = views[index];
                if (specialRoles[index]
                        == RecommendationSpecialRoleKind.FireBlocker
                    && source.Enabled
                    && !source.HasRules
                    && source.Blocker)
                    return source.Id;
            }

            for (int index = 0; index < views.Count; index++)
            {
                RoleView source = views[index];
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
