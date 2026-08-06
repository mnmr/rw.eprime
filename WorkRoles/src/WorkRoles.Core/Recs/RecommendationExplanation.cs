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
        HolderModeNever,
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

    /// Published decision facts consumed by recommendation tooltips. The new
    /// planner owns this contract; the UI never reconstructs planner rules.
    public sealed class RoleRecommendationExplanation
    {
        public int RoleId;
        public bool Recommended;
        public RecommendationDecision Decision;
        public int RelatedRoleId = -1;
        public int RequiredTotal;
        public int CoveredTotal;
        public int ConfiguredMaximum = RoleHolderRange.Uncapped;
        public IReadOnlyList<string> RequiredSkills = Array.Empty<string>();
        public SignalBucket SignalBucket = SignalBucket.Neutral;
        public string SignalSkillDefName;
    }
}
