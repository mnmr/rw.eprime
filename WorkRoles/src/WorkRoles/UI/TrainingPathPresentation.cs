using System;
using UnityEngine;

namespace WorkRoles.UI
{
    /// Shared display semantics for a training path. Recommendation ordering
    /// ranks bands by their minimum, so every view uses that same definition
    /// when referring to the path's highest-band role.
    internal static class TrainingPathPresentation
    {
        internal static int HighestBandRoleId(TrainingPath path)
        {
            int target = -1;
            int bestMin = int.MinValue;
            int count = Math.Min(path.roleIds.Count, path.bandMins.Count);
            for (int i = 0; i < count; i++)
                if (path.bandMins[i] > bestMin)
                {
                    bestMin = path.bandMins[i];
                    target = path.roleIds[i];
                }
            return target;
        }

        internal static Color ColorFor(RoleStore store, TrainingPath path)
        {
            if (path.hasCustomColor) return path.color;
            Role target = store.RoleById(HighestBandRoleId(path));
            return target != null && target.hasCustomColor
                ? target.color
                : RoleChipUI.DefaultChipColor;
        }
    }
}
