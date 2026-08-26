namespace WorkRoles.Core
{
    /// Which pawns WorkRoles tracks: alive colony members only (subhumans such
    /// as ghouls fail the colony-member fact and are never tracked). Saving
    /// additionally requires a persistence anchor — evidence the game itself
    /// serializes the pawn (held on a map, kept in the world pawn list, or held
    /// by any holder chain) — so a saved reference key always resolves on load.
    public static class PawnRolePersistencePolicy
    {
        public static bool ShouldAutoAssign(bool isAlive, bool isColonyMember) =>
            isAlive && isColonyMember;

        public static bool ShouldRetain(bool isAlive, bool isColonyMember,
            bool hasPersistenceAnchor) =>
            isAlive && isColonyMember && hasPersistenceAnchor;
    }
}
