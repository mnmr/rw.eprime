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
                model.AddSection().Fact("WR_RecTipRecommendation".Translate(),
                    state == Dialog_ChangesPreview.ChipState.Removed
                        ? "WR_ReasonRemoved".Translate()
                        : "WR_AlreadyAssigned".Translate());
                return new StructuredTip(
                    $"recommendation:{pawn.thingIDNumber}:{role.id}", model);
            }

            TipSection facts = model.AddSection();
            if (!role.autoAssign && !role.blocker)
            {
                string need = explanation.RequiredHolders == 1
                    ? "WR_RecTipNeedOne".Translate(explanation.RecommendedHolders)
                    : explanation.RequiredHolders > 1
                        ? "WR_RecTipNeedMany".Translate(
                            explanation.RequiredHolders, explanation.RecommendedHolders)
                        : "WR_RecTipNeedNone".Translate(explanation.RecommendedHolders);
                facts.Fact("WR_RecTipColonyNeed".Translate(), need);
                if (explanation.ConfiguredMaximum < RoleHolderRange.Uncapped)
                    facts.Fact("WR_RecTipConfiguredMax".Translate(),
                        "WR_RecTipMaximum".Translate(explanation.ConfiguredMaximum));
            }

            if (explanation.RequiredSkills.Count > 0)
                facts.Fact("WR_RecTipSkills".Translate(),
                    explanation.RequiredSkills.Select(SkillLabel).ToCommaList());

            if (explanation.SignalSkillDefName != null)
                facts.Fact("WR_RecTipSignalVerdict".Translate(),
                    SignalVerdict(skillBuckets, explanation),
                    SkillSignalPresentation.VerdictColor(explanation.SignalBucket));

            facts.Fact("WR_RecTipRecommendation".Translate(),
                RecommendationDecisionText(store, explanation));
            return new StructuredTip(
                $"recommendation:{pawn.thingIDNumber}:{role.id}", model);
        }

        private static string SignalVerdict(
            SkillBucketSnapshot skillBuckets,
            RoleRecommendationExplanation explanation)
        {
            string verdict = SkillSignalPresentation.BucketLabel(explanation.SignalBucket);
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
            RoleRecommendationExplanation explanation)
        {
            switch (explanation.Decision)
            {
                case RecommendationDecision.AutoAssigned:
                    return "WR_RecDecisionAuto".Translate();
                case RecommendationDecision.SignalQualified:
                    return "WR_RecDecisionSignals".Translate();
                case RecommendationDecision.CoverageDrafted:
                    return CoverageDraftDecisionText(explanation);
                case RecommendationDecision.Training:
                {
                    Role target = store?.RoleById(explanation.RelatedRoleId);
                    return "WR_RecDecisionTraining".Translate(target?.label ?? "?");
                }
                case RecommendationDecision.Hunter:
                    return "WR_RecDecisionHunter".Translate();
                case RecommendationDecision.FireSafety:
                    return "WR_RecDecisionFire".Translate();
                case RecommendationDecision.Retained:
                    return "WR_RecDecisionRetained".Translate();
                case RecommendationDecision.ProtectedAssignment:
                    return "WR_RecDecisionProtected".Translate();
                case RecommendationDecision.HolderModeNever:
                    return "WR_RecDecisionNever".Translate();
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
                case RecommendationDecision.OutsideTrainingBand:
                    return "WR_RecDecisionBand".Translate();
                case RecommendationDecision.CoveredByRecommendedRole:
                {
                    Role covering = store?.RoleById(explanation.RelatedRoleId);
                    return "WR_RecDecisionCovered".Translate(covering?.label ?? "?");
                }
                case RecommendationDecision.RequiredCoverageFilled:
                    return CoverageFilledDecisionText(explanation);
                case RecommendationDecision.ConfiguredMaximumReached:
                    return "WR_RecDecisionMaximum".Translate(explanation.ConfiguredMaximum);
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

        private static string CoverageDraftDecisionText(
            RoleRecommendationExplanation explanation)
        {
            if (explanation.CandidateRank <= 0)
                return "WR_RecDecisionCoverageDraft".Translate();
            if (explanation.CandidateSkillDefName.NullOrEmpty())
                return "WR_RecDecisionCoverageDraftRankedNoSkill".Translate(
                    explanation.CandidateRank, explanation.CandidatePoolSize);
            return "WR_RecDecisionCoverageDraftRanked".Translate(
                explanation.CandidateRank,
                explanation.CandidatePoolSize,
                SkillLabel(explanation.CandidateSkillDefName),
                explanation.CandidateSkillLevel);
        }

        private static string CoverageFilledDecisionText(
            RoleRecommendationExplanation explanation)
        {
            if (explanation.CandidateRank <= 0)
                return "WR_RecDecisionCoverageFilled".Translate();
            string key = explanation.CoverageOpenSlots > 0
                ? "WR_RecDecisionCoverageFilledRanked"
                : "WR_RecDecisionCoverageAlreadyFullRanked";
            if (explanation.CandidateSkillDefName.NullOrEmpty())
                return (key + "NoSkill").Translate(
                    explanation.CandidateRank, explanation.CandidatePoolSize);
            return key.Translate(
                explanation.CandidateRank,
                explanation.CandidatePoolSize,
                SkillLabel(explanation.CandidateSkillDefName),
                explanation.CandidateSkillLevel);
        }
    }
}
