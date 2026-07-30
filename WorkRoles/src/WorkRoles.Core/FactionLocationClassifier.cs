namespace WorkRoles.Core
{
    /// <summary>
    /// Derives a faction-relative place from cached faction-invariant map facts.
    /// </summary>
    public static class FactionLocationClassifier
    {
        public static PawnPlace Classify(string locationId,
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
                LocationId = locationId,
                IsSettlement = home && !ship,
                IsShip = ship,
            };
        }
    }
}
