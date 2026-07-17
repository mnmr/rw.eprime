using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// Band arithmetic over PathView entries.
    public static class PathMath
    {
        /// Inside [min, max); max 21 = open top.
        public static bool InsideBand(PathView path, int entry, int level)
            => level >= path.BandMins[entry]
            && (path.BandMaxes[entry] >= SkillProgressionMath.MaxLevel
                || level < path.BandMaxes[entry]);

        /// A training TARGET: some same-path entry has a strictly lower band min.
        public static bool IsTarget(PathView path, int entry)
        {
            for (int i = 0; i < path.BandMins.Count; i++)
                if (path.BandMins[i] < path.BandMins[entry]) return true;
            return false;
        }

        /// Entry indexes with a strictly lower band min (trainees toward entry).
        public static IEnumerable<int> LowerBandEntries(PathView path, int entry)
        {
            for (int i = 0; i < path.RoleIds.Count; i++)
                if (path.BandMins[i] < path.BandMins[entry]) yield return i;
        }
    }
}
