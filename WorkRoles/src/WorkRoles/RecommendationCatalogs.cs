using System;
using System.Collections.Generic;
using WorkRoles.Core.Recs;
using WorkRoles.Signals;

namespace WorkRoles
{
    public static partial class RecsAdapter
    {
        // Owner: process, partitioned by current RoleStore identity. Key:
        // role identity/order/work, definitions, normalized training paths and scalar
        // recommendation/rule/activation revisions; research completion alone
        // refreshes availability. Values: detached immutable full/training
        // catalogs shared by ordering, UI verdicts and colony plans. Refresh:
        // event-driven; no source reads on hits. Equality: equal policies/paths
        // preserve published identities and all unchanged work artifacts.
        // Teardown: ResetRecommendationCaches on world release; owner replacement
        // drops every previous store, role and catalog reference.
        private static RoleStore? catalogOwner;
        private static Role[] catalogRoles = Array.Empty<Role>();
        private static List<RecommendationRoleSource>? catalogSources;
        private static IReadOnlyList<PathView> catalogPaths = Array.Empty<PathView>();
        private static RecommendationCatalogProjection? fullCatalog, trainingCatalog;
        private static bool trainingDirty;
        private static int catalogWork = -1, catalogPolicy = -1, catalogRules = -1, catalogActivation = -1;
        private static int catalogDefinitions = -1, catalogResearch = -1, catalogOrder = -1, researchGeneration;
        private static readonly RecommendationOrderCache OrderingCache = new RecommendationOrderCache();

        internal static void NotifyRecommendationResearchChanged() { unchecked { researchGeneration++; } }

        internal static void ResetRecommendationCaches()
        {
            catalogOwner = null;
            catalogRoles = Array.Empty<Role>();
            catalogSources = null;
            catalogPaths = Array.Empty<PathView>();
            fullCatalog = trainingCatalog = null;
            trainingDirty = false;
            catalogWork = catalogPolicy = catalogRules = catalogActivation = catalogDefinitions = catalogResearch = catalogOrder = -1;
            OrderingCache.Reset();
        }

        internal static RecommendationCatalogProjection SharedRoleCatalog(RoleStore store, bool training)
        {
            bool ownerChanged = !ReferenceEquals(catalogOwner, store);
            if (ownerChanged) ResetRecommendationCaches();
            bool policyChanged = catalogPolicy != UiVersion.RoleRecommendation;
            bool structureChanged = fullCatalog == null || catalogWork != UiVersion.RoleWork
                || catalogDefinitions != DefinitionReloadCoordinator.Revision
                || policyChanged && !SameRoles(store);
            if (structureChanged)
            {
                catalogOwner = store;
                catalogRoles = store.roles.ToArray();
                catalogSources = BuildRoleSources(catalogRoles, ReferenceEquals(store, RoleStore.Current));
                fullCatalog = RecommendationCatalogBuilder.Build(catalogSources, Array.Empty<PathView>(),
                    GameJobCatalog.Instance, RoleWorkSpecs.NaturalPriorities(), JobSkillProfiles.RecommendationIndex());
                catalogPaths = PathViewsOf(store);
                trainingCatalog = null;
                trainingDirty = true;
            }
            else
            {
                if (catalogOrder != UiVersion.Catalog) ReorderCatalog(store);
                if (policyChanged || catalogRules != UiVersion.RoleRules
                    || catalogActivation != UiVersion.RoleActivation || catalogResearch != researchGeneration)
                {
                    var policies = new RecommendationRolePolicy[catalogRoles.Length];
                    bool availabilityChanged = catalogResearch != researchGeneration;
                    for (int i = 0; i < policies.Length; i++)
                    {
                        Role role = catalogRoles[i];
                        policies[i] = new RecommendationRolePolicy
                        {
                            AutoAssign = role.autoAssign, HasRules = role.HasRules, Blocker = role.blocker,
                            ChampionPenalty = role.championPenalty, PartialCapability = role.partialCapability,
                            Available = availabilityChanged ? RoleAvailable(role) : fullCatalog!.Roles[i].Available,
                            Enabled = role.enabled, Category = role.category, Time = role.time,
                            MinAge = Math.Max(0, role.minAge), MaxAge = role.maxAge,
                            ColonyMin = role.colonyMin, CoveragePercent = role.coverage,
                        };
                    }
                    fullCatalog = fullCatalog!.WithPolicy(policies);
                    if (trainingCatalog != null) trainingCatalog = trainingCatalog.WithPolicy(policies);
                    if (policyChanged && !SamePaths(store))
                    {
                        catalogPaths = PathViewsOf(store);
                        trainingDirty = true;
                    }
                }
            }
            catalogWork = UiVersion.RoleWork;
            catalogPolicy = UiVersion.RoleRecommendation;
            catalogRules = UiVersion.RoleRules;
            catalogActivation = UiVersion.RoleActivation;
            catalogDefinitions = DefinitionReloadCoordinator.Revision;
            catalogResearch = researchGeneration;
            catalogOrder = UiVersion.Catalog;
            if (!training) return fullCatalog!;
            if (trainingCatalog == null || trainingDirty)
            {
                trainingCatalog = RecommendationCatalogBuilder.WithTraining(
                    fullCatalog!, catalogSources!, catalogPaths, GameJobCatalog.Instance,
                    RoleWorkSpecs.NaturalPriorities(), JobSkillProfiles.RecommendationIndex(), trainingCatalog);
                trainingDirty = false;
            }
            return trainingCatalog;
        }

        private static void ReorderCatalog(RoleStore store)
        {
            bool same = true;
            for (int i = 0; i < catalogRoles.Length; i++)
                if (!ReferenceEquals(catalogRoles[i], store.roles[i])) { same = false; break; }
            if (same) return;
            var ids = new int[store.roles.Count];
            var sources = new List<RecommendationRoleSource>(store.roles.Count);
            var paths = new List<PathView>(catalogPaths.Count);
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = store.roles[i].id;
                for (int source = 0; source < catalogSources!.Count; source++)
                    if (catalogSources[source].Id == ids[i]) { sources.Add(catalogSources[source]); break; }
                for (int path = 0; path < catalogPaths.Count; path++)
                    if (catalogPaths[path].Id == ids[i]) paths.Add(catalogPaths[path]);
            }
            fullCatalog = fullCatalog!.WithRoleOrder(ids);
            if (trainingCatalog != null) trainingCatalog = trainingCatalog.WithRoleOrder(ids);
            catalogRoles = store.roles.ToArray();
            catalogSources = sources;
            catalogPaths = paths;
        }

        private static bool SameRoles(RoleStore store)
        {
            if (catalogRoles.Length != store.roles.Count) return false;
            for (int i = 0; i < catalogRoles.Length; i++)
                if (!ReferenceEquals(catalogRoles[i], store.RoleById(catalogRoles[i].id))) return false;
            return true;
        }

        private static bool SamePaths(RoleStore store)
        {
            int count = 0;
            for (int i = 0; i < store.roles.Count; i++)
            {
                Role role = store.roles[i];
                if (role.trainingRoleIds.Count == 0) continue;
                count++;
                PathView? found = null;
                for (int path = 0; path < catalogPaths.Count; path++)
                    if (catalogPaths[path].Id == role.id) { found = catalogPaths[path]; break; }
                if (found == null || !SameSequence(found.RoleIds, role.trainingRoleIds)
                    || !SameSequence(found.BandMins, role.trainingMins)
                    || !SameSequence(found.BandMaxes, role.trainingMaxes)) return false;
            }
            return count == catalogPaths.Count;
        }

        private static bool SameSequence(IReadOnlyList<int> left, IReadOnlyList<int> right)
        {
            if (left.Count != right.Count) return false;
            for (int i = 0; i < left.Count; i++) if (left[i] != right[i]) return false;
            return true;
        }

        internal static RecommendationOrderSnapshot RecommendationOrdering(RoleStore store, bool training = false)
        {
            RecommendationCatalogProjection catalog = SharedRoleCatalog(store, training);
            return OrderingCache.For(store, catalog.Roles,
                UiVersion.RecommendationOrder, store.recommendationOrder);
        }

        internal static IReadOnlyDictionary<int, long> RecommendationPositions(RoleStore store)
            => RecommendationOrdering(store, training: true).Positions;
    }
}
