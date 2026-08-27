using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// Per-(pawn, role) suitability verdicts for chip display. Ordinary roles
    /// use the planner's BestSignal; composites average their member verdicts
    /// and round down because composites are published bundles, not candidates.
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
                    verdicts[role.Id] = Verdict(context, pawnIndex, role);
                result.Add(verdicts);
            }
            return result;
        }

        private static SignalBucket Verdict(
            EngineContext context, int pawnIndex, RoleView role)
        {
            if (!context.MeetsExplicitRequiredSkills(pawnIndex, role))
                return SignalBucket.Awful;
            if (role.MemberRoleIds == null || role.MemberRoleIds.Count == 0)
                return context.BestSignal(pawnIndex, role, out _, out _);
            int total = 0;
            int count = 0;
            for (int index = 0; index < role.MemberRoleIds.Count; index++)
            {
                RoleView member = context.RoleOf(role.MemberRoleIds[index]);
                if (member == null) continue;
                total += (int)context.BestSignal(
                    pawnIndex, member, out _, out _);
                count++;
            }
            return count == 0
                ? context.BestSignal(pawnIndex, role, out _, out _)
                : (SignalBucket)(total / count);
        }
    }
}
