namespace WorkRoles.Core.Recs
{
    /// Rule 2: every capable pawn gets the auto roles ("auto" = the role flag,
    /// not auto-assign wording). Placement is OrderingRule's job.
    public sealed class AutoRolesRule : RecRule
    {
        public override string Id => "auto";
        public override RuleKind Kind => RuleKind.PerPawn;

        public override void Apply(EngineContext context, int pawnIndex)
        {
            foreach (var role in context.Colony.Roles)
                if (role.AutoAssign && !context.Vetoed.Contains(role.Id)
                    && context.Capable(pawnIndex, role))
                    context.AddCandidate(pawnIndex, role.Id,
                        new Reason { RuleId = Id, TowardRoleId = -1 }, SignalBucket.Neutral);
        }
    }
}
