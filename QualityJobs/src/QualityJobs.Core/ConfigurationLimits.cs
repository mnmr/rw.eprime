namespace QualityJobs.Core
{
    /// <summary>Canonical normalization for multiplayer-visible configuration.</summary>
    public static class ConfigurationLimits
    {
        public static int Skill(int value) => SkillRules.ClampSkill(value);

        public static int Quality(int value) => value < 0 ? 0 : value > 6 ? 6 : value;

        public static int StockCap(int value) => value < 0 ? 0 : value > 50 ? 50 : value;
    }
}
