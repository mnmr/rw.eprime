using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// Per-(pawn, role) suitability verdicts for chip display: the same
    /// BestSignal aggregation the planner ranks candidates with.
    public static class RoleSuitability
    {
        /// One roleId-to-verdict map per colony pawn, aligned with Colony.Pawns.
        public static List<Dictionary<int, SignalBucket>> Verdicts(ColonyView colony)
        {
            var context = new EngineContext(colony);
            var result = new List<Dictionary<int, SignalBucket>>(colony.Pawns.Count);
            for (int pawnIndex = 0; pawnIndex < colony.Pawns.Count; pawnIndex++)
            {
                var verdicts = new Dictionary<int, SignalBucket>(colony.Roles.Count);
                foreach (RoleView role in colony.Roles)
                    verdicts[role.Id] = context.BestSignal(
                        pawnIndex, role, out _, out _);
                result.Add(verdicts);
            }
            return result;
        }
    }
}
