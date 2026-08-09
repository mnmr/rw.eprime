using System;
using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    public sealed partial class RecommendationPlan
    {
        private static Dictionary<int, RoleRecommendationExplanation>[]
            BuildExplanations(
                EngineContext facts,
                PawnDraft[] drafts,
                RecommendationFormulaEngine formulas,
                RecommendationTargetAssignment[] targetAssignments)
        {
            var coveredTotals = new Dictionary<int, int>();
            for (int roleIndex = 0;
                 roleIndex < facts.Colony.Roles.Count;
                 roleIndex++)
            {
                RoleView role = facts.Colony.Roles[roleIndex];
                coveredTotals[role.Id] = CountCoveredPawns(
                    facts, drafts, role);
            }

            var result = new Dictionary<
                int, RoleRecommendationExplanation>[drafts.Length];
            for (int pawnIndex = 0;
                 pawnIndex < drafts.Length;
                 pawnIndex++)
            {
                var explanations = new Dictionary<
                    int, RoleRecommendationExplanation>();
                result[pawnIndex] = explanations;
                var relevant = new SortedSet<int>();
                PawnView pawn = facts.Colony.Pawns[pawnIndex];
                for (int index = 0; index < pawn.Existing.Count; index++)
                    relevant.Add(pawn.Existing[index].RoleId);
                for (int index = 0; index < drafts[pawnIndex].RoleCount; index++)
                    relevant.Add(drafts[pawnIndex].RoleAt(index));

                foreach (int roleId in relevant)
                {
                    RoleView role = facts.RoleOf(roleId);
                    if (role == null) continue;
                    bool recommended = drafts[pawnIndex].ContainsRole(roleId);
                    SignalBucket rawSignal = facts.BestSignal(
                        pawnIndex,
                        role,
                        out string signalSkill,
                        out _);
                    int signalSkillLevel = facts.SkillLevel(
                        pawnIndex, signalSkill);
                    SignalBucket surplusSignal = RolePlan.SurplusSignal(
                        facts,
                        pawnIndex,
                        role,
                        rawSignal,
                        signalSkillLevel,
                        formulas);
                    int trainingTargetRoleId = drafts[pawnIndex]
                        .TrainingTargetFor(role.Id);
                    RoleView requirementRole = trainingTargetRoleId >= 0
                        ? facts.RoleOf(trainingTargetRoleId) ?? role
                        : role;
                    HolderRequirement requirement = requirementRole.RequirementAt(
                        facts.Colony.Pawns.Count);
                    int requiredTotal = requirement.RequiredTotal;
                    var explanation = new RoleRecommendationExplanation
                    {
                        RoleId = roleId,
                        Recommended = recommended,
                        RequiredTotal = requiredTotal,
                        TrainingWaivers = System.Math.Min(
                            requiredTotal, requirement.TrainingWaivers),
                        CoveredTotal = coveredTotals.TryGetValue(
                            requirementRole.Id, out int covered) ? covered : 0,
                        ConfiguredMaximum = requirementRole.MaxHoldersAt(
                            facts.Colony.Pawns.Count),
                        HolderScaleApplies = requirementRole.UsesHolderScale
                            && requirementRole.Scale != null,
                        RequiredSkills = RequiredSkillNames(facts, role),
                        SignalBucket = surplusSignal,
                        BaseSignalBucket = rawSignal,
                        SignalSkillLevel = signalSkillLevel,
                        SignalSkillDefName = signalSkill,
                    };
                    RecommendationTargetAssignment selection =
                        FindTargetAssignment(
                            targetAssignments,
                            pawnIndex,
                            roleId,
                            trainingTargetRoleId);
                    if (selection != null)
                    {
                        RecommendationSelectionStage stage = selection.Stage;
                        if (stage ==
                                RecommendationSelectionStage.TrainingWaiver
                            && selection.AssignsTargetRole)
                            stage = roleId == selection.TargetRoleId
                                ? RecommendationSelectionStage.Required
                                : RecommendationSelectionStage.None;
                        explanation.SelectionStage = stage;
                        explanation.CandidateRank = selection.CandidateRank;
                        explanation.CandidateCount = selection.CandidateCount;
                        explanation.StageRank = selection.StageRank;
                        explanation.SelectionSlot = selection.SelectionSlot;
                        explanation.SelectionSlotCount =
                            selection.SelectionSlotCount;
                        explanation.SelectionSignalBucket =
                            selection.SignalBucket;
                        explanation.SurplusMinimumSignalBucket =
                            selection.SurplusMinimumSignalBucket;
                        explanation.SurplusQualifiedBySignal =
                            selection.SurplusQualifiedBySignal;
                        if (stage ==
                            RecommendationSelectionStage.TrainingWaiver)
                            explanation.TrainingSkills = TrainingSkills(
                                facts,
                                pawnIndex,
                                requirementRole,
                                selection.PathId);
                    }
                    if (recommended)
                        IncludedDecision(
                            facts,
                            drafts[pawnIndex],
                            pawnIndex,
                            role,
                            surplusSignal,
                            formulas,
                            explanation);
                    else
                        RemovedDecision(
                            facts,
                            drafts[pawnIndex],
                            pawnIndex,
                            role,
                            rawSignal,
                            surplusSignal,
                            requiredTotal,
                            formulas,
                            explanation);
                    explanations.Add(roleId, explanation);
                }
            }
            return result;
        }

        private static RecommendationTargetAssignment FindTargetAssignment(
            RecommendationTargetAssignment[] assignments,
            int pawnIndex,
            int roleId,
            int trainingTargetRoleId)
        {
            int targetRoleId = trainingTargetRoleId >= 0
                ? trainingTargetRoleId
                : roleId;
            for (int index = 0; index < assignments.Length; index++)
            {
                RecommendationTargetAssignment assignment = assignments[index];
                if (assignment.PawnIndex != pawnIndex
                    || assignment.TargetRoleId != targetRoleId)
                    continue;
                for (int roleIndex = 0;
                     roleIndex < assignment.RoleCount;
                     roleIndex++)
                    if (assignment.RoleAt(roleIndex) == roleId)
                        return assignment;
            }
            return null;
        }

        private static IReadOnlyList<RecommendationTrainingSkill>
            TrainingSkills(
                EngineContext facts,
                int pawnIndex,
                RoleView target,
                int pathId)
        {
            if (target == null
                || !facts.PathsById.TryGetValue(pathId, out PathView path))
                return Array.Empty<RecommendationTrainingSkill>();
            int targetAt = path.RoleIds.IndexOf(target.Id);
            if (targetAt < 0 || targetAt >= path.BandMins.Count)
                return Array.Empty<RecommendationTrainingSkill>();

            IReadOnlyList<RoleSkillView> required =
                facts.RequiredSkills(target);
            var result = new List<RecommendationTrainingSkill>(
                required.Count);
            int targetMinimum = path.BandMins[targetAt];
            for (int index = 0; index < required.Count; index++)
            {
                RoleSkillView skill = required[index];
                if (!PathActivation.IsQualifyingTargetSkill(
                        facts, target, path, skill))
                    continue;
                result.Add(new RecommendationTrainingSkill(
                    skill.SkillDefName,
                    facts.SkillLevel(pawnIndex, skill.SkillDefName),
                    targetMinimum));
            }
            return result.Count == 0
                ? Array.Empty<RecommendationTrainingSkill>()
                : result.ToArray();
        }

        private static IReadOnlyList<string> RequiredSkillNames(
            EngineContext facts,
            RoleView role)
        {
            IReadOnlyList<RoleSkillView> required =
                facts.RequiredSkills(role);
            if (required.Count == 0) return Array.Empty<string>();
            var names = new SortedSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < required.Count; index++)
                names.Add(required[index].SkillDefName);
            var result = new string[names.Count];
            names.CopyTo(result);
            return result;
        }

        private static void IncludedDecision(
            EngineContext facts,
            PawnDraft draft,
            int pawnIndex,
            RoleView role,
            SignalBucket surplusSignal,
            RecommendationFormulaEngine formulas,
            RoleRecommendationExplanation explanation)
        {
            PawnView pawn = facts.Colony.Pawns[pawnIndex];
            if (role.AutoAssign)
            {
                explanation.Decision = RecommendationDecision.AutoAssigned;
                return;
            }
            if (role.Id == facts.Colony.HunterRoleId)
            {
                explanation.Decision = RecommendationDecision.Hunter;
                return;
            }
            if (role.Id == facts.Colony.FireBlockerRoleId && pawn.FireFear)
            {
                explanation.Decision = RecommendationDecision.FireSafety;
                return;
            }
            if (IsProtectedExisting(pawn, role))
            {
                explanation.Decision =
                    RecommendationDecision.ProtectedAssignment;
                return;
            }
            if (role.Unskilled && HasExisting(pawn, role.Id))
            {
                explanation.Decision = RecommendationDecision.Retained;
                return;
            }
            int targetRoleId = draft.TrainingTargetFor(role.Id);
            if (targetRoleId >= 0)
            {
                explanation.Decision = RecommendationDecision.Training;
                explanation.RelatedRoleId = targetRoleId;
                return;
            }
            explanation.Decision = draft.IsMinimumRole(role.Id)
                ? RecommendationDecision.CoverageDrafted
                : surplusSignal >= formulas.SurplusMinimumSignal
                    ? RecommendationDecision.SignalQualified
                    : RecommendationDecision.Recommended;
        }

        private static void RemovedDecision(
            EngineContext facts,
            PawnDraft draft,
            int pawnIndex,
            RoleView role,
            SignalBucket rawSignal,
            SignalBucket surplusSignal,
            int requiredTotal,
            RecommendationFormulaEngine formulas,
            RoleRecommendationExplanation explanation)
        {
            PawnView pawn = facts.Colony.Pawns[pawnIndex];
            if (role.UsesHolderScale && role.IsNever)
            {
                // A Never role that is a training-path trainee is controlled by
                // its target (e.g. Medic by Doctor), not "not configured".
                int controllingTarget = ControllingTrainingTarget(
                    facts, role.Id);
                if (controllingTarget >= 0)
                {
                    explanation.Decision =
                        RecommendationDecision.ControlledByTrainingTarget;
                    explanation.RelatedRoleId = controllingTarget;
                }
                else
                    explanation.Decision = RecommendationDecision.ScaleNever;
            }
            else if (!role.Enabled)
                explanation.Decision = RecommendationDecision.RoleDisabled;
            else if (!role.Available)
                explanation.Decision = RecommendationDecision.RoleUnavailable;
            else if (role.HasRules || role.Blocker)
                explanation.Decision = RecommendationDecision.RoleExcluded;
            else if (!facts.Capable(pawnIndex, role))
                explanation.Decision = RecommendationDecision.PawnIncapable;
            else if (role.Hunting && !pawn.HasRangedWeapon)
                explanation.Decision =
                    RecommendationDecision.HunterRequirementsNotMet;
            else if (rawSignal < formulas.CandidateMinimumSignal)
                explanation.Decision = rawSignal == SignalBucket.Awful
                    ? RecommendationDecision.AwfulSignal
                    : RecommendationDecision.SignalBelowThreshold;
            else
            {
                int coveringRoleId = CoveringRole(
                    facts, draft, pawnIndex, role);
                if (coveringRoleId >= 0)
                {
                    explanation.Decision =
                        RecommendationDecision.CoveredByRecommendedRole;
                    explanation.RelatedRoleId = coveringRoleId;
                }
                else if (explanation.CoveredTotal >= requiredTotal
                    && requiredTotal > 0)
                    explanation.Decision =
                        RecommendationDecision.RequiredCoverageFilled;
                else if (surplusSignal < formulas.SurplusMinimumSignal)
                    explanation.Decision =
                        RecommendationDecision.SignalBelowThreshold;
                else
                    explanation.Decision = RecommendationDecision.NotSelected;
            }
        }

        /// The training-path target that controls this role (the role is a
        /// non-target member of a path), or -1 when the role stands alone.
        private static int ControllingTrainingTarget(
            EngineContext facts, int roleId)
        {
            for (int index = 0; index < facts.Colony.Paths.Count; index++)
            {
                PathView path = facts.Colony.Paths[index];
                if (!path.RoleIds.Contains(roleId)) continue;
                int target = PathActivation.UniqueTargetRoleId(path);
                if (target >= 0 && target != roleId) return target;
            }
            return -1;
        }

        private static int CoveringRole(
            EngineContext facts,
            PawnDraft draft,
            int pawnIndex,
            RoleView role)
        {
            if (!facts.FullyCapable(pawnIndex, role)) return -1;
            for (int index = 0; index < draft.RoleCount; index++)
            {
                int candidateRoleId = draft.RoleAt(index);
                if (candidateRoleId == role.Id) continue;
                RoleView candidate = facts.RoleOf(candidateRoleId);
                if (candidate == null || candidate.Blocker) continue;
                if (CoverageMath.Covers(candidate.Coverage, role.Coverage))
                    return candidateRoleId;
            }
            return -1;
        }

        private static bool IsProtectedExisting(PawnView pawn, RoleView role)
        {
            for (int index = 0; index < pawn.Existing.Count; index++)
            {
                AssignmentView assignment = pawn.Existing[index];
                if (assignment.RoleId == role.Id
                    && (assignment.Pinned || role.HasRules || role.Blocker))
                    return true;
            }
            return false;
        }

        private static bool HasExisting(PawnView pawn, int roleId)
        {
            for (int index = 0; index < pawn.Existing.Count; index++)
                if (pawn.Existing[index].RoleId == roleId) return true;
            return false;
        }
    }
}
