using System;
using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Deterministic merge boundary for assignment strategies carried by a
    /// role import.
    public static class HolderScaleImport
    {
        public static bool Merge(
            IList<RoleAssignmentStrategy> existingStrategies,
            RoleAssignmentStrategy imported)
        {
            if (existingStrategies == null || imported == null
                || string.IsNullOrWhiteSpace(imported.Name))
                return false;

            int existingIndex = -1;
            for (int index = 0; index < existingStrategies.Count; index++)
            {
                RoleAssignmentStrategy existing = existingStrategies[index];
                if (existing != null && string.Equals(
                        existing.Name, imported.Name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    existingIndex = index;
                    break;
                }
            }

            if (existingIndex < 0)
            {
                existingStrategies.Add(imported.Copy());
                return true;
            }

            RoleAssignmentStrategy current = existingStrategies[existingIndex];
            if (current.Preset) return false;
            if (current.SameValuesAs(imported)) return false;
            existingStrategies[existingIndex] = imported.Copy();
            return true;
        }
    }
}
