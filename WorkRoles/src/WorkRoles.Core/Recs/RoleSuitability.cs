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
            => Verdicts(new EngineContext(colony));

        internal static List<Dictionary<int, SignalBucket>> Verdicts(EngineContext context)
        {
            var result = new List<Dictionary<int, SignalBucket>>(context.Colony.Pawns.Count);
            for (int pawnIndex = 0; pawnIndex < context.Colony.Pawns.Count; pawnIndex++)
                result.Add(VerdictsForPawn(context, pawnIndex));
            return result;
        }

        internal static Dictionary<int, SignalBucket> VerdictsForPawn(EngineContext context, int pawnIndex)
        {
            var verdicts = new Dictionary<int, SignalBucket>(context.Colony.Roles.Count);
            foreach (RoleView role in context.Colony.Roles)
                verdicts[role.Id] = Verdict(context, pawnIndex, role);
            return verdicts;
        }

        private static SignalBucket Verdict(
            EngineContext context, int pawnIndex, RoleView role)
        {
            // Specialist robots can carry generic skills. Their unskilled and
            // composite roles are unsuitable when none of the jobs can be done.
            // Humans keep their pre-mechanical aptitude ratings and warnings.
            // Partial capability still uses the ordinary aptitude verdict;
            // assigned chips explain the missing jobs with their warning flag.
            if (context.Colony.Pawns[pawnIndex].IsMechanical
                && !role.Blocker && role.WorkTypes.Count > 0
                && !EngineContext.MeetsWorkCapability(context.Colony.Pawns[pawnIndex],
                    role.WorkSpec, partial: true))
                return SignalBucket.Awful;
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
