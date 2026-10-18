using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core
{
    /// Audited facts for optional code-defined jobs. Inputs are detached
    /// definition ownership identities; no optional game assembly is linked.
    public sealed class OptionalJobSkillProfile
    {
        private const string BrothelPackage = "calamabanana.rjw.brothelcolony";

        private static readonly OptionalJobSkillProfile SocialOnly =
            new OptionalJobSkillProfile(
                "Social",
                new ReadOnlyCollection<string>(new[] { "Social" }),
                new ReadOnlyCollection<JobProfileSkillEffect>(new[] {
                    new JobProfileSkillEffect("Social", RoleWorkEffect.Success) }));

        // Audited Brothel 1.6: WhoringHelper applies Social's acceptance factor
        // 0.8 + level/40 and Sexperience's price factor max(level/10, 0.2).
        // Sex is the recommendation discipline because the earnings factor is
        // more sensitive to that skill; Social remains used/trained secondary.
        // These are semantic effects, not normalized outcome curves. Social XP
        // comes from accepted invitations; Sexperience grants Sex XP in its
        // RJW orgasm hook. Static read-only descriptors are facts, not caches.
        private static readonly OptionalJobSkillProfile WithSexperience =
            new OptionalJobSkillProfile(
                "Sex",
                new ReadOnlyCollection<string>(new[] { "Social", "Sex" }),
                new ReadOnlyCollection<JobProfileSkillEffect>(new[] {
                    new JobProfileSkillEffect("Social", RoleWorkEffect.Success),
                    new JobProfileSkillEffect("Sex", RoleWorkEffect.Yield) }));

        private OptionalJobSkillProfile(string recommendationSkillDefName,
            IReadOnlyList<string> skills,
            IReadOnlyList<JobProfileSkillEffect> effects)
        {
            RecommendationSkillDefName = recommendationSkillDefName;
            UsedSkills = skills;
            TrainedSkills = skills;
            Effects = effects;
        }

        public IReadOnlyList<string> UsedSkills { get; }
        public IReadOnlyList<string> TrainedSkills { get; }
        public IReadOnlyList<JobProfileSkillEffect> Effects { get; }
        /// Detached recommendation discipline; the live work type is untouched.
        public string RecommendationSkillDefName { get; }

        public static OptionalJobSkillProfile? ForBrothel(
            string? giverDefName, string? giverPackageId, string? giverClassName,
            bool giverClassOwnedByPack, string? workTypeDefName,
            string? workTypePackageId, string? sexSkillDefName,
            string? sexSkillPackageId)
        {
            if (!string.Equals(giverDefName, "Soliciting", StringComparison.Ordinal)
                || !string.Equals(giverClassName, "BrothelColony.WorkGiver_Whore", StringComparison.Ordinal)
                || !giverClassOwnedByPack
                || !string.Equals(giverPackageId, BrothelPackage, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(workTypeDefName, "Brothel", StringComparison.Ordinal)
                || !string.Equals(workTypePackageId, BrothelPackage, StringComparison.OrdinalIgnoreCase))
                return null;
            return string.Equals(sexSkillDefName, "Sex", StringComparison.Ordinal)
                && string.Equals(sexSkillPackageId, "rjw.sexperience", StringComparison.OrdinalIgnoreCase)
                    ? WithSexperience : SocialOnly;
        }
    }
}
