namespace WorkRoles.Core
{
    /// Trait rarity tiers by commonality, matching the default limits of
    /// the Trait Rarity Colors mod so traits read the same with or without
    /// it: 1 mystic, 2 legendary, 3 epic, 4 rare, 5 uncommon, 6 common.
    public static class TraitRarityTiers
    {
        public const int Common = 6;

        /// Traits whose commonality differs by gender use the average.
        public static float Commonality(float neutral, float male, float female) =>
            male != female ? (male + female) / 2f : neutral;

        public static int Tier(float commonality) =>
            commonality <= 0.1f ? 1
            : commonality <= 0.5f ? 2
            : commonality <= 0.9f ? 3
            : commonality <= 1.9f ? 4
            : commonality <= 2f ? 5
            : Common;
    }
}
