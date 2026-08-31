namespace RimShared.Common
{
    /// Where a pawn currently is, for location-rule matching, serviceability,
    /// and grouping.
    public struct PawnPlace
    {
        public string? LocationId; // map id / ship id; null when off-map
        public bool IsSettlement;  // a player settlement map
        public bool IsShip;        // a gravship map not parked at a settlement
        // Neither flag set: caravan or any non-home map.

        /// A serviceable location can execute automation for pawns at it.
        public bool IsServiceable => IsSettlement || IsShip;
    }

    /// Derives a faction-relative place from cached faction-invariant map facts.
    public static class FactionLocationClassifier
    {
        public static PawnPlace Classify(
            string? mapLocationId,
            string? shipLocationId,
            bool ownedByFaction,
            bool spawnedViaGravship,
            bool parentCanBePlayerHome,
            bool parentIsSettlement,
            bool hasGravEngine)
        {
            bool ship = ownedByFaction && hasGravEngine && !parentIsSettlement;
            bool home = ownedByFaction
                && (spawnedViaGravship || parentCanBePlayerHome || hasGravEngine);
            return new PawnPlace
            {
                LocationId = ship ? shipLocationId : mapLocationId,
                IsSettlement = home && !ship,
                IsShip = ship,
            };
        }
    }
}
