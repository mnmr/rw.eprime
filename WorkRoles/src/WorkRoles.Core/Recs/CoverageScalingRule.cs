namespace WorkRoles.Core.Recs
{
    /// Per-role scaling seam: the complete holder requirement at a colony
    /// size. Refinements swap the algorithm, not the interpretation.
    public interface IScalingAlgorithm
    {
        HolderRequirement Requirement(RoleView role, int colonySize);
    }

    /// Banded scales are direct lookups; roles without a scale keep the legacy
    /// 1-per-6 unit formula (auto-coverage roles require one unit, needed roles
    /// use RequiredTotal per unit; interest-only and Never require nothing).
    public sealed class UnitScaling : IScalingAlgorithm
    {
        public HolderRequirement Requirement(RoleView role, int colonySize)
        {
            int requiredTotal;
            if (role.Scale != null)
            {
                requiredTotal = System.Math.Max(
                    0, role.Scale.RequiredTotalAt(colonySize));
                int cap = role.Scale.MaxAt(colonySize);
                if (cap != RoleHolderRange.Uncapped)
                    requiredTotal = System.Math.Min(requiredTotal, cap);
            }
            else if (role.HolderMode == RoleHolderMode.Custom)
                requiredTotal = System.Math.Max(0, role.RequiredTotal);
            else
            {
                int units = System.Math.Max(1, (colonySize + 5) / 6);
                if (role.RequiredTotal >= 1)
                    requiredTotal = role.RequiredTotal * units;
                else
                    requiredTotal = role.RequiredTotal == -1 ? units : 0;
            }
            requiredTotal = System.Math.Min(colonySize, requiredTotal);
            return new HolderRequirement(
                requiredTotal, role.TrainingWaiversAt(colonySize));
        }
    }

    /// Fills EngineContext.RequiredTotal for every dealable role.
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
                HolderRequirement requirement = scaling.Requirement(
                    role, context.Colony.Pawns.Count);
                if (requirement.RequiredTotal > 0)
                {
                    context.BaseRequiredTotal[role.Id] = requirement.RequiredTotal;
                    context.RequiredTotal[role.Id] = requirement.RequiredTotal;
                }
            }
        }
    }
}
