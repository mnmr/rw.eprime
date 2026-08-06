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
                IScalingAlgorithm scaling)
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
                    SignalBucket signal = facts.BestSignal(
                        pawnIndex,
                        role,
                        out string signalSkill,
                        out _);
                    HolderRequirement requirement = scaling.Requirement(
                        role, facts.Colony.Pawns.Count);
                    int capacity = facts.Colony.Pawns.Count;
                    int maximum = role.MaxHoldersAt(capacity);
                    if (maximum < RoleHolderRange.Uncapped)
                        capacity = System.Math.Min(
                            capacity, System.Math.Max(0, maximum));
                    int requiredTotal = System.Math.Min(
                        capacity, requirement.RequiredTotal);
                    var explanation = new RoleRecommendationExplanation
                    {
                        RoleId = roleId,
                        Recommended = recommended,
                        RequiredTotal = requiredTotal,
                        CoveredTotal = coveredTotals.TryGetValue(
                            roleId, out int covered) ? covered : 0,
                        ConfiguredMaximum = role.MaxHoldersAt(
                            facts.Colony.Pawns.Count),
                        RequiredSkills = RequiredSkillNames(facts, role),
                        SignalBucket = signal,
                        SignalSkillDefName = signalSkill,
                    };
                    if (recommended)
                        IncludedDecision(
                            facts,
                            drafts[pawnIndex],
                            pawnIndex,
                            role,
                            signal,
                            formulas,
                            explanation);
                    else
                        RemovedDecision(
                            facts,
                            drafts[pawnIndex],
                            pawnIndex,
                            role,
                            signal,
                            requiredTotal,
                            formulas,
                            explanation);
                    explanations.Add(roleId, explanation);
                }
            }
            return result;
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
            SignalBucket signal,
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
                : signal >= formulas.SurplusMinimumSignal
                    ? RecommendationDecision.SignalQualified
                    : RecommendationDecision.Recommended;
        }

        private static void RemovedDecision(
            EngineContext facts,
            PawnDraft draft,
            int pawnIndex,
            RoleView role,
            SignalBucket signal,
            int requiredTotal,
            RecommendationFormulaEngine formulas,
            RoleRecommendationExplanation explanation)
        {
            PawnView pawn = facts.Colony.Pawns[pawnIndex];
            if (role.HolderMode == RoleHolderMode.Never)
                explanation.Decision = RecommendationDecision.HolderModeNever;
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
            else if (signal < formulas.CandidateMinimumSignal)
                explanation.Decision = signal == SignalBucket.Awful
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
                else if (signal < formulas.SurplusMinimumSignal)
                    explanation.Decision =
                        RecommendationDecision.SignalBelowThreshold;
                else
                    explanation.Decision = RecommendationDecision.NotSelected;
            }
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
