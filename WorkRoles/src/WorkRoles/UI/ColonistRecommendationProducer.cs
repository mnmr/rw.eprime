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
        // and planning tuning. Human coverage additionally consumes the shared
        // mechanical snapshot identity only while integration is enabled, plus
        // transitions into/out of that policy. Workers never join human Pawns.
        // Catalog ownership belongs to RecsAdapter.
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
        private MechanicalRecommendationSnapshot? currentWorkers;
        private Func<Pawn, PawnExternalSnapshot>? currentExternalSnapshot;

        // Owner: window/store. Key: map identity (one retained map). Value:
        // detached mechanical facts and matching stable pawn identities, shared
        // by human coverage and robot targets, immutable after publication.
        // Dependencies: support, catalog/definitions, external-facts generation,
        // mechanical facts, runtime rules, roster and assignment presentation.
        // Refresh: event-driven on read; equal contents keep snapshot identity.
        // Teardown: Reset/store change, disabling support, or selecting another
        // map releases the old projection; no map scans on unchanged reads.
        private MechanicalRecommendationSnapshot mechanical = MechanicalRecommendationSnapshot.Empty;
        private Map? mechanicalMap;
        private RecommendationCatalogProjection? mechanicalCatalog;
        private int mechanicalExternal = -1;
        private int mechanicalRevision = -1;
        private int mechanicalRuntime = -1;
        private int mechanicalRoster = -1;
        private int mechanicalPresentation = -1;

        internal ColonistRecommendationProducer()
        {
            buildColony = BuildColony;
        }

        internal RecommendationSnapshot For(RoleStore store,
            IReadOnlyList<Pawn> pawns, Func<Pawn, PawnExternalSnapshot> externalSnapshot,
            MechanicalRecommendationSnapshot? workers = null)
        {
            EnsureOwner(store);
            // Listed-pawn suitability has no colony coverage dependency. Only
            // the map-plan caller supplies mechanical facts, through the same
            // snapshot it uses for robot targets.
            if (!store.mechanicalRecommendationsEnabled || !store.mechanicalWorkersEnabled
                || workers?.Workers.Count == 0) workers = null;
            catalog = RecsAdapter.BuildRoleCatalog(store);

            currentWorkers = workers;
            currentExternalSnapshot = externalSnapshot;
            try
            {
                return cache.For(store, pawns, catalog, UiVersion.Assignments,
                    externalGeneration, UiVersion.RecommendationOrder,
                    store.recommendationTuning ?? RecommendationsTuningOptions.Default,
                    buildColony, workers);
            }
            finally
            {
                currentExternalSnapshot = null;
                currentWorkers = null;
            }
        }

        private void EnsureOwner(RoleStore store)
        {
            if (ReferenceEquals(owner, store)) return;
            Reset();
            owner = store;
        }

        internal MechanicalRecommendationSnapshot MechanicalFor(RoleStore store, Map? map)
        {
            EnsureOwner(store);
            if (!store.mechanicalWorkersEnabled || map == null)
            {
                mechanical = MechanicalRecommendationSnapshot.Empty;
                mechanicalMap = null;
                mechanicalCatalog = null;
                return mechanical;
            }
            RecommendationCatalogProjection nextCatalog = RecsAdapter.BuildRoleCatalog(store);
            if (ReferenceEquals(mechanicalMap, map) && ReferenceEquals(mechanicalCatalog, nextCatalog)
                && mechanicalExternal == externalGeneration && mechanicalRevision == store.MechanicalFactsRevision
                && mechanicalRuntime == UiVersion.RuntimeRules && mechanicalRoster == UiVersion.Roster
                && mechanicalPresentation == UiVersion.AssignmentPresentation) return mechanical;
            List<Pawn> pawns = MechanicalWorkers.OnMap(map);
            var views = new MechanicalWorkerView[pawns.Count];
            bool equal = ReferenceEquals(mechanicalMap, map) && mechanical.Pawns.Count == pawns.Count;
            for (int i = 0; i < pawns.Count; i++)
            {
                views[i] = MechanicalWorkers.Capture(pawns[i], store);
                if (equal && (!ReferenceEquals(mechanical.Pawns[i], pawns[i])
                    || !views[i].ContentEquals(mechanical.Workers[i]))) equal = false;
            }
            if (!equal) mechanical = new MechanicalRecommendationSnapshot(pawns, views);
            mechanicalMap = map;
            mechanicalCatalog = nextCatalog;
            mechanicalExternal = externalGeneration;
            mechanicalRevision = store.MechanicalFactsRevision;
            mechanicalRuntime = UiVersion.RuntimeRules;
            mechanicalRoster = UiVersion.Roster;
            mechanicalPresentation = UiVersion.AssignmentPresentation;
            return mechanical;
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
            currentWorkers = null;
            externalGeneration = 0;
            mechanical = MechanicalRecommendationSnapshot.Empty;
            mechanicalMap = null;
            mechanicalCatalog = null;
            mechanicalExternal = -1;
            mechanicalRevision = mechanicalRuntime = mechanicalRoster = mechanicalPresentation = -1;
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
            ColonyView colony = RecsAdapter.BuildColonyView(owner!, pawns,
                currentExternalSnapshot!, catalog!, previous);
            if (currentWorkers != null)
            {
                colony.IntegrateMechanicalWorkers = true;
                for (int i = 0; i < currentWorkers.Workers.Count; i++)
                    colony.MechanicalWorkers.Add(currentWorkers.Workers[i]);
            }
            return colony;
        }
    }

    internal sealed class MechanicalRecommendationSnapshot
    {
        internal static readonly MechanicalRecommendationSnapshot Empty =
            new MechanicalRecommendationSnapshot(Array.Empty<Pawn>(), Array.Empty<MechanicalWorkerView>());
        internal MechanicalRecommendationSnapshot(IReadOnlyList<Pawn> pawns, IReadOnlyList<MechanicalWorkerView> workers)
        { Pawns = pawns; Workers = workers; }
        internal IReadOnlyList<Pawn> Pawns { get; }
        internal IReadOnlyList<MechanicalWorkerView> Workers { get; }
    }
}
