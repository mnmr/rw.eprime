using System;

namespace WorkRoles.Core
{
    /// Derives the engine's banded holder demand from a role's two tuning
    /// numbers. Per band: RequiredTotal = max(colonyMin, coverage*colonySize/100)
    /// (rounded half up at the band's top size); DirectMinimum = min(1, colonyMin),
    /// the rest of the total is waivable to training roles.
    public static class RoleDemand
    {
        public static HolderScale DeriveScale(int colonyMin, int coverage)
        {
            var scale = new HolderScale();
            for (int band = 0; band < HolderScale.Bands; band++)
            {
                int size = (band + 1) * HolderScale.BandSize;
                int total = Math.Max(colonyMin, (coverage * size + 50) / 100);
                scale.RequiredTotals[band] = total;
                scale.TrainingWaivers[band] = Math.Max(0, total - Math.Min(1, colonyMin));
            }
            return scale;
        }

        /// Save/import migration: the two numbers a retired named strategy maps
        /// to. False when the strategy carries no demand (Never or no scale).
        public static bool TryFromLegacyStrategy(
            RoleAssignmentStrategy strategy, out int colonyMin, out int coverage)
        {
            colonyMin = 0;
            coverage = 0;
            if (strategy == null || strategy.Mode == ScaleMode.Never) return false;
            if (strategy.Mode == ScaleMode.Unskilled)
            {
                colonyMin = 1;
                coverage = 100;
                return true;
            }
            if (strategy.Scale == null) return false;
            colonyMin = strategy.Scale.RequiredTotals[0];
            // Round half up over the top band's size of 36 colonists.
            coverage = (strategy.Scale.RequiredTotals[HolderScale.Bands - 1] * 100 + 18) / 36;
            return true;
        }
    }
}
