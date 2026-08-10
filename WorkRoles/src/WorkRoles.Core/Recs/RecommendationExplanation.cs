using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// Selects the per-pawn role display-ordering implementation. Current is the
    /// production ordering (keys + score + path anchors). Experimental is the
    /// redesigned path, wired in parallel for comparison and never used by
    /// production until it replaces Current.
    public enum RoleOrderingStrategy
    {
        Current,
        Experimental,
    }

    public enum RecommendationSelectionStage
    {
        None,
        Required,
        TrainingWaiver,
        CoverageRepair,
        Surplus,
        Special,
    }

    /// Why a role was assigned outside the scored single pass (stage Special).
    public enum SpecialPickReason
    {
        None,
        AutoAssigned,
        Hunter,
        FireSafety,
        Retained,
        Protected,
    }

    /// Why a role a pawn holds was not recommended. Pick-outcome reasons are
    /// stamped by the single pass; the path-controlled reasons are derived from
    /// role and path structure.
    public enum PickRejectReason
    {
        None,
        Incapable,
        HunterRequirementsNotMet,
        AwfulSignal,
        WeakSignal,
        OutOfBand,
        Covered,
        RequiredCoverageFilled,
        Outqualified,
        ControlledByTarget,
        ScaleNever,
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
        public SpecialPickReason SpecialPickReason;
        public PickRejectReason RejectReason;
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
