using System.Collections.Generic;
using Verse;
using WorkRoles.Core.Signals;

namespace WorkRoles.Signals
{
    internal sealed class PawnSignalSnapshotCache
    {
        private readonly Dictionary<Pawn, PawnSignalSnapshot> byPawn =
            new Dictionary<Pawn, PawnSignalSnapshot>();

        internal PawnSignalSnapshot Get(Pawn pawn)
        {
            if (pawn == null) return PawnSignalSnapshot.Empty;
            if (!byPawn.TryGetValue(pawn, out var snapshot))
            {
                snapshot = PawnSignalSnapshots.Build(pawn);
                byPawn[pawn] = snapshot;
            }
            return snapshot;
        }

        internal void Clear() => byPawn.Clear();
    }
}
