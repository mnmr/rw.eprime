using UnityEngine;
using Verse;
using WorkRoles.Core.Signals;

namespace WorkRoles.Signals
{
    /// Shared session cache so mutation/lifecycle patches can evict snapshots
    /// even when the Work Roles window is not currently drawing.
    internal static class PawnSignalSnapshotCache
    {
        private static readonly MutableSignalSnapshotCache<Pawn, PawnSignalSnapshot>
            snapshots = new MutableSignalSnapshotCache<Pawn, PawnSignalSnapshot>(
                PawnSignalCollector.Signature, PawnSignalSnapshots.Build);

        internal static long Revision => snapshots.Revision;

        /// One fallback observation per in-game second while running or wall
        /// second while paused. Known mutations still invalidate immediately.
        internal static long ObservationEpoch
        {
            get
            {
                TickManager tickManager = Find.TickManager;
                return MutableSignalObservationEpoch.FromClocks(
                    tickManager?.TicksGame ?? 0,
                    (int)Time.realtimeSinceStartup,
                    tickManager?.Paused ?? true);
            }
        }

        internal static PawnSignalSnapshot Get(Pawn pawn)
        {
            if (pawn == null) return PawnSignalSnapshot.Empty;
            long observationEpoch = ObservationEpoch;
            VseSignalReflection.ObserveGlobalInputs(observationEpoch);
            return snapshots.Get(pawn, observationEpoch);
        }

        internal static void Invalidate(Pawn pawn) => snapshots.Invalidate(pawn);

        internal static void Clear()
        {
            snapshots.Clear();
            VseSignalReflection.ResetGlobalObservation();
        }
    }
}
