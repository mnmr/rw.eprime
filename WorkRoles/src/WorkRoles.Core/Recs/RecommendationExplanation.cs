using System;
using System.Collections.Generic;
using System.Linq;

namespace WorkRoles.Core.Recs
{
    public enum RecommendationDecision
    {
        Recommended,
        AutoAssigned,
        SignalQualified,
        CoverageDrafted,
        Hunter,
        FireSafety,
        Retained,
        ProtectedAssignment,
        HolderModeNever,
        RoleDisabled,
        RoleUnavailable,
        RoleExcluded,
        PawnIncapable,
        HunterRequirementsNotMet,
        AwfulSignal,
        OutsideTrainingBand,
        CoveredByRecommendedRole,
        RequiredCoverageFilled,
        SignalBelowThreshold,
        NotSelected,
    }

    /// Immutable-enough result data used to explain a role decision without
    /// making the UI reconstruct recommendation rules from game state.
    public sealed class RoleRecommendationExplanation
    {
        public int RoleId;
        public bool Recommended;
        public RecommendationDecision Decision;
        public int RelatedRoleId = -1;
        public int RequiredHolders;
        public int RecommendedHolders;
        public int ConfiguredMaximum = RoleHolderRange.Uncapped;
        public IReadOnlyList<string> RequiredSkills = Array.Empty<string>();
        public SignalBucket SignalBucket = SignalBucket.Neutral;
        public string SignalSkillDefName;
        /// Need-driven ranking facts. CandidateRank == 0 means this pawn was
        /// not part of the role's remaining eligible pool.
        public int CandidateRank;
        public int CandidatePoolSize;
        public int CoverageOpenSlots;
        public string CandidateSkillDefName;
        public int CandidateSkillLevel;
    }

    internal static class RecommendationExplainer
    {
        internal static void Populate(EngineContext context)
        {
            for (int pawnIndex = 0; pawnIndex < context.Colony.Pawns.Count; pawnIndex++)
            {
                PawnView pawn = context.Colony.Pawns[pawnIndex];
                PawnResult result = context.Results[pawnIndex];
                var relevant = new SortedSet<int>();
                foreach (AssignmentView assignment in pawn.Existing)
                    relevant.Add(assignment.RoleId);
                foreach (AssignmentView assignment in result.Assignments)
                    relevant.Add(assignment.RoleId);

                var recommended = new HashSet<int>(
                    result.Assignments.Select(assignment => assignment.RoleId));
                foreach (int roleId in relevant)
                {
                    RoleView role = context.RoleOf(roleId);
                    if (role == null) continue;
                    var explanation = Facts(context, pawnIndex, role);
                    explanation.Recommended = recommended.Contains(roleId);
                    if (explanation.Recommended)
                    {
                        explanation.Decision = IncludedDecision(result, pawn, role);
                    }
                    else
                    {
                        explanation.Decision = RemovedDecision(
                            context, pawnIndex, result, role,
                            explanation.SignalBucket, out int relatedRoleId);
                        explanation.RelatedRoleId = relatedRoleId;
                    }
                    result.Explanations[roleId] = explanation;
                }
            }
        }

        private static RoleRecommendationExplanation Facts(
            EngineContext context,
            int pawnIndex,
            RoleView role)
        {
            SignalBucket signal = context.BestSignal(
                pawnIndex, role, out string signalSkill, out _);
            var explanation = new RoleRecommendationExplanation
            {
                RoleId = role.Id,
                RequiredHolders = context.Want.TryGetValue(role.Id, out int want) ? want : 0,
                RecommendedHolders = context.HoldersOf(role.Id),
                ConfiguredMaximum = role.MaxHolders,
                RequiredSkills = RequiredSkills(context, role),
                SignalBucket = signal,
                SignalSkillDefName = signalSkill,
            };
            if (context.DraftRankings[pawnIndex].TryGetValue(role.Id, out DraftRanking ranking))
            {
                explanation.CandidateRank = ranking.Rank;
                explanation.CandidatePoolSize = ranking.EligibleCount;
                explanation.CoverageOpenSlots = ranking.OpenSlots;
                explanation.CandidateSkillDefName = ranking.SkillDefName;
                explanation.CandidateSkillLevel = ranking.SkillLevel;
            }
            return explanation;
        }

        private static IReadOnlyList<string> RequiredSkills(
            EngineContext context,
            RoleView role)
        {
            var skills = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string workType in role.WorkTypes)
                if (context.Colony.WorkTypeSkills.TryGetValue(workType, out var mapped))
                    foreach (string skill in mapped)
                        if (!string.IsNullOrWhiteSpace(skill)) skills.Add(skill);
            return skills.Count == 0 ? Array.Empty<string>() : skills.ToList();
        }

        private static RecommendationDecision IncludedDecision(
            PawnResult result,
            PawnView pawn,
            RoleView role)
        {
            if (result.Reasons.TryGetValue(role.Id, out Reason reason))
            {
                switch (reason.RuleId)
                {
                    case "auto": return RecommendationDecision.AutoAssigned;
                    case "signals": return RecommendationDecision.SignalQualified;
                    case "draft": return RecommendationDecision.CoverageDrafted;
                    case "hunter": return RecommendationDecision.Hunter;
                    case "fire": return RecommendationDecision.FireSafety;
                    case "retention": return RecommendationDecision.Retained;
                    default: return RecommendationDecision.Recommended;
                }
            }

            if (pawn.Existing.Any(assignment => assignment.RoleId == role.Id
                    && (assignment.Pinned || role.HasRules || role.Blocker)))
                return RecommendationDecision.ProtectedAssignment;
            return RecommendationDecision.Recommended;
        }

        private static RecommendationDecision RemovedDecision(
            EngineContext context,
            int pawnIndex,
            PawnResult result,
            RoleView role,
            SignalBucket signal,
            out int relatedRoleId)
        {
            relatedRoleId = -1;
            if (role.HolderMode == RoleHolderMode.Never)
                return RecommendationDecision.HolderModeNever;
            if (!role.Enabled) return RecommendationDecision.RoleDisabled;
            if (!role.Available) return RecommendationDecision.RoleUnavailable;
            if (role.HasRules || role.Blocker)
                return RecommendationDecision.RoleExcluded;
            if (!context.Capable(pawnIndex, role))
                return RecommendationDecision.PawnIncapable;
            if (role.Hunting && !context.Colony.Pawns[pawnIndex].HasRangedWeapon)
                return RecommendationDecision.HunterRequirementsNotMet;
            if (signal == SignalBucket.Awful)
                return RecommendationDecision.AwfulSignal;
            // Bands reject only Strong+ interest candidates. The coverage
            // draft uses band membership as ranking context but may still
            // select an out-of-band pawn to satisfy the required floor.
            if (!role.AutoAssign && !role.Unskilled && !role.Hunting
                && signal >= SignalBucket.Strong
                && !context.PassesBands(pawnIndex, role))
                return RecommendationDecision.OutsideTrainingBand;

            relatedRoleId = CoveringRole(context, result, role);
            if (relatedRoleId != -1)
                return RecommendationDecision.CoveredByRecommendedRole;

            if (context.Want.TryGetValue(role.Id, out int want)
                && context.HoldersOf(role.Id) >= want)
                return RecommendationDecision.RequiredCoverageFilled;
            if (signal < SignalBucket.Strong)
                return RecommendationDecision.SignalBelowThreshold;
            return RecommendationDecision.NotSelected;
        }

        private static int CoveringRole(
            EngineContext context,
            PawnResult result,
            RoleView role)
        {
            foreach (AssignmentView assignment in result.Assignments)
            {
                RoleView other = context.RoleOf(assignment.RoleId);
                if (other == null || other.Id == role.Id || other.Blocker) continue;
                if (CoverageMath.MakesRedundant(
                        other.Coverage, other.Id, role.Coverage, role.Id))
                    return other.Id;
            }
            return -1;
        }
    }
}
