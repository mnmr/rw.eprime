namespace WorkRoles.Core.Recs
{
    /// Rule 9, hardcoded: fire-fearing pawns get the firefighting blocker.
    /// Forced past the blocker veto; OrderingRule seats it first.
    public sealed class FireSafetyRule : RecRule
    {
        public override string Id => "fire";
        public override RuleKind Kind => RuleKind.PerPawn;

        public override bool Relevant(EngineContext context)
            => context.Colony.FireBlockerRoleId != -1
            && context.RoleOf(context.Colony.FireBlockerRoleId) != null;

        public override void Apply(EngineContext context, int pawnIndex)
        {
            if (!context.Colony.Pawns[pawnIndex].FireFear) return;
            context.AddCandidate(pawnIndex, context.Colony.FireBlockerRoleId,
                new Reason { RuleId = Id, TowardRoleId = -1 }, SignalBucket.Neutral, force: true);
            context.FireGranted[pawnIndex] = true;
        }
    }
}
