using System.Collections.Generic;

namespace WorkRoles.Core
{
    public static class TrainingPathMutationPolicy
    {
        public static bool IntSequenceEqual(
            IReadOnlyList<int> current,
            IReadOnlyList<int> requested)
        {
            int currentCount = current?.Count ?? 0;
            int requestedCount = requested?.Count ?? 0;
            if (currentCount != requestedCount) return false;
            for (int i = 0; i < currentCount; i++)
                if (current[i] != requested[i]) return false;
            return true;
        }

        public static bool ColorEqual(
            bool currentHasColor,
            float currentR,
            float currentG,
            float currentB,
            float currentA,
            bool requestedHasColor,
            float requestedR,
            float requestedG,
            float requestedB,
            float requestedA)
        {
            if (currentHasColor != requestedHasColor) return false;
            if (!currentHasColor) return true;
            return currentR == requestedR
                && currentG == requestedG
                && currentB == requestedB
                && currentA == requestedA;
        }

        public static bool BandsEqual(
            IReadOnlyList<int> currentRoleIds,
            IReadOnlyList<int> currentMins,
            IReadOnlyList<int> currentMaxes,
            IReadOnlyList<int> requestedRoleIds,
            IReadOnlyList<int> requestedMins,
            IReadOnlyList<int> requestedMaxes) =>
            IntSequenceEqual(currentRoleIds, requestedRoleIds)
            && IntSequenceEqual(currentMins, requestedMins)
            && IntSequenceEqual(currentMaxes, requestedMaxes);
    }
}
