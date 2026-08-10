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

            if (explanation.Recommended)
            {
                switch (explanation.SpecialPickReason)
                {
                    case SpecialPickReason.AutoAssigned:
                        return "WR_RecDecisionAuto".Translate();
                    case SpecialPickReason.Hunter:
                        return "WR_RecDecisionHunter".Translate();
                    case SpecialPickReason.FireSafety:
                        return "WR_RecDecisionFire".Translate();
                    case SpecialPickReason.Retained:
                        return "WR_RecDecisionRetained".Translate();
                    case SpecialPickReason.Protected:
                        return "WR_RecDecisionProtected".Translate();
                    default:
                        return "WR_RecDecisionRecommended".Translate();
                }
            }

            switch (explanation.RejectReason)
            {
                case PickRejectReason.ControlledByTarget:
                {
                    Role controller = store?.RoleById(explanation.RelatedRoleId);
                    return controller == null
                        ? "WR_RecDecisionNever".Translate().ToString()
                        : "WR_RecDecisionControlledByTraining".Translate(
                            controller.label).ToString();
                }
                case PickRejectReason.ScaleNever:
                    return "WR_RecDecisionNever".Translate();
                case PickRejectReason.Incapable:
                    return "WR_RecDecisionIncapable".Translate();
                case PickRejectReason.HunterRequirementsNotMet:
                    return "WR_RecDecisionHunterRequirements".Translate();
                case PickRejectReason.AwfulSignal:
                    return "WR_RecDecisionAwful".Translate();
                case PickRejectReason.WeakSignal:
                    return "WR_RecDecisionWeakSignal".Translate();
                case PickRejectReason.OutOfBand:
                    return "WR_RecDecisionOutOfBand".Translate();
                case PickRejectReason.Covered:
                {
                    Role covering = store?.RoleById(explanation.RelatedRoleId);
                    return covering == null
                        ? "WR_RecDecisionCoveredUnknown".Translate().ToString()
                        : "WR_RecDecisionCovered".Translate(
                            covering.label).ToString();
                }
                case PickRejectReason.RequiredCoverageFilled:
                    return "WR_RecDecisionCoverageFilled".Translate();
                case PickRejectReason.Outqualified:
                    return "WR_RecDecisionOutqualified".Translate();
                default:
                    return "WR_RecDecisionNotSelected".Translate();
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
