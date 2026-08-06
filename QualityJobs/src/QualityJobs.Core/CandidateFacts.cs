namespace QualityJobs.Core
{
    /// <summary>Immutable snapshot of a pawn, built game-side, consumed by Core decisions.</summary>
    public readonly struct CandidateFacts
    {
        /// <summary>Stable deterministic identity (thingIDNumber) used for tie-breaks.</summary>
        public readonly int Id;
        /// <summary>Relevant skill level (recipe workSkill / Construction); mechs pass their fixed skill level.</summary>
        public readonly int Skill;
        /// <summary>InspirationDef == Inspired_Creativity.</summary>
        public readonly bool Inspired;
        /// <summary>Ideology RoleEffect_ProductionQualityOffset offset (0 without Ideology).</summary>
        public readonly int RoleOffset;
        /// <summary>The work type driving the bench's work giver is enabled for the pawn.</summary>
        public readonly bool WorkTypeEnabled;
        /// <summary>recipe.skillRequirements are all satisfied.</summary>
        public readonly bool MeetsRecipeSkillRequirements;
        /// <summary>XP progress toward the next level as fixed-point milli (0-999);
        /// rank tie-break only. 0 for mechs and skill-less pawns.</summary>
        public readonly int XpMilli;

        public CandidateFacts(int id, int skill, bool inspired, int roleOffset,
            bool workTypeEnabled, bool meetsRecipeSkillRequirements, int xpMilli = 0)
        {
            Id = id;
            Skill = skill;
            Inspired = inspired;
            RoleOffset = roleOffset;
            WorkTypeEnabled = workTypeEnabled;
            MeetsRecipeSkillRequirements = meetsRecipeSkillRequirements;
            XpMilli = xpMilli;
        }
    }
}
