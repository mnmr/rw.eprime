namespace WorkRoles.Core.Recs
{
    /// Rule 3: bucket-classified interest (Strong or better) makes a skilled
    /// role a candidate, with the bucket as its strength. Autos, unskilled
    /// and hunting roles have their own rules.
    public sealed class SignalCandidatesRule : RecRule
    {
        public override string Id => "signals";
        public override RuleKind Kind => RuleKind.PerPawn;

        public override void Apply(EngineContext context, int pawnIndex)
        {
            foreach (var role in context.Colony.Roles)
            {
                if (role.AutoAssign || role.Unskilled || role.Hunting) continue;
                if (context.Vetoed.Contains(role.Id) || !context.Capable(pawnIndex, role)) continue;
                var bucket = context.BestSignal(pawnIndex, role, out string skill, out var source);
                if (bucket < SignalBucket.Strong) continue;
                context.AddCandidate(pawnIndex, role.Id, new Reason
                {
                    RuleId = Id, Bucket = bucket, Source = source,
                    SkillDefName = skill, TowardRoleId = -1,
                }, bucket);
            }
        }
    }
}
