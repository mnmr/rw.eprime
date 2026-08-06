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
    }
}
