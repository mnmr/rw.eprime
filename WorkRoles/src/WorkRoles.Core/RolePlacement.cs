using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Where a palette role lands in a colonist's assignment list for the
    /// "after Basics" gesture, using the recommendation-order positions from
    /// Ordering.BasePositions. Existing roles without a position never bound
    /// an insert. (The "where recommendations would put it" gesture is
    /// RecommendationPlan.PlacementIndex.)
    public static class RolePlacement
    {
        /// Index right after the lead role (Basics) when the colonist has it;
        /// otherwise after the last assigned role ranked at or before the lead
        /// position; 0 when neither applies.
        public static int AfterLeadingIndex(IReadOnlyList<int> existing, int leadRoleId,
            IReadOnlyDictionary<int, long> positions, long leadPosition)
        {
            int result = 0;
            for (int i = 0; i < existing.Count; i++)
            {
                if (existing[i] == leadRoleId) return i + 1;
                if (positions.TryGetValue(existing[i], out long at) && at <= leadPosition)
                    result = i + 1;
            }
            return result;
        }
    }
}
