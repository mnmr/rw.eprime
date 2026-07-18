using System.Collections.Generic;
using Verse;
using WorkRoles.Core.Signals;

namespace WorkRoles.Signals
{
    internal sealed class PawnSignalSnapshotCache
    {
        private readonly Dictionary<Pawn, SignalSnapshot> byPawn =
            new Dictionary<Pawn, SignalSnapshot>();

        internal SignalSnapshot Get(Pawn pawn)
        {
            if (pawn == null) return SignalSnapshot.Empty;
            if (!byPawn.TryGetValue(pawn, out var snapshot))
            {
                snapshot = new SignalSnapshot(PawnSignalCollector.Collect(pawn));
                byPawn[pawn] = snapshot;
            }
            return snapshot;
        }

        internal void Clear() => byPawn.Clear();
    }
}
