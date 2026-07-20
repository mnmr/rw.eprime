namespace WorkRoles.Core.Signals
{
    /// <summary>
    /// Bounds fallback polling for signal inputs whose mutations cannot be
    /// hooked. Sixty game ticks is one in-game second at normal speed: known
    /// mutations still invalidate immediately, while an unchanged open window
    /// traverses a pawn cohort at most once per interval. While the game is
    /// paused, a wall-clock lane keeps unhooked external settings observable.
    /// </summary>
    public static class MutableSignalObservationEpoch
    {
        public const int GameTicksPerEpoch = 60;

        public static int FromGameTick(int gameTick) => gameTick / GameTicksPerEpoch;

        public static long FromClocks(
            int gameTick, int realTimeSecond, bool paused)
        {
            long sourceEpoch = paused ? realTimeSecond : FromGameTick(gameTick);
            // The low bit makes pause/resume itself an observation boundary.
            return (sourceEpoch << 1) | (paused ? 1L : 0L);
        }
    }
}
