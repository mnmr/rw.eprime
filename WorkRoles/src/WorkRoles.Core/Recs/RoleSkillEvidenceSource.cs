using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// Reduces per-giver job-profile facts into one role's skill evidence.
    /// Shared by the game adapter (live JobProfileIndex) and offline harnesses
    /// (generated vanilla baseline index), so both derive identical profiles.
    public static class RoleSkillEvidenceSource
    {
        public static IReadOnlyList<RoleSkillEvidence> ForCoverage(
            IEnumerable<string> giverNames,
            IReadOnlyDictionary<string, JobProfileGiverFacts> givers,
            RoleSkillEvidenceAccumulator scratch)
        {
            if (givers == null)
                throw new System.ArgumentNullException(nameof(givers));
            if (scratch == null)
                throw new System.ArgumentNullException(nameof(scratch));
            scratch.BeginRole();
            if (giverNames == null) return scratch.CompleteRole();

            foreach (string giverName in giverNames)
            {
                if (!scratch.BeginSource(giverName)) continue;
                if (!givers.TryGetValue(giverName, out JobProfileGiverFacts facts))
                    continue;
                if (facts.TrainedSkillDefNames.Count > 0)
                    scratch.SetSourceWeight(4);
                for (int i = 0; i < facts.UsedSkillDefNames.Count; i++)
                    scratch.AddUsedSkill(facts.UsedSkillDefNames[i]);
                for (int i = 0; i < facts.TrainedSkillDefNames.Count; i++)
                    scratch.AddTrainedSkill(facts.TrainedSkillDefNames[i]);
                for (int i = 0; i < facts.Requirements.Count; i++)
                {
                    JobProfileRequirementFacts requirement = facts.Requirements[i];
                    scratch.AddRequiredContent(
                        requirement.SkillDefName, requirement.Gated);
                }
            }
            return scratch.CompleteRole();
        }
    }
}
