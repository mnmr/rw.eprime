using System;
using System.Collections.Generic;
using Verse;
using WorkRoles.Core.Recs;

namespace WorkRoles.UI
{
    /// Shared window producer for map recommendations and listed-pawn verdicts.
    /// Cohort matching and result identity preservation live in the pure Core
    /// cache. All game projection work is behind these explicit domain gates.
    internal sealed class ColonistRecommendationProducer
    {
        // Owner: Colonists window/store. Key: store identity and role catalog
        // domains. Value: detached immutable-by-ownership catalog shared across
        // both cohorts. Dependencies: role work, recommendation policy/path,
        // global activation, authored-rule presence, definition reload and
        // research availability (RoleRecommendation advances on completion).
        // Refresh: event-driven on the next read.
        // Equality: Core compares exact consumed contents before publishing a
        // new computation; equal refreshes preserve plan/verdict identity.
        // Teardown: Reset drops catalog, cohort cache and transient build inputs.
        private readonly RecommendationSnapshotCache<Pawn> cache =
            new RecommendationSnapshotCache<Pawn>();
        private readonly Func<IReadOnlyList<Pawn>, IReadOnlyList<PawnView>?, ColonyView> buildColony;
        private RoleStore? owner;
        private RecommendationCatalogProjection? catalog;
        private int work = -1, recommendation = -1, activation = -1, rules = -1;
        private int definitions = -1;
        private int externalGeneration;
        private Func<Pawn, PawnExternalSnapshot>? currentExternalSnapshot;

        internal ColonistRecommendationProducer()
        {
            buildColony = BuildColony;
        }

        internal RecommendationSnapshot For(RoleStore store,
            IReadOnlyList<Pawn> pawns, Func<Pawn, PawnExternalSnapshot> externalSnapshot)
        {
            int definitionRevision = DefinitionReloadCoordinator.Revision;
            if (!ReferenceEquals(owner, store) || catalog == null
                || work != UiVersion.RoleWork
                || recommendation != UiVersion.RoleRecommendation
                || rules != UiVersion.RoleRules
                || definitions != definitionRevision)
            {
                catalog = RecsAdapter.BuildRoleCatalog(store);
                owner = store;
                work = UiVersion.RoleWork;
                recommendation = UiVersion.RoleRecommendation;
                activation = UiVersion.RoleActivation;
                rules = UiVersion.RoleRules;
                definitions = definitionRevision;
            }
            else if (activation != UiVersion.RoleActivation)
            {
                var enabled = new bool[catalog.Roles.Count];
                for (int index = 0; index < enabled.Length; index++)
                    enabled[index] = store.RoleById(catalog.Roles[index].Id)?.enabled == true;
                catalog = catalog.WithActivation(enabled);
                activation = UiVersion.RoleActivation;
            }

            currentExternalSnapshot = externalSnapshot;
            try
            {
                return cache.For(store, pawns, catalog, UiVersion.Assignments,
                    externalGeneration, UiVersion.RecommendationOrder,
                    store.recommendationTuning ?? RecommendationsTuningOptions.Default,
                    buildColony);
            }
            finally
            {
                currentExternalSnapshot = null;
            }
        }

        internal void InvalidateExternalFacts()
        {
            unchecked { externalGeneration++; }
        }

        internal void Reset()
        {
            cache.Reset();
            catalog = null;
            owner = null;
            currentExternalSnapshot = null;
            work = recommendation = activation = rules = definitions = -1;
            externalGeneration = 0;
        }

        private ColonyView BuildColony(IReadOnlyList<Pawn> pawns, IReadOnlyList<PawnView>? previous)
            => RecsAdapter.BuildColonyView(owner!, pawns,
                currentExternalSnapshot!, catalog!, previous);
    }
}
