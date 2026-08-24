namespace WorkRoles
{
    /// <summary>
    /// Narrow presentation revision for the auto-optimize toggle. It advances
    /// only when the authoritative option actually changes.
    /// </summary>
    internal static class AutoOptimizePresentationRevision
    {
        internal static int Current { get; private set; }

        internal static void Bump()
        {
            unchecked { Current++; }
        }
    }
}
