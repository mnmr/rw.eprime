namespace QualityJobs.Core
{
    /// <summary>
    /// The skill ceiling and quality-roll rule in force for the session.
    /// Vanilla caps skills at 20 and quality rolls at Masterwork. Endless
    /// Growth (SlimeSenpai.EndlessGrowth) lets skills reach 100, derives the
    /// roll center from its own curve and lets a roll land on Legendary
    /// directly, so with it loaded the odds differ even below skill 20.
    ///
    /// Set once by the game side at startup from the loaded assemblies, the
    /// same fact on every multiplayer client; never changed while playing.
    /// </summary>
    public static class SkillRules
    {
        public const int VanillaMaxSkill = 20;
        public const int EndlessGrowthMaxSkill = 100;

        /// <summary>Endless Growth is loaded.</summary>
        public static bool EndlessGrowth;

        public static int MaxSkill => EndlessGrowth ? EndlessGrowthMaxSkill : VanillaMaxSkill;

        public static int ClampSkill(int value)
            => value < 0 ? 0 : value > MaxSkill ? MaxSkill : value;
    }
}
