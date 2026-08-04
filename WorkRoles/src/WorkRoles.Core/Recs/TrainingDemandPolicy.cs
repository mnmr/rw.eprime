namespace WorkRoles.Core.Recs
{
    /// Combines a role's own holder range with exact assignments arriving
    /// from training waivers for other roles. Kept separate so the experiment
    /// can change without rewriting path matching or candidate selection.
    public interface ITrainingDemandPolicy
    {
        int RequiredTotal(int baseRequiredTotal, int inboundAssignments);
        int Maximum(int baseMaximum, int inboundAssignments);
    }

    public sealed class AdditiveTrainingDemandPolicy : ITrainingDemandPolicy
    {
        public int RequiredTotal(int baseRequiredTotal, int inboundAssignments)
            => System.Math.Min(RoleHolderRange.Uncapped,
                System.Math.Max(0, baseRequiredTotal)
                + System.Math.Max(0, inboundAssignments));

        public int Maximum(int baseMaximum, int inboundAssignments)
            => baseMaximum >= RoleHolderRange.Uncapped
                ? RoleHolderRange.Uncapped
                : System.Math.Min(RoleHolderRange.Uncapped,
                    System.Math.Max(0, baseMaximum) + System.Math.Max(0, inboundAssignments));
    }
}
