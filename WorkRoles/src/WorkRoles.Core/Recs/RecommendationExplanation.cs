using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    public enum RecommendationDecision
    {
        Recommended,
        AutoAssigned,
        SignalQualified,
        CoverageDrafted,
        Training,
        Hunter,
        FireSafety,
        Retained,
        ProtectedAssignment,
        ScaleNever,
        ControlledByTrainingTarget,
        RoleDisabled,
        RoleUnavailable,
        RoleExcluded,
        PawnIncapable,
        HunterRequirementsNotMet,
        AwfulSignal,
        CoveredByRecommendedRole,
        RequiredCoverageFilled,
        SignalBelowThreshold,
        NotSelected,
    }

    public enum RecommendationSelectionStage
    {
        None,
        Required,
        TrainingWaiver,
        DirectFallback,
        CoverageRepair,
        Surplus,
    }

    public sealed class RecommendationTrainingSkill
    {
        internal RecommendationTrainingSkill(
            string skillDefName,
            int pawnLevel,
            int targetMinimum)
        {
            SkillDefName = skillDefName;
            PawnLevel = pawnLevel;
            TargetMinimum = targetMinimum;
        }

        public string SkillDefName { get; }
        public int PawnLevel { get; }
        public int TargetMinimum { get; }
    }

    /// Published decision facts consumed by recommendation tooltips. The new
    /// planner owns this contract; the UI never reconstructs planner rules.
    public sealed class RoleRecommendationExplanation
    {
        public int RoleId;
        public bool Recommended;
        public RecommendationDecision Decision;
        public int RelatedRoleId = -1;
        public int RequiredTotal;
        public int TrainingWaivers;
        public int CoveredTotal;
        public int ConfiguredMaximum = RoleHolderRange.Uncapped;
        public bool HolderScaleApplies;
        public RecommendationSelectionStage SelectionStage;
        public int CandidateRank;
        public int CandidateCount;
        public int StageRank;
        public int SelectionSlot;
        public int SelectionSlotCount;
        public IReadOnlyList<RecommendationTrainingSkill> TrainingSkills =
            Array.Empty<RecommendationTrainingSkill>();
        public SignalBucket SelectionSignalBucket = SignalBucket.Neutral;
        public SignalBucket SurplusMinimumSignalBucket = SignalBucket.Strong;
        public bool SurplusQualifiedBySignal;
        public IReadOnlyList<string> RequiredSkills = Array.Empty<string>();
        public SignalBucket SignalBucket = SignalBucket.Neutral;
        public SignalBucket BaseSignalBucket = SignalBucket.Neutral;
        public int SignalSkillLevel = -1;
        public string SignalSkillDefName;
    }
}
