using System.Linq;
using RimWorld;
using Verse;
using WorkRoles.Core;
using WorkRoles.Core.Recs;
using WorkRoles.Core.Signals;
using WorkRoles.Signals;

namespace WorkRoles.UI
{
    internal static class RecommendationPresentation
    {
        internal static StructuredTip CreateTooltip(
            RoleStore store,
            Pawn pawn,
            Role role,
            Dialog_ChangesPreview.ChipState state,
            RoleRecommendationExplanation explanation,
            SkillBucketSnapshot skillBuckets)
        {
            var model = new TipModel { Title = role.label };
            if (explanation == null)
            {
                model.AddSection().Fact("WR_RecTipSelection".Translate(),
                    state == Dialog_ChangesPreview.ChipState.Removed
                        ? "WR_ReasonRemoved".Translate()
                        : "WR_AlreadyAssigned".Translate());
                return new StructuredTip(
                    $"recommendation:{pawn.thingIDNumber}:{role.id}", model);
            }

            TipSection facts = model.AddSection();
            if (explanation.HolderScaleApplies)
                facts.Fact("WR_RecTipColonyNeed".Translate(),
                    ColonyNeedText(explanation));

            if (explanation.RequiredSkills.Count > 0)
                facts.Fact("WR_RecTipSkills".Translate(),
                    explanation.RequiredSkills.Select(SkillLabel).ToCommaList());

            if (explanation.SignalSkillDefName != null)
                facts.Fact("WR_RecTipSignalVerdict".Translate(),
                    SignalVerdict(skillBuckets, explanation),
                    SkillSignalPresentation.VerdictColor(explanation.SignalBucket));

            facts.Fact("WR_RecTipSelection".Translate(),
                RecommendationDecisionText(store, role, explanation));
            return new StructuredTip(
                $"recommendation:{pawn.thingIDNumber}:{role.id}", model);
        }

        private static string SignalVerdict(
            SkillBucketSnapshot skillBuckets,
            RoleRecommendationExplanation explanation)
        {
            string verdict = SkillSignalPresentation.BucketLabel(
                explanation.SignalBucket);
            SkillBucketSignal bucket = (skillBuckets ?? SkillBucketSnapshot.Empty)
                .ForSkill(explanation.SignalSkillDefName);
            if (bucket == null) return verdict;

            var sources = bucket.Contributions
                .Where(contribution => contribution.IsClassified)
                .Select(contribution => contribution.Signal.Ui.Label.NullOrEmpty()
                    ? contribution.Signal.Source.DefName
                    : contribution.Signal.Ui.Label)
                .Where(label => !label.NullOrEmpty())
                .Select(label => label.CapitalizeFirst())
                .Distinct()
                .ToList();
            return sources.Count == 0
                ? verdict
                : verdict + " (" + string.Join(", ", sources) + ")";
        }

        private static string SkillLabel(string defName)
        {
            SkillDef skill = DefDatabase<SkillDef>.GetNamedSilentFail(defName);
            return skill?.skillLabel.CapitalizeFirst() ?? defName;
        }

        private static string RecommendationDecisionText(
            RoleStore store,
            Role role,
            RoleRecommendationExplanation explanation)
        {
            string selection = SelectionDecisionText(
                store, role, explanation);
            if (selection != null) return selection;

            switch (explanation.Decision)
            {
                case RecommendationDecision.AutoAssigned:
                    return "WR_RecDecisionAuto".Translate();
                case RecommendationDecision.SignalQualified:
                    return "WR_RecDecisionSignals".Translate();
                case RecommendationDecision.CoverageDrafted:
                    return "WR_RecDecisionCoverageDraft".Translate();
                case RecommendationDecision.Training:
                {
                    Role target = store?.RoleById(explanation.RelatedRoleId);
                    return target == null
                        ? "WR_RecDecisionTrainingUnknown".Translate().ToString()
                        : "WR_RecDecisionTraining".Translate(
                            target.label).ToString();
                }
                case RecommendationDecision.Hunter:
                    return "WR_RecDecisionHunter".Translate();
                case RecommendationDecision.FireSafety:
                    return "WR_RecDecisionFire".Translate();
                case RecommendationDecision.Retained:
                    return "WR_RecDecisionRetained".Translate();
                case RecommendationDecision.ProtectedAssignment:
                    return "WR_RecDecisionProtected".Translate();
                case RecommendationDecision.ScaleNever:
                    return "WR_RecDecisionNever".Translate();
                case RecommendationDecision.ControlledByTrainingTarget:
                {
                    Role controller = store?.RoleById(explanation.RelatedRoleId);
                    return controller == null
                        ? "WR_RecDecisionNever".Translate().ToString()
                        : "WR_RecDecisionControlledByTraining".Translate(
                            controller.label).ToString();
                }
                case RecommendationDecision.RoleDisabled:
                    return "WR_RecDecisionDisabled".Translate();
                case RecommendationDecision.RoleUnavailable:
                    return "WR_RecDecisionUnavailable".Translate();
                case RecommendationDecision.RoleExcluded:
                    return "WR_RecDecisionExcluded".Translate();
                case RecommendationDecision.PawnIncapable:
                    return "WR_RecDecisionIncapable".Translate();
                case RecommendationDecision.HunterRequirementsNotMet:
                    return "WR_RecDecisionHunterRequirements".Translate();
                case RecommendationDecision.AwfulSignal:
                    return "WR_RecDecisionAwful".Translate();
                case RecommendationDecision.CoveredByRecommendedRole:
                {
                    Role covering = store?.RoleById(explanation.RelatedRoleId);
                    return covering == null
                        ? "WR_RecDecisionCoveredUnknown".Translate().ToString()
                        : "WR_RecDecisionCovered".Translate(
                            covering.label).ToString();
                }
                case RecommendationDecision.RequiredCoverageFilled:
                    return "WR_RecDecisionCoverageFilled".Translate();
                case RecommendationDecision.SignalBelowThreshold:
                    return "WR_RecDecisionWeakSignal".Translate();
                case RecommendationDecision.NotSelected:
                    return "WR_RecDecisionNotSelected".Translate();
                default:
                    return explanation.Recommended
                        ? "WR_RecDecisionRecommended".Translate()
                        : "WR_RecDecisionNotSelected".Translate();
            }
        }

        private static string ColonyNeedText(
            RoleRecommendationExplanation explanation)
        {
            string result = explanation.RequiredTotal == 1
                ? "WR_RecTipAssignmentsOne".Translate().ToString()
                : "WR_RecTipAssignmentsMany"
                    .Translate(explanation.RequiredTotal).ToString();
            if (explanation.TrainingWaivers == 1)
                result = "WR_RecTipWithWaiverOne".Translate(result).ToString();
            else if (explanation.TrainingWaivers > 1)
                result = "WR_RecTipWithWaiversMany"
                    .Translate(result, explanation.TrainingWaivers).ToString();
            if (explanation.ConfiguredMaximum < RoleHolderRange.Uncapped)
                result = "WR_RecTipWithCap"
                    .Translate(result, explanation.ConfiguredMaximum).ToString();
            return result;
        }

        private static string SelectionDecisionText(
            RoleStore store,
            Role role,
            RoleRecommendationExplanation explanation)
        {
            switch (explanation.SelectionStage)
            {
                case RecommendationSelectionStage.Required:
                    return HasSelectionSlot(explanation)
                        ? "WR_RecDecisionDirectSlot".Translate(
                            explanation.SelectionSlot,
                            explanation.SelectionSlotCount,
                            role.label).ToString()
                        : null;
                case RecommendationSelectionStage.TrainingWaiver:
                {
                    Role target = store?.RoleById(explanation.RelatedRoleId);
                    if (!HasSelectionSlot(explanation)) return null;
                    if (target == null)
                        return "WR_RecDecisionTrainingSlotUnknown".Translate(
                            explanation.SelectionSlot,
                            explanation.SelectionSlotCount,
                            role.label).ToString();
                    string targetLabel = target.label;
                    if (explanation.TrainingSkills.Count == 0)
                        return "WR_RecDecisionTrainingSlotSimple".Translate(
                            explanation.SelectionSlot,
                            explanation.SelectionSlotCount,
                            targetLabel,
                            role.label).ToString();
                    string skills = explanation.TrainingSkills
                        .Select(skill => "WR_RecTrainingSkill".Translate(
                            SkillLabel(skill.SkillDefName),
                            skill.PawnLevel).ToString())
                        .ToCommaList(useAnd: true);
                    return "WR_RecDecisionTrainingSlot".Translate(
                        explanation.SelectionSlot,
                        explanation.SelectionSlotCount,
                        targetLabel,
                        role.label,
                        skills,
                        explanation.TrainingSkills[0].TargetMinimum).ToString();
                }
                case RecommendationSelectionStage.DirectFallback:
                    return HasSelectionSlot(explanation)
                        ? "WR_RecDecisionDirectFallbackSlot".Translate(
                            explanation.SelectionSlot,
                            explanation.SelectionSlotCount,
                            role.label).ToString()
                        : null;
                case RecommendationSelectionStage.CoverageRepair:
                    return HasSelectionSlot(explanation)
                        ? "WR_RecDecisionCoverageRepairSlot".Translate(
                            explanation.SelectionSlot,
                            explanation.SelectionSlotCount,
                            role.label).ToString()
                        : null;
                case RecommendationSelectionStage.Surplus:
                    return explanation.SurplusQualifiedBySignal
                        ? "WR_RecDecisionSignalSurplus".Translate().ToString()
                        : "WR_RecDecisionAptitudeSurplus".Translate().ToString();
                default:
                    return null;
            }
        }

        private static bool HasSelectionSlot(
            RoleRecommendationExplanation explanation) =>
            explanation.SelectionSlot > 0
            && explanation.SelectionSlot <= explanation.SelectionSlotCount;

    }
}
