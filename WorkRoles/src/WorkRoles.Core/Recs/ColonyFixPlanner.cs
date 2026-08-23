using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// One pawn's colony-fix outcome: the recommended assignment order and
    /// whether it differs from the pawn's stored assignments. Unchanged pawns
    /// must not be written, so hourly auto-optimize runs and manual Fix My
    /// Colony share this changed determination.
    public sealed class PawnFixTarget
    {
        internal PawnFixTarget(int pawnIndex, int[] roleIds, bool changed)
        {
            PawnIndex = pawnIndex;
            this.roleIds = roleIds;
            Changed = changed;
        }

        private readonly int[] roleIds;

        public int PawnIndex { get; }
        public IReadOnlyList<int> RoleIds => roleIds;
        /// True when the recommended role-id sequence differs from the pawn's
        /// existing assignment sequence (covers additions, removals, and
        /// reorders alike).
        public bool Changed { get; }
    }

    /// Deterministic colony-fix targets over a recommendation plan: for every
    /// pawn, the recommended assignment order and whether applying it would
    /// change the pawn. Pure projection — callers own applying targets and
    /// preserving per-assignment state for kept roles.
    public static class ColonyFixPlanner
    {
        public static IReadOnlyList<PawnFixTarget> Build(ColonyView colony)
            => Build(colony, RecommendationsTuningOptions.Default);

        public static IReadOnlyList<PawnFixTarget> Build(
            ColonyView colony, RecommendationsTuningOptions options)
            => Build(colony, RecommendationPlan.Build(colony, options));

        public static IReadOnlyList<PawnFixTarget> Build(
            ColonyView colony, RecommendationPlan plan)
        {
            var targets = new PawnFixTarget[plan.PawnCount];
            for (int pawnIndex = 0; pawnIndex < plan.PawnCount; pawnIndex++)
            {
                var roleIds = new int[plan.RoleCountAt(pawnIndex)];
                for (int roleIndex = 0; roleIndex < roleIds.Length; roleIndex++)
                    roleIds[roleIndex] = plan.RoleAt(pawnIndex, roleIndex);
                targets[pawnIndex] = new PawnFixTarget(pawnIndex, roleIds,
                    !SameSequence(colony.Pawns[pawnIndex].Existing, roleIds));
            }
            return targets;
        }

        private static bool SameSequence(
            List<AssignmentView> existing, int[] roleIds)
        {
            if (existing.Count != roleIds.Length) return false;
            for (int index = 0; index < roleIds.Length; index++)
                if (existing[index].RoleId != roleIds[index]) return false;
            return true;
        }
    }
}
