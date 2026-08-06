namespace WorkRoles.Core.Recs
{
    /// Per-role scaling seam: the complete holder requirement at a colony
    /// size. Refinements swap the algorithm, not the interpretation.
    public interface IScalingAlgorithm
    {
        HolderRequirement Requirement(RoleView role, int colonySize);
    }

    /// Recommendation holder scaling. Explicit role scales and custom totals
    /// remain authoritative; only the no-scale unit formula is tuned.
    public sealed class RecommendationScaling : IScalingAlgorithm
    {
        private readonly RecommendationFormulaEngine formulas;

        public RecommendationScaling(RecommendationsTuningOptions options)
            : this(new RecommendationFormulaEngine(
                options ?? RecommendationsTuningOptions.Default))
        {
        }

        internal RecommendationScaling(RecommendationFormulaEngine formulas)
        {
            this.formulas = formulas;
        }

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
                int perUnit = formulas.FallbackColonistsPerUnit;
                int units = System.Math.Max(
                    formulas.FallbackMinimumUnits,
                    (System.Math.Max(0, colonySize) + perUnit - 1) / perUnit);
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

}
