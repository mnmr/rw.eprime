namespace WorkRoles
{
    /// Monotonic change stamp backing the UI's open-window snapshots: bumped by
    /// every mutation that can alter what the window shows — role/assignment
    /// commands (local or another MP client's), pawn arrivals/departures, hour
    /// flips (all via CompiledJobOrders invalidation) and cosmetic role edits.
    /// Draw-path caches recompute only when the stamp moves, so an idle open
    /// window costs nothing per frame.
    public static class UiVersion
    {
        public static int Current { get; private set; }

        public static void Bump() => Current++;
    }
}
