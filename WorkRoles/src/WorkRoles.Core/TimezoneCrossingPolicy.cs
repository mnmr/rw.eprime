namespace WorkRoles.Core
{
    public enum TimezoneCrossingResponse
    {
        None,
        /// The moving object carries its pawns off-map (caravan): evict their
        /// compiled orders wholesale.
        InvalidateTravelerPawns,
        /// The moving object is a live map with spawned pawns: evict only
        /// time-ruled holders and let the map tick reconcile them.
        InvalidateMapTimeRuled
    }

    /// <summary>
    /// Decides how a world-object tile change affects time-ruled compiled
    /// orders. Local hours normally flip only at the global 2500-tick
    /// boundary; crossing a timezone meridian mid-hour is the one transition
    /// the boundary gates cannot see.
    /// </summary>
    public static class TimezoneCrossingPolicy
    {
        public static TimezoneCrossingResponse Respond(
            bool anyTimeRuledRole,
            int previousTimeZone,
            int newTimeZone,
            bool isTraveler,
            bool hasSpawnedMap)
        {
            if (!anyTimeRuledRole || previousTimeZone == newTimeZone)
                return TimezoneCrossingResponse.None;
            if (isTraveler)
                return TimezoneCrossingResponse.InvalidateTravelerPawns;
            if (hasSpawnedMap)
                return TimezoneCrossingResponse.InvalidateMapTimeRuled;
            return TimezoneCrossingResponse.None;
        }
    }
}
