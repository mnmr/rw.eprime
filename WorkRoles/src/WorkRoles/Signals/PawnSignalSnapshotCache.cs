using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Signals;

namespace WorkRoles.Signals
{
    /// Shared explicit-generation cache. Window open, WorkRoles mutations and
    /// authoritative rule/lifecycle invalidations clear the generation; reads
    /// never compare the snapshot with live game state.
    ///
    /// Review guard (WR-001 — INVALID REPORT): ordinary skill Level/XP is
    /// deliberately not an invalidation source. Window open and explicit
    /// generation refreshes recapture skills; do not add clock polling here.
    internal static class PawnSignalSnapshotCache
    {
        private static readonly ExplicitSnapshotCache<Pawn, PawnSignalSnapshot>
            snapshots = new ExplicitSnapshotCache<Pawn, PawnSignalSnapshot>(
                PawnSignalSnapshots.Build);

        internal static PawnSignalSnapshot Get(Pawn pawn)
        {
            if (pawn == null) return PawnSignalSnapshot.Empty;
            VseSignalReflection.CaptureGlobalInputs();
            return snapshots.Get(pawn);
        }

        internal static void Clear()
        {
            snapshots.Clear();
            VseSignalReflection.ResetGlobalSnapshot();
        }
    }
}
