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
                    HolderRequirement requirement = facts.RequirementOf(
                        requirementRole.Id);
                    int requiredTotal = System.Math.Min(
                        facts.Colony.Pawns.Count, requirement.RequiredTotal);
                    var explanation = new RoleRecommendationExplanation
                    {
                        RoleId = roleId,
                        Recommended = recommended,
                        RequiredTotal = requiredTotal,
                        TrainingWaivers = System.Math.Min(
                            requiredTotal, requirement.TrainingWaivers),
                        CoveredTotal = coveredTotals.TryGetValue(
                            requirementRole.Id, out int covered) ? covered : 0,
                        DemandApplies = requirementRole.PlannedByDemand
                            && !requirementRole.IsNever,
                        EveryoneCapable =
                            requirementRole.CoveragePercent >= 100,
                        RequiredSkills = RequiredSkillNames(facts, role),
                        SignalBucket = surplusSignal,
                        BaseSignalBucket = rawSignal,
                        SignalSkillLevel = signalSkillLevel,
                        SignalSkillDefName = signalSkill,
                    };
                    RecommendationTargetAssignment? selection =
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

        private static RecommendationTargetAssignment? FindTargetAssignment(
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

            // The published training skills are the path's qualifying set,
            // as the pre-spec engine derived it.
            string[] covered = facts.PathSkills(path).Qualifying;
            var result = new List<RecommendationTrainingSkill>(covered.Length);
            int targetMinimum = path.BandMins[targetAt];
            for (int index = 0; index < covered.Length; index++)
                result.Add(new RecommendationTrainingSkill(
                    covered[index],
                    facts.SkillLevel(pawnIndex, covered[index]),
                    targetMinimum));
            return result.Count == 0
                ? Array.Empty<RecommendationTrainingSkill>()
                : result.ToArray();
        }

        private static IReadOnlyList<string> RequiredSkillNames(
            EngineContext facts,
            RoleView role)
        {
            IReadOnlyList<RoleSkillFact> required =
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
            // A role assigned outside the scored pass has no stage yet; it takes
            // the Special stage and the reason the assigning logic used. A role
            // the pass picked already carries its stage and score stats.
            if (explanation.SelectionStage == RecommendationSelectionStage.None)
            {
                SpecialPickReason special = SpecialReasonFor(facts, pawn, role);
                if (special != SpecialPickReason.None)
                {
                    explanation.SelectionStage =
                        RecommendationSelectionStage.Special;
                    explanation.SpecialPickReason = special;
                    return;
                }
            }
            // A downgraded waiver or surplus records the target it fell from so
            // the tooltip can name it; the stage and score stats carry the rest.
            int targetRoleId = draft.TrainingTargetFor(role.Id);
            if (targetRoleId >= 0) explanation.RelatedRoleId = targetRoleId;
        }

        private static SpecialPickReason SpecialReasonFor(
            EngineContext facts, PawnView pawn, RoleView role)
        {
            if (role.AutoAssign) return SpecialPickReason.AutoAssigned;
            if (role.Id == facts.Colony.HunterRoleId)
                return SpecialPickReason.Hunter;
            if (role.Id == facts.Colony.FireBlockerRoleId && pawn.FireFear)
                return SpecialPickReason.FireSafety;
            if (IsProtectedExisting(pawn, role))
                return SpecialPickReason.Protected;
            if (role.UseUnskilledPlacementRules && HasExisting(pawn, role.Id))
                return SpecialPickReason.Retained;
            return SpecialPickReason.None;
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
            if (role.PlannedByDemand && !role.HasDemand)
            {
                // A no-demand role that is a training-path trainee is controlled
                // by its target (e.g. Medic by Doctor); an unskilled no-demand
                // role is simply configured off. Standalone skilled no-demand
                // roles stay surplus-eligible and fall through to the ordinary
                // reject reasons.
                int controllingTarget = ControllingTrainingTarget(
                    facts, role.Id);
                if (controllingTarget >= 0)
                {
                    explanation.RejectReason =
                        PickRejectReason.ControlledByTarget;
                    explanation.RelatedRoleId = controllingTarget;
                    return;
                }
                if (role.IsNever)
                {
                    explanation.RejectReason = PickRejectReason.ScaleNever;
                    return;
                }
            }
            // Role on/off state (disabled, unavailable, rule-driven, blocker) is
            // not a recommendation decision and carries no reject reason.
            if (!role.Enabled || !role.Available
                || role.HasRules || role.Blocker)
                return;
            if (!facts.MeetsCapabilityRequirement(pawnIndex, role))
            {
                explanation.RejectReason = PickRejectReason.Incapable;
                return;
            }
            if (role.Hunting && !pawn.HasRangedWeapon)
            {
                explanation.RejectReason =
                    PickRejectReason.HunterRequirementsNotMet;
                return;
            }
            if (rawSignal < formulas.CandidateMinimumSignal)
            {
                explanation.RejectReason = rawSignal == SignalBucket.Awful
                    ? PickRejectReason.AwfulSignal
                    : PickRejectReason.WeakSignal;
                return;
            }
            if (PathActivation.BelongsToHigherBand(facts, pawnIndex, role))
            {
                explanation.RejectReason = PickRejectReason.OutOfBand;
                return;
            }
            int coveringRoleId = CoveringRole(facts, draft, pawnIndex, role);
            if (coveringRoleId >= 0)
            {
                explanation.RejectReason = PickRejectReason.Covered;
                explanation.RelatedRoleId = coveringRoleId;
                return;
            }
            if (explanation.CoveredTotal >= requiredTotal && requiredTotal > 0)
            {
                explanation.RejectReason =
                    PickRejectReason.RequiredCoverageFilled;
                return;
            }
            if (surplusSignal < formulas.SurplusMinimumSignal)
            {
                explanation.RejectReason = PickRejectReason.WeakSignal;
                return;
            }
            // Capable, adequate, uncovered, in-band: it simply lost the slots to
            // better-scored candidates.
            explanation.RejectReason = PickRejectReason.Outqualified;
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
            if (!facts.MeetsCapabilityRequirement(pawnIndex, role)) return -1;
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
