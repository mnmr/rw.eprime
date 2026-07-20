using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// Reusable scratch for reducing flat per-giver facts into one role's
    /// skill evidence. CompleteRole's list is valid until the next BeginRole.
    public sealed class RoleSkillEvidenceAccumulator
    {
        private sealed class SkillTotals
        {
            public long RoleStamp;
            public long UsedSourceStamp;
            public long TrainedSourceStamp;
            public int UsedJobs;
            public int TrainedJobs;
            public int RequiredContent;
            public string SkillDefName;
        }

        private readonly Dictionary<string, SkillTotals> totalsBySkill =
            new Dictionary<string, SkillTotals>(StringComparer.Ordinal);
        private readonly HashSet<string> seenSources =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly List<SkillTotals> roleTotals = new List<SkillTotals>();
        private readonly List<RoleSkillEvidence> flatEvidence =
            new List<RoleSkillEvidence>();
        private long roleStamp;
        private long sourceStamp;
        private bool sourceActive;

        public void BeginRole()
        {
            if (roleStamp == long.MaxValue)
            {
                totalsBySkill.Clear();
                roleStamp = 0;
            }
            roleStamp++;
            seenSources.Clear();
            roleTotals.Clear();
            flatEvidence.Clear();
            sourceActive = false;
        }

        public bool BeginSource(string sourceKey)
        {
            sourceActive = sourceKey != null && seenSources.Add(sourceKey);
            if (!sourceActive) return false;
            if (sourceStamp == long.MaxValue)
            {
                foreach (SkillTotals totals in totalsBySkill.Values)
                {
                    totals.UsedSourceStamp = 0;
                    totals.TrainedSourceStamp = 0;
                }
                sourceStamp = 0;
            }
            sourceStamp++;
            return true;
        }

        public void AddUsedSkill(string skillDefName)
        {
            SkillTotals totals = Current(skillDefName);
            if (totals == null || totals.UsedSourceStamp == sourceStamp) return;
            totals.UsedSourceStamp = sourceStamp;
            totals.UsedJobs++;
        }

        public void AddTrainedSkill(string skillDefName)
        {
            SkillTotals totals = Current(skillDefName);
            if (totals == null || totals.TrainedSourceStamp == sourceStamp) return;
            totals.TrainedSourceStamp = sourceStamp;
            totals.TrainedJobs++;
        }

        public void AddRequiredContent(string skillDefName, int requiredContent)
        {
            SkillTotals totals = Current(skillDefName);
            if (totals != null)
                totals.RequiredContent += Math.Max(1, requiredContent);
        }

        public IReadOnlyList<RoleSkillEvidence> CompleteRole()
        {
            flatEvidence.Clear();
            for (int i = 0; i < roleTotals.Count; i++)
            {
                SkillTotals totals = roleTotals[i];
                flatEvidence.Add(new RoleSkillEvidence(totals.SkillDefName,
                    totals.UsedJobs, totals.TrainedJobs, totals.RequiredContent));
            }
            flatEvidence.Sort((left, right) => string.CompareOrdinal(
                left.SkillDefName, right.SkillDefName));
            sourceActive = false;
            return flatEvidence;
        }

        private SkillTotals Current(string skillDefName)
        {
            if (!sourceActive || string.IsNullOrEmpty(skillDefName)) return null;
            if (!totalsBySkill.TryGetValue(skillDefName, out SkillTotals totals))
            {
                totals = new SkillTotals { SkillDefName = skillDefName };
                totalsBySkill.Add(skillDefName, totals);
            }
            if (totals.RoleStamp != roleStamp)
            {
                totals.RoleStamp = roleStamp;
                totals.UsedSourceStamp = 0;
                totals.TrainedSourceStamp = 0;
                totals.UsedJobs = 0;
                totals.TrainedJobs = 0;
                totals.RequiredContent = 0;
                roleTotals.Add(totals);
            }
            return totals;
        }
    }
}
