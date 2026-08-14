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

        internal static bool IsRelevant(Pawn pawn) => pawn != null
            && (pawn.IsColonist || pawn.IsSlaveOfColony
                || RoleStore.Current?.IsManaged(pawn) == true);

        internal static void Invalidate(Pawn pawn)
        {
            if (pawn == null) return;
            Revisions.Invalidate(pawn);
            PawnSignalSnapshotCache.Invalidate(pawn);
        }

        internal static void InvalidateAll()
        {
            Revisions.InvalidateAll();
            PawnSignalSnapshotCache.Clear();
        }

        internal static void Release(Pawn pawn)
        {
            if (pawn == null) return;
            Revisions.Release(pawn);
            PawnSignalSnapshotCache.Invalidate(pawn);
        }
    }
}
