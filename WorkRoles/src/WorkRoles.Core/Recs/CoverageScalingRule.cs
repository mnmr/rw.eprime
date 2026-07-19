namespace WorkRoles.Core.Recs
{
    /// Per-role scaling seam (spec rule 6): how many holders a role wants at
    /// a colony size. Refinements swap the algorithm, not the rule.
    public interface IScalingAlgorithm
    {
        int Want(RoleView role, int colonySize);
    }

    /// 1-per-6 colony units: auto-coverage roles want one unit, needed roles
    /// want MinHolders per unit; interest-only and Never want nothing.
    public sealed class UnitScaling : IScalingAlgorithm
    {
        public int Want(RoleView role, int colonySize)
        {
            if (role.HolderMode == RoleHolderMode.Custom)
                return System.Math.Max(0, role.MinHolders);
            int units = System.Math.Max(1, (colonySize + 5) / 6);
            if (role.MinHolders >= 1)
                return System.Math.Min(colonySize, role.MinHolders * units);
            return role.MinHolders == -1 ? System.Math.Min(colonySize, units) : 0;
        }
    }

    /// Fills EngineContext.Want for every dealable role.
    public sealed class CoverageScalingRule : RecRule
    {
        private readonly IScalingAlgorithm scaling;
        public CoverageScalingRule(IScalingAlgorithm scaling) { this.scaling = scaling; }

        public override string Id => "scaling";
        public override RuleKind Kind => RuleKind.Colony;

        public override void Apply(EngineContext context)
        {
            foreach (var role in context.Colony.Roles)
            {
                if (context.Vetoed.Contains(role.Id)) continue;
                if (role.AutoAssign || role.HasRules || role.Blocker) continue;
                int want = scaling.Want(role, context.Colony.Pawns.Count);
                if (want > 0)
                {
                    context.BaseWant[role.Id] = want;
                    context.Want[role.Id] = want;
                }
            }
        }
    }
}
