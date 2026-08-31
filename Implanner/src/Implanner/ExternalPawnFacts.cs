namespace Implanner
{
    /// Revision over the external pawn state Implanner evaluation and gear
    /// display consume: worn apparel, equipped weapon, hediffs, and
    /// pawn-roster membership. Advanced only by the exact event patches in
    /// Patch_PawnFacts; never polled.
    internal static class ExternalPawnFacts
    {
        internal static int Revision { get; private set; }

        internal static void Bump() => Revision = unchecked(Revision + 1);

        internal static void Reset() => Revision = 0;
    }
}
