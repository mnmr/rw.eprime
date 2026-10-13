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
        // Owner: Colonists window/store. Key: store identity and exact pawn
        // cohorts. Value: detached immutable-by-ownership pawn inputs, verdicts
        // and plans. Dependencies: shared role catalog identity, per-pawn
        // assignment order/pins and external snapshot identities, stored order
        // and planning tuning. Catalog ownership belongs to RecsAdapter.
        // Refresh: event-driven on the next read.
        // Equality: Core compares exact consumed contents before publishing a
        // new computation; equal refreshes preserve plan/verdict identity.
        // Teardown: Reset drops catalog, cohort cache and transient build inputs.
        private readonly RecommendationSnapshotCache<Pawn> cache =
            new RecommendationSnapshotCache<Pawn>();
        private readonly Func<IReadOnlyList<Pawn>, IReadOnlyList<PawnView>?, ColonyView> buildColony;
        private RoleStore? owner;
        private RecommendationCatalogProjection? catalog;
        private static readonly Func<(Pawn pawn, RoleStore store, PawnExternalSnapshot facts), PawnView>
            BuildPawn = state => RecsAdapter.PawnViewOf(state.pawn, state.store, state.facts);
        private int externalGeneration;
        private Func<Pawn, PawnExternalSnapshot>? currentExternalSnapshot;

        internal ColonistRecommendationProducer()
        {
            buildColony = BuildColony;
        }

        internal RecommendationSnapshot For(RoleStore store,
            IReadOnlyList<Pawn> pawns, Func<Pawn, PawnExternalSnapshot> externalSnapshot)
        {
            catalog = RecsAdapter.BuildRoleCatalog(store);
            owner = store;

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
            externalGeneration = 0;
        }

        private ColonyView BuildColony(IReadOnlyList<Pawn> pawns, IReadOnlyList<PawnView>? previous)
        {
            if (previous == null)
            {
                var projected = new PawnView[pawns.Count];
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    PawnExternalSnapshot facts = currentExternalSnapshot!(pawn);
                    projected[i] = cache.PawnFor(owner!, pawn, UiVersion.AssignmentsForPawn(pawn),
                        facts, (pawn, owner!, facts), BuildPawn);
                }
                previous = projected;
            }
            return RecsAdapter.BuildColonyView(owner!, pawns,
                currentExternalSnapshot!, catalog!, previous);
        }
    }
}
