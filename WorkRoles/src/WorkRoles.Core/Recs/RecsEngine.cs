using System.Collections.Generic;

namespace WorkRoles.Core.Recs
{
    /// One pawn's output: ordered assignment list plus per-role reasons for the UI.
    public sealed class PawnResult
    {
        public List<AssignmentView> Assignments = new List<AssignmentView>();
        public Dictionary<int, Reason> Reasons = new Dictionary<int, Reason>();
        public Dictionary<int, RoleRecommendationExplanation> Explanations =
            new Dictionary<int, RoleRecommendationExplanation>();
        public int HunterTier = -1;
        public bool FireGranted;
    }

    public static class RecsEngine
    {
        /// Execution order is part of the contract: candidates before gates,
        /// colony passes before ordering, protected re-entry before final
        /// placement anchoring.
        public static readonly IReadOnlyList<RecRule> Pipeline = new RecRule[]
        {
            new ExclusionsRule(),
            new AutoRolesRule(),
            new SignalCandidatesRule(),
            new BandGatingRule(),
            new CoverageScalingRule(new UnitScaling()),
            new HunterRule(),
            new TrainingWaiverRule(new AdditiveTrainingDemandPolicy()),
            new BestInColonyDraftRule(),
            new FireSafetyRule(),
            new RetentionRule(),
            new HolderLimitRule(new AdditiveTrainingDemandPolicy()),
            new RedundancySuppressionRule(),
            new OrderingRule(),
            new ProtectedReentryRule(),
            new AnchorPreservationRule(),
        };

        public static List<PawnResult> Run(ColonyView colony) => Run(colony, Pipeline);

        /// Runs the given rules in order over one shared context.
        public static List<PawnResult> Run(ColonyView colony, IEnumerable<RecRule> rules)
        {
            var context = new EngineContext(colony);
            foreach (var rule in rules)
            {
                if (!rule.Relevant(context)) continue;
                if (rule.Kind == RuleKind.Colony)
                    rule.Apply(context);
                else
                    for (int i = 0; i < colony.Pawns.Count; i++)
                        rule.Apply(context, i);
            }
            for (int i = 0; i < context.Results.Count; i++)
            {
                context.Results[i].HunterTier = context.HunterTiers[i];
                context.Results[i].FireGranted = context.FireGranted[i];
            }
            RecommendationExplainer.Populate(context);
            return context.Results;
        }
    }
}
