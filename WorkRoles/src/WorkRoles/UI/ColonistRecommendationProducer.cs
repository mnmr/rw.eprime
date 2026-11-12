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
        // and assignment activation when ordinary composites may substitute,
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
        // Dependencies: support, definitions, roster and each worker's shared
        // external snapshot identity, RuntimeForPawn, PresentationForPawn,
        // ProvidesService and QualityPreparationAllowed. Aggregate external,
        // mechanical, runtime and presentation revisions only gate comparing
        // these keys; human edits never rebuild worker projections. Service
        // booleans are observed only after explicit external/mechanical events.
        // Refresh: event-driven on read; equal worker contents and cohorts keep
        // snapshot identity. Map scans occur only on roster/map changes.
        // Teardown: Reset/store change, disabling support, or selecting another
        // map releases the old projection; no map scans on unchanged reads.
        private MechanicalRecommendationSnapshot mechanical = MechanicalRecommendationSnapshot.Empty;
        private Map? mechanicalMap;
        private int mechanicalDefinitions = -1;
        private int mechanicalExternal = -1;
        private int mechanicalRevision = -1;
        private int mechanicalRuntime = -1;
        private int mechanicalRoster = -1;
        private int mechanicalPresentation = -1;
        // Same owner/key/dependencies/refresh/teardown as mechanical above.
        // Metadata is private and mutable; each published View is immutable.
        private sealed class WorkerEntry
        {
            internal PawnExternalSnapshot External = null!;
            internal MechanicalWorkerView View = null!;
            internal int Runtime, Presentation, Definitions;
            internal bool ProvidesService, PreparationAllowed;
        }
        private readonly Dictionary<Pawn, WorkerEntry> mechanicalEntries = new Dictionary<Pawn, WorkerEntry>();
        private readonly List<Pawn> departedWorkers = new List<Pawn>();

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
                    buildColony, workers, assignmentActivation: catalog.HasCompositeSubstitutions
                        ? UiVersion.AssignmentPresentation : 0);
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

        internal MechanicalRecommendationSnapshot MechanicalFor(RoleStore store, Map? map,
            Func<Pawn, PawnExternalSnapshot> externalSnapshot)
        {
            EnsureOwner(store);
            if (!store.mechanicalWorkersEnabled || map == null)
            {
                ReleaseMechanical();
                return mechanical;
            }
            int definitions = DefinitionReloadCoordinator.Revision;
            bool sameMap = ReferenceEquals(mechanicalMap, map);
            if (sameMap && mechanicalDefinitions == definitions
                && mechanicalExternal == externalGeneration && mechanicalRevision == store.MechanicalFactsRevision
                && mechanicalRuntime == UiVersion.RuntimeRules && mechanicalRoster == UiVersion.Roster
                && mechanicalPresentation == UiVersion.AssignmentPresentation) return mechanical;
            if (!sameMap) ReleaseMechanical();
            bool rosterChanged = !sameMap || mechanicalRoster != UiVersion.Roster;
            IReadOnlyList<Pawn> pawns = rosterChanged ? MechanicalWorkers.OnMap(map) : mechanical.Pawns;
            bool sameCohort = sameMap && mechanical.Pawns.Count == pawns.Count;
            if (sameCohort)
                for (int i = 0; i < pawns.Count; i++)
                    if (!ReferenceEquals(mechanical.Pawns[i], pawns[i])) { sameCohort = false; break; }
            if (sameCohort) pawns = mechanical.Pawns;
            MechanicalWorkerView[]? views = sameCohort ? null : new MechanicalWorkerView[pawns.Count];
            bool observeService = mechanicalRevision != store.MechanicalFactsRevision
                || mechanicalExternal != externalGeneration || mechanicalDefinitions != definitions;
            bool preparation = MechanicalWorkers.QualityPreparationAllowed;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                PawnExternalSnapshot external = externalSnapshot(pawn);
                int runtime = UiVersion.RuntimeForPawn(pawn);
                int presentation = UiVersion.PresentationForPawn(pawn);
                mechanicalEntries.TryGetValue(pawn, out WorkerEntry? entry);
                bool service = entry == null || observeService
                    ? MechanicalWorkers.ProvidesService(pawn) : entry.ProvidesService;
                if (entry == null || !ReferenceEquals(entry.External, external)
                    || entry.Runtime != runtime || entry.Presentation != presentation
                    || entry.Definitions != definitions || entry.ProvidesService != service
                    || entry.PreparationAllowed != preparation)
                {
                    MechanicalWorkerView rebuilt = MechanicalWorkers.CaptureFromSnapshot(pawn, store, external);
                    entry ??= new WorkerEntry();
                    if (entry.View == null || !rebuilt.ContentEquals(entry.View)) entry.View = rebuilt;
                    entry.External = external;
                    entry.Runtime = runtime;
                    entry.Presentation = presentation;
                    entry.Definitions = definitions;
                    entry.ProvidesService = service;
                    entry.PreparationAllowed = preparation;
                    mechanicalEntries[pawn] = entry;
                }
                if (views == null && !ReferenceEquals(entry.View, mechanical.Workers[i]))
                {
                    views = new MechanicalWorkerView[pawns.Count];
                    for (int previous = 0; previous < i; previous++) views[previous] = mechanical.Workers[previous];
                }
                if (views != null) views[i] = entry.View;
            }
            if (views != null) mechanical = new MechanicalRecommendationSnapshot(pawns, views);
            if (rosterChanged)
            {
                foreach (Pawn cached in mechanicalEntries.Keys)
                {
                    bool retained = false;
                    for (int i = 0; i < pawns.Count; i++)
                        if (ReferenceEquals(cached, pawns[i])) { retained = true; break; }
                    if (!retained) departedWorkers.Add(cached);
                }
                for (int i = 0; i < departedWorkers.Count; i++) mechanicalEntries.Remove(departedWorkers[i]);
                departedWorkers.Clear();
            }
            mechanicalMap = map;
            mechanicalDefinitions = definitions;
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
            ReleaseMechanical();
        }

        private void ReleaseMechanical()
        {
            mechanical = MechanicalRecommendationSnapshot.Empty;
            mechanicalMap = null;
            mechanicalEntries.Clear();
            departedWorkers.Clear();
            mechanicalDefinitions = -1;
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
                        facts, (pawn, owner!, facts), BuildPawn, assignmentActivation: catalog!.HasCompositeSubstitutions
                            ? UiVersion.PresentationForPawn(pawn) : 0);
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
