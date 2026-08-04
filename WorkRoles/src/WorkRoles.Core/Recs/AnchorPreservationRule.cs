using System.Linq;

namespace WorkRoles.Core.Recs
{
    /// Final ordering pass: existing assignments whose placement is controlled
    /// by the player or by role behavior keep their position relative to the
    /// nearest assignments that survive the recommendation pass.
    public sealed class AnchorPreservationRule : RecRule
    {
        public override string Id => "anchors";
        public override RuleKind Kind => RuleKind.PerPawn;

        public override void Apply(EngineContext context, int pawnIndex)
        {
            var result = context.Results[pawnIndex];
            var targetByRole = result.Assignments
                .GroupBy(a => a.RoleId)
                .ToDictionary(group => group.Key, group => group.First());
            int[] ordered = Ordering.PreserveProtectedOrder(
                context,
                pawnIndex,
                result.Assignments.Select(assignment => assignment.RoleId)
                    .ToArray());
            result.Assignments.Clear();
            for (int index = 0; index < ordered.Length; index++)
                result.Assignments.Add(targetByRole[ordered[index]]);
        }
    }
}
