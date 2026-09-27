namespace Implanner
{
    /// Revision over the external pawn state Implanner evaluation and gear
    /// display consume: worn apparel, equipped weapon, hediffs, and
    /// pawn-roster membership. Advanced by the exact event patches in
    /// Patch_PawnFacts, and by ImplantQualities when Quality Bionics
    /// Remastered's efficiency multipliers (which scale installed implants)
    /// change.
    internal static class ExternalPawnFacts
    {
        internal static int Revision { get; private set; }

        internal static void Bump() => Revision = unchecked(Revision + 1);

        internal static void Reset() => Revision = 0;
    }
}
