using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace WorkRoles.Core.Recs
{
    public readonly struct RecommendationWorkTypeEvidence
    {
        public RecommendationWorkTypeEvidence(string defName, int naturalPriority)
        {
            DefName = defName;
            NaturalPriority = naturalPriority;
        }

        public string DefName { get; }
        public int NaturalPriority { get; }
    }

    public readonly struct RecommendationSkillEvidence
    {
        internal RecommendationSkillEvidence(RoleSkillView skill)
        {
            SkillDefName = skill.SkillDefName;
            Primary = skill.Primary;
            Required = skill.Required;
            Importance = skill.Importance;
            UsedJobs = skill.UsedJobs;
            TrainedJobs = skill.TrainedJobs;
            RequiredContent = skill.RequiredContent;
        }

        public string SkillDefName { get; }
        public bool Primary { get; }
        public bool Required { get; }
        public int Importance { get; }
        public int UsedJobs { get; }
        public int TrainedJobs { get; }
        public int RequiredContent { get; }

        internal RoleSkillView ToView() => new RoleSkillView
        {
            SkillDefName = SkillDefName,
            Primary = Primary,
            Required = Required,
            Importance = Importance,
            UsedJobs = UsedJobs,
            TrainedJobs = TrainedJobs,
            RequiredContent = RequiredContent,
        };
    }

    /// Immutable role facts shared by every recommendation consumer in one
    /// planning run. Mutable RoleView lists are materialized as owned copies.
    public sealed class RecommendationRoleProjection
    {
        private readonly HashSet<string> literalWorkTypeSet;

        public RecommendationRoleProjection(
            IEnumerable<RecommendationWorkTypeEvidence> workTypes,
            IEnumerable<string> literalWorkTypes,
            IEnumerable<RoleSkillEvidence> skillEvidence)
        {
            var orderedWorkTypes = new List<string>();
            var priorities = new Dictionary<string, int>(StringComparer.Ordinal);
            if (workTypes != null)
                foreach (RecommendationWorkTypeEvidence workType in workTypes)
                {
                    if (string.IsNullOrEmpty(workType.DefName)
                        || priorities.ContainsKey(workType.DefName)) continue;
                    priorities.Add(workType.DefName, workType.NaturalPriority);
                    orderedWorkTypes.Add(workType.DefName);
                    if (workType.DefName == "Hunting") Hunting = true;
                    if (workType.NaturalPriority > MaxNaturalPriority)
                        MaxNaturalPriority = workType.NaturalPriority;
                }

            literalWorkTypeSet = new HashSet<string>(StringComparer.Ordinal);
            if (literalWorkTypes != null)
                foreach (string literal in literalWorkTypes)
                    if (!string.IsNullOrEmpty(literal))
                        literalWorkTypeSet.Add(literal);

            var skillViews = RoleSkillProfile.Build(
                skillEvidence ?? Array.Empty<RoleSkillEvidence>());
            var skills = new List<RecommendationSkillEvidence>(skillViews.Count);
            for (int i = 0; i < skillViews.Count; i++)
            {
                skills.Add(new RecommendationSkillEvidence(skillViews[i]));
                if (skillViews[i].Primary)
                    PrimarySkill = skillViews[i].SkillDefName;
            }

            WorkTypes = new ReadOnlyCollection<string>(orderedWorkTypes);
            NaturalPriorities = new ReadOnlyDictionary<string, int>(priorities);
            SkillEvidence = new ReadOnlyCollection<RecommendationSkillEvidence>(skills);
        }

        public IReadOnlyList<string> WorkTypes { get; }
        public IReadOnlyDictionary<string, int> NaturalPriorities { get; }
        public bool Hunting { get; }
        public int MaxNaturalPriority { get; }
        public IReadOnlyList<RecommendationSkillEvidence> SkillEvidence { get; }
        public string PrimarySkill { get; }
        public bool HasSkillEvidence => SkillEvidence.Count > 0;

        public bool HasLiteralWorkType(string defName) =>
            defName != null && literalWorkTypeSet.Contains(defName);

        public List<string> CopyWorkTypes() => new List<string>(WorkTypes);

        public List<RoleSkillView> CopySkillViews()
        {
            var result = new List<RoleSkillView>(SkillEvidence.Count);
            for (int i = 0; i < SkillEvidence.Count; i++)
                result.Add(SkillEvidence[i].ToView());
            return result;
        }
    }
}
