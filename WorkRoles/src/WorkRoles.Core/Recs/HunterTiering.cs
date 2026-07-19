using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// Ensures one pawn in the explicitly supplied Hunter candidate set owns
    /// the hardcoded tier-zero placement. Both initial assignment and
    /// post-limit normalization use this single tie-break contract.
    public static class HunterTiering
    {
        public static void EnsureTierZero(
            EngineContext context,
            IEnumerable<int> candidatePawns)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (candidatePawns == null)
                throw new ArgumentNullException(nameof(candidatePawns));

            int lowest = -1;
            int lowestLevel = int.MaxValue;
            foreach (int pawn in candidatePawns)
            {
                if (context.HunterTiers[pawn] == 0) return;
                int level = context.Colony.Pawns[pawn].ShootingLevel;
                if (lowest < 0 || level < lowestLevel
                    || level == lowestLevel && pawn < lowest)
                {
                    lowest = pawn;
                    lowestLevel = level;
                }
            }
            if (lowest >= 0) context.HunterTiers[lowest] = 0;
        }
    }
}
