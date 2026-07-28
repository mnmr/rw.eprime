using System.Collections.Generic;

namespace WorkRoles.Core
{
    /// Recovery planning for Restore Defaults: which candidate roles to assign
    /// so a pawn's post-restore job coverage again includes everything it had
    /// before. Pure coverage math — no role names or template knowledge.
    public static class RestoreCoveragePlanner
    {
        /// Candidate role ids in pick order, greedy largest-recovery-first;
        /// every pick must recover at least one lost giver. Unrecoverable
        /// givers (no candidate covers them) are simply left behind.
        public static List<int> RecoveryRoles(
            IReadOnlyCollection<string> lostGivers,
            IReadOnlyList<(int roleId, IReadOnlyCollection<string> coverage)> candidates)
        {
            var result = new List<int>();
            if (lostGivers == null || lostGivers.Count == 0 || candidates == null)
                return result;
            var remaining = new HashSet<string>(lostGivers);
            var used = new HashSet<int>();
            while (remaining.Count > 0)
            {
                int bestIndex = -1, bestGain = 0;
                for (int i = 0; i < candidates.Count; i++)
                {
                    var (roleId, coverage) = candidates[i];
                    if (coverage == null || used.Contains(roleId)) continue;
                    int gain = 0;
                    foreach (var giver in coverage)
                        if (remaining.Contains(giver)) gain++;
                    if (gain > bestGain)
                    {
                        bestGain = gain;
                        bestIndex = i;
                    }
                }
                if (bestIndex < 0) break;
                used.Add(candidates[bestIndex].roleId);
                result.Add(candidates[bestIndex].roleId);
                foreach (var giver in candidates[bestIndex].coverage)
                    remaining.Remove(giver);
            }
            return result;
        }
    }
}
