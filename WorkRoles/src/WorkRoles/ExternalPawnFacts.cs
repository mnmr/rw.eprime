using RimShared.Common;
using Verse;
using WorkRoles.Core;
using WorkRoles.Signals;

namespace WorkRoles
{
    /// <summary>
    /// Explicit invalidation source for live pawn data captured by the UI,
    /// including cached portrait/name/trait presentation.
    /// Role and assignment mutations do not belong here: recommendation views
    /// overlay those values after reading the external snapshot.
    /// </summary>
    internal static class ExternalPawnFacts
    {
        internal static readonly OwnerInvalidationRevisions<Pawn> Revisions =
            new OwnerInvalidationRevisions<Pawn>(
                ReferenceIdentityComparer<Pawn>.Instance);

        // Owner: world. Keys: pawn identity; values: factual/skill capture
        // revisions. Dependencies: factual changes exclude names. Refresh is event-driven. Repeated
        // reads allocate nothing. Release removes a pawn; world teardown clears
        // all registries. The broad revision also owns tooltip grammar epochs.
        internal static readonly OwnerInvalidationRevisions<Pawn> FactRevisions =
            new OwnerInvalidationRevisions<Pawn>(ReferenceIdentityComparer<Pawn>.Instance);

        // Same world/pawn ownership and teardown as FactRevisions. Only changed
        // work evidence advances this stamp; it does not invalidate skill facts.
        internal static readonly OwnerInvalidationRevisions<Pawn> WorkEvidenceRevisions =
            new OwnerInvalidationRevisions<Pawn>(ReferenceIdentityComparer<Pawn>.Instance);

        internal static void InvalidateWorkEvidence(Pawn pawn)
        {
            WorkEvidenceRevisions.Invalidate(pawn);
            Revisions.Invalidate(pawn);
            if (RoleStore.Current?.mechanicalWorkersEnabled == true && MechanicalWorkers.IsMechanical(pawn))
                MechanicalWorkers.NotifyChanged();
        }

        internal static bool IsRelevant(Pawn pawn) => pawn != null
            && (pawn.IsColonist || pawn.IsSlaveOfColony
                || RoleStore.Current?.mechanicalWorkersEnabled == true && MechanicalWorkers.IsColonyWorker(pawn)
                || RoleStore.Current?.IsManaged(pawn) == true);

        internal static void Invalidate(Pawn pawn)
        {
            if (pawn == null) return;
            Revisions.Invalidate(pawn);
            FactRevisions.Invalidate(pawn);
            PawnSignalSnapshotCache.Invalidate(pawn);
            Capabilities.PawnWorkFacts.Refresh(pawn);
            if (RoleStore.Current?.mechanicalWorkersEnabled == true && MechanicalWorkers.IsMechanical(pawn))
                MechanicalWorkers.NotifyChanged();
        }

        internal static void InvalidateName(Pawn pawn)
        {
            if (pawn != null) Revisions.Invalidate(pawn);
        }

        internal static void InvalidateDowned(Pawn pawn)
        {
            // Health transitions can invoke hediff callbacks that change skill
            // inputs. Recapture them; semantic skill equality retains unchanged
            // presentations and widths instead of assuming only Downed changed.
            Invalidate(pawn);
        }

        internal static void InvalidateAll()
        {
            Revisions.InvalidateAll();
            FactRevisions.InvalidateAll();
            WorkEvidenceRevisions.InvalidateAll();
            Capabilities.PawnWorkFacts.Clear();
            PawnSignalSnapshotCache.Clear();
        }

        internal static void ReleaseForTeardown() => InvalidateAll();

        internal static void Release(Pawn pawn)
        {
            if (pawn == null) return;
            Revisions.Release(pawn);
            FactRevisions.Release(pawn);
            WorkEvidenceRevisions.Release(pawn);
            Capabilities.PawnWorkFacts.Release(pawn);
            PawnSignalSnapshotCache.Invalidate(pawn);
        }
    }
}
