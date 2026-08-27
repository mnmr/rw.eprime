using System;

namespace WorkRoles.Core
{
    /// Holder demand from a role's two tuning numbers: RequiredTotal =
    /// max(colonyMin, coverage percent of the colony size, rounded half up);
    /// the direct minimum is min(1, colonyMin) and the rest of the total is
    /// waivable to training roles. Consumers clamp the total to their capacity
    /// so the direct minimum survives small colonies.
    public static class RoleDemand
    {
        public static HolderRequirement RequirementFor(
            int colonyMin, int coverage, int colonySize)
        {
            int total = Math.Max(colonyMin,
                (coverage * Math.Max(0, colonySize) + 50) / 100);
            return new HolderRequirement(
                total, Math.Max(0, total - Math.Min(1, colonyMin)));
        }
    }

    /// A holder requirement in the same terms used by configuration and UI.
    /// Training waivers are part of the required total; the direct minimum is
    /// therefore derived once here rather than reinterpreted by each consumer.
    public readonly struct HolderRequirement
    {
        public HolderRequirement(int requiredTotal, int trainingWaivers)
        {
            RequiredTotal = Math.Max(0, requiredTotal);
            TrainingWaivers = Math.Max(
                0, Math.Min(trainingWaivers, RequiredTotal));
        }

        public int RequiredTotal { get; }
        public int TrainingWaivers { get; }
        public int DirectMinimum => RequiredTotal - TrainingWaivers;
    }
}
