using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

public class RecommendationTuningScenarioTests
{
    [Test]
    public async Task CoverageRepairPublishesItsStageAndSlots()
    {
        // Coverage repair is a defensive post-draft transition. A final-plan
        // fixture cannot reliably request that transient state without
        // duplicating the overlap resolver, so this tests the published facts
        // at the transition that owns them.
        // colonyMin 0 with coverage keeps the whole total waivable (direct
        // minimum 0), so both slots (75% of 2 pawns, half-up = 2) stay open
        // for coverage repair.
        RoleView role = RecsTestBed.Role(1, "Crafting");
        RecsTestBed.Require(role, 0, 75);
        PawnView first = RecsTestBed.Pawn();
        first.SkillLevels["Crafting"] = 8;
        first.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        PawnView second = RecsTestBed.Pawn();
        second.SkillLevels["Crafting"] = 7;
        second.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        var formulas = new RecommendationFormulaEngine(RecommendationsTuningOptions.Default);
        RolePlan plan = RolePlan.Build(new EngineContext(RecsTestBed.Colony([role], first, second)), role, formulas);

        plan.SelectForCoverage(0, 1);
        plan.SelectForCoverage(1, 2);

        await Assert.That(plan.SelectionStageAt(0)).IsEqualTo(RecommendationSelectionStage.CoverageRepair);
        await Assert.That(plan.SelectionSlotAt(0)).IsEqualTo(1);
        await Assert.That(plan.SelectionStageAt(1)).IsEqualTo(RecommendationSelectionStage.CoverageRepair);
        await Assert.That(plan.SelectionSlotAt(1)).IsEqualTo(2);
        await Assert.That(plan.SelectionSlotCount).IsEqualTo(2);
    }

    [Test]
    public async Task ChampionMultipliersChangeThePublishedMinimumHolder()
    {
        RoleView crafter = RecsTestBed.Role(1, "Crafting");
        RecsTestBed.Require(crafter, 1);

        PawnView greatTen = RecsTestBed.Pawn();
        greatTen.SkillLevels["Crafting"] = 10;
        greatTen.SignalBuckets["Crafting"] = SignalBucket.Great;
        PawnView neutralTwenty = RecsTestBed.Pawn();
        neutralTwenty.SkillLevels["Crafting"] = 20;
        neutralTwenty.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        neutralTwenty.Existing.Add(new AssignmentView { RoleId = crafter.Id, Enabled = true });
        ColonyView colony = RecsTestBed.Colony([crafter], greatTen, neutralTwenty);

        RecommendationsTuningOptions noSurplus = RecommendationsTuningOptions.Default.With(RecommendationTuningOption.SurplusMinimumSignal, (int)SignalBucket.Exceptional);
        RecommendationPlan defaults = RecommendationPlan.Build(colony, noSurplus);
        RecommendationPlan reducedGreatMultiplier = RecommendationPlan.Build(colony, noSurplus.With(RecommendationTuningOption.ChampionGreatMultiplierQuarterUnits, 1));
        HashSet<int> defaultGreatTenAssignments = [.. Enumerable.Range(0, defaults.RoleCountAt(0)).Select(index => defaults.RoleAt(0, index))];
        HashSet<int> defaultNeutralTwentyAssignments = [.. Enumerable.Range(0, defaults.RoleCountAt(1)).Select(index => defaults.RoleAt(1, index))];
        HashSet<int> reducedGreatTenAssignments = [.. Enumerable.Range(0, reducedGreatMultiplier.RoleCountAt(0)).Select(index => reducedGreatMultiplier.RoleAt(0, index))];
        HashSet<int> reducedNeutralTwentyAssignments = [.. Enumerable.Range(0, reducedGreatMultiplier.RoleCountAt(1)).Select(index => reducedGreatMultiplier.RoleAt(1, index))];

        await Assert.That(defaultGreatTenAssignments.SetEquals([1])).IsTrue();
        await Assert.That(defaultNeutralTwentyAssignments).IsEmpty();
        await Assert.That(reducedGreatTenAssignments).IsEmpty();
        await Assert.That(reducedNeutralTwentyAssignments.SetEquals([1])).IsTrue();
        await Assert.That(defaults.TryGetExplanation(0, crafter.Id, out RoleRecommendationExplanation selected)).IsTrue();
        await Assert.That(selected.Recommended).IsTrue();
        await Assert.That(selected.SelectionStage).IsEqualTo(RecommendationSelectionStage.Required);
        await Assert.That(defaults.TryGetExplanation(1, crafter.Id, out RoleRecommendationExplanation removed)).IsTrue();
        await Assert.That(removed.Recommended).IsFalse();
        await Assert.That(removed.RejectReason).IsEqualTo(PickRejectReason.RequiredCoverageFilled);
    }

    [Test]
    public async Task OrderingSignalPointsChangeThePublishedRoleOrder()
    {
        RoleView doctor = RecsTestBed.Role(1, "Doctor");
        RoleView cook = RecsTestBed.Role(2, "Cooking");

        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Medicine"] = 10;
        pawn.SignalBuckets["Medicine"] = SignalBucket.Strong;
        pawn.SkillLevels["Cooking"] = 10;
        pawn.SignalBuckets["Cooking"] = SignalBucket.Great;
        ColonyView colony = RecsTestBed.Colony([doctor, cook], pawn);

        RecommendationPlan defaults = RecommendationPlan.Build(colony, RecommendationsTuningOptions.Default);
        RecommendationPlan reducedGreatPoints = RecommendationPlan.Build(colony, RecommendationsTuningOptions.Default.With(RecommendationTuningOption.OrderingGreatSignalPoints, 0));
        string actualDefaultOrder = string.Join(",", Enumerable.Range(0, defaults.RoleCountAt(0)).Select(index => defaults.RoleAt(0, index)));
        string actualReducedOrder = string.Join(",", Enumerable.Range(0, reducedGreatPoints.RoleCountAt(0)).Select(index => reducedGreatPoints.RoleAt(0, index)));

        await Assert.That(actualDefaultOrder).IsEqualTo("2,1");
        await Assert.That(actualReducedOrder).IsEqualTo("1,2");
    }

    [Test]
    public async Task SemanticallyEqualEditPreservesTheOptionsSnapshot()
    {
        RecommendationsTuningOptions defaults = RecommendationsTuningOptions.Default;

        RecommendationsTuningOptions normalized = defaults.With(RecommendationTuningOption.ChampionSkillDivisor, 2);

        await Assert.That(normalized).IsSameReferenceAs(defaults);
    }

    [Test]
    public async Task FormulaDescriptorCatalogContainsEveryOptionExactlyOnce()
    {
        IReadOnlyList<RecommendationTuningDescriptor> descriptors = RecommendationsTuningOptions.Descriptors;
        RecommendationTuningOption[] options = Enum.GetValues(typeof(RecommendationTuningOption)).Cast<RecommendationTuningOption>().ToArray();

        await Assert.That(descriptors.Select(item => item.Option)).IsEquivalentTo(options);
        await Assert.That(descriptors.Select(item => item.StableKey).Distinct().Count()).IsEqualTo(descriptors.Count);
    }

    [Test]
    public async Task FormulaOptionsPublishExactPersistenceAndUiMetadata()
    {
        IReadOnlyList<RecommendationTuningDescriptor> descriptors = RecommendationsTuningOptions.Descriptors;
        string actualMetadata = string.Join("\n", descriptors.OrderBy(item => item.Option).Select(item => $"{item.Option}={item.StableKey}={item.DefaultValue}"));
        const string expectedMetadata = """
CandidateMinimumSignal=candidateMinimumSignal=1
ChampionSkillDivisor=championSkillDivisor=2
ChampionMultiSkillMinimumCount=championMultiSkillMinimumCount=2
ChampionAwfulMultiplierQuarterUnits=championAwfulMultiplier=0
ChampionPoorMultiplierQuarterUnits=championPoorMultiplier=2
ChampionNeutralMultiplierQuarterUnits=championNeutralMultiplier=4
ChampionStrongMultiplierQuarterUnits=championStrongMultiplier=6
ChampionGreatMultiplierQuarterUnits=championGreatMultiplier=8
ChampionExceptionalMultiplierQuarterUnits=championExceptionalMultiplier=10
ChampionAwfulTieBreakPoints=championAwfulTieBreakPoints=-5
ChampionPoorTieBreakPoints=championPoorTieBreakPoints=-3
ChampionNeutralTieBreakPoints=championNeutralTieBreakPoints=0
ChampionStrongTieBreakPoints=championStrongTieBreakPoints=1
ChampionGreatTieBreakPoints=championGreatTieBreakPoints=3
ChampionExceptionalTieBreakPoints=championExceptionalTieBreakPoints=5
RankedCandidatePrioritySignal=rankedCandidatePrioritySignal=3
SurplusMinimumSignal=surplusMinimumSignal=3
PathMinimumSignal=pathMinimumSignal=1
OptionalTargetMinimumSkillCount=optionalTargetMinimumSkillCount=2
OptionalTargetMinimumSignal=optionalTargetMinimumSignal=2
OptionalTargetStrongLevel=optionalTargetStrongLevel=10
OptionalTargetStrongPromotedSignal=optionalTargetStrongPromotedSignal=3
OptionalTargetGreatLevel=optionalTargetGreatLevel=15
OptionalTargetGreatPromotedSignal=optionalTargetGreatPromotedSignal=4
OptionalTargetMinimumPoints=optionalTargetMinimumPoints=2
LeadMinimumConnectedTargets=leadMinimumConnectedTargets=3
LeadMinimumSignal=leadMinimumSignal=3
OrderingSkillDivisor=orderingSkillDivisor=5
OrderingAwfulSignalPoints=orderingAwfulSignalPoints=-5
OrderingPoorSignalPoints=orderingPoorSignalPoints=-5
OrderingNeutralSignalPoints=orderingNeutralSignalPoints=-3
OrderingStrongSignalPoints=orderingStrongSignalPoints=1
OrderingGreatSignalPoints=orderingGreatSignalPoints=3
OrderingExceptionalSignalPoints=orderingExceptionalSignalPoints=5
FirstMinimumPickBonus=firstMinimumPickBonus=10
SecondMinimumPickBonus=secondMinimumPickBonus=5
ThirdMinimumPickBonus=thirdMinimumPickBonus=2
LaterMinimumPickBonus=laterMinimumPickBonus=1
OrderingImportantCategoryPoints=orderingImportantCategoryPoints=4
OrderingOptionalCategoryPoints=orderingOptionalCategoryPoints=-4
OrderingPartTimePoints=orderingPartTimePoints=2
OrderingOpportunisticPoints=orderingOpportunisticPoints=-2
HunterFirstTierMaximum=hunterFirstTierMaximum=4
HunterSecondTierMaximum=hunterSecondTierMaximum=8
HunterThirdTierMaximum=hunterThirdTierMaximum=12
HunterFourthTierMaximum=hunterFourthTierMaximum=16
RepeatChampionOverlapPenalty=repeatChampionOverlapPenalty=60
RepeatChampionDistinctPenalty=repeatChampionDistinctPenalty=40
RepeatChampionOccasionalPenalty=repeatChampionOccasionalPenalty=20
""";

        await Assert.That(actualMetadata).IsEqualTo(expectedMetadata);
        await Assert.That(descriptors.All(item => RecommendationsTuningOptions.Default.Get(item.Option) == item.DefaultValue)).IsTrue();
        await Assert.That(descriptors.All(item => !string.IsNullOrEmpty(item.SectionLabelKey) && !string.IsNullOrEmpty(item.LabelKey) && !string.IsNullOrEmpty(item.DescriptionKey))).IsTrue();
    }

    [Test]
    public async Task OptionalTargetPointsChangeThePublishedTrainingRole()
    {
        RoleView target = RecsTestBed.Role(1, "Crafting", "TargetWork");
        ApplyTwoSkillProfile(target, "TargetWork");
        RoleView trainee = RecsTestBed.Role(2, "Crafting", "TrainingWork");
        ApplyTwoSkillProfile(trainee, "TrainingWork");
        PathView path = RecsTestBed.Path(10, (trainee.Id, 0, 15), (target.Id, 15, 21));

        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = 10;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        pawn.SkillLevels["Intellectual"] = 10;
        pawn.SignalBuckets["Intellectual"] = SignalBucket.Neutral;
        ColonyView colony = RecsTestBed.Colony([target, trainee], pawn);
        colony.Paths.Add(path);

        RecommendationPlan defaults = RecommendationPlan.Build(colony, RecommendationsTuningOptions.Default);
        RecommendationPlan higherMinimum = RecommendationPlan.Build(colony, RecommendationsTuningOptions.Default.With(RecommendationTuningOption.OptionalTargetMinimumPoints, 3));
        RecommendationPlan higherSkillCountThreshold = RecommendationPlan.Build(colony, RecommendationsTuningOptions.Default.With(RecommendationTuningOption.OptionalTargetMinimumSkillCount, 3));
        HashSet<int> defaultAssignments = [.. Enumerable.Range(0, defaults.RoleCountAt(0)).Select(index => defaults.RoleAt(0, index))];
        HashSet<int> higherMinimumAssignments = [.. Enumerable.Range(0, higherMinimum.RoleCountAt(0)).Select(index => higherMinimum.RoleAt(0, index))];
        HashSet<int> higherSkillCountAssignments = [.. Enumerable.Range(0, higherSkillCountThreshold.RoleCountAt(0)).Select(index => higherSkillCountThreshold.RoleAt(0, index))];

        await Assert.That(defaultAssignments.SetEquals([2])).IsTrue();
        await Assert.That(higherMinimumAssignments).IsEmpty();
        await Assert.That(higherSkillCountAssignments).IsEmpty();
        await Assert.That(defaults.TryGetExplanation(0, trainee.Id, out RoleRecommendationExplanation training)).IsTrue();
        await Assert.That(training.RelatedRoleId).IsEqualTo(1);
        await Assert.That(training.SelectionStage).IsEqualTo(RecommendationSelectionStage.Surplus);
        await Assert.That(training.SurplusQualifiedBySignal).IsFalse();
    }

    [Test]
    public async Task BulkLoadNormalizesRelatedValuesAfterReadingTheWholeSnapshot()
    {
        var persisted = new Dictionary<RecommendationTuningOption, int>
        {
            [RecommendationTuningOption.OptionalTargetStrongLevel] = 18,
            [RecommendationTuningOption.OptionalTargetGreatLevel] = 20,
            [RecommendationTuningOption.HunterFirstTierMaximum] = 12,
            [RecommendationTuningOption.HunterSecondTierMaximum] = 16,
            [RecommendationTuningOption.HunterThirdTierMaximum] = 19,
        };

        RecommendationsTuningOptions loaded = RecommendationsTuningOptions.FromValues(persisted);

        await Assert.That(loaded.Get(RecommendationTuningOption.OptionalTargetStrongLevel)).IsEqualTo(18);
        await Assert.That(loaded.Get(RecommendationTuningOption.OptionalTargetGreatLevel)).IsEqualTo(20);
        await Assert.That(loaded.Get(RecommendationTuningOption.HunterFirstTierMaximum)).IsEqualTo(12);
        await Assert.That(loaded.Get(RecommendationTuningOption.HunterSecondTierMaximum)).IsEqualTo(16);
        await Assert.That(loaded.Get(RecommendationTuningOption.HunterThirdTierMaximum)).IsEqualTo(19);
    }

    [Test]
    public async Task ZeroMinimumBonusStillPublishesCoverageDraftExplanation()
    {
        RoleView role = RecsTestBed.Role(1, "Crafting");
        RecsTestBed.Require(role, 1);
        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = 10;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        ColonyView colony = RecsTestBed.Colony([role], pawn);
        RecommendationsTuningOptions options = RecommendationsTuningOptions.Default.With(RecommendationTuningOption.FirstMinimumPickBonus, 0);

        RecommendationPlan plan = RecommendationPlan.Build(colony, options);
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];

        await Assert.That(assignments.SetEquals([1])).IsTrue();
        await Assert.That(plan.TryGetExplanation(0, role.Id, out RoleRecommendationExplanation explanation)).IsTrue();
        await Assert.That(explanation.SelectionStage).IsEqualTo(RecommendationSelectionStage.Required);
        await Assert.That(explanation.CandidateRank).IsEqualTo(1);
        await Assert.That(explanation.CandidateCount).IsEqualTo(1);
        await Assert.That(explanation.StageRank).IsEqualTo(1);
    }

    [Test]
    public async Task RaisedCandidateFloorDoesNotMislabelNeutralAsAwful()
    {
        RoleView role = RecsTestBed.Role(1, "Crafting");
        RecsTestBed.Require(role, 1);
        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = 10;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        pawn.Existing.Add(new AssignmentView { RoleId = role.Id, Enabled = true });
        ColonyView colony = RecsTestBed.Colony([role], pawn);
        RecommendationsTuningOptions options = RecommendationsTuningOptions.Default.With(RecommendationTuningOption.CandidateMinimumSignal, (int)SignalBucket.Strong);

        RecommendationPlan plan = RecommendationPlan.Build(colony, options);
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];

        await Assert.That(assignments).IsEmpty();
        await Assert.That(plan.TryGetExplanation(0, role.Id, out RoleRecommendationExplanation explanation)).IsTrue();
        await Assert.That(explanation.RejectReason).IsEqualTo(PickRejectReason.WeakSignal);
    }

    [Test]
    public async Task AwfulChampionMultiplierIsAnEditableFormulaInput()
    {
        RoleView role = RecsTestBed.Role(1, "Crafting");
        RecsTestBed.Require(role, 1);
        PawnView first = RecsTestBed.Pawn();
        first.SkillLevels["Crafting"] = 1;
        first.SignalBuckets["Crafting"] = SignalBucket.Awful;
        PawnView second = RecsTestBed.Pawn();
        second.SkillLevels["Crafting"] = 20;
        second.SignalBuckets["Crafting"] = SignalBucket.Awful;
        second.Existing.Add(new AssignmentView { RoleId = role.Id, Enabled = true });
        ColonyView colony = RecsTestBed.Colony([role], first, second);
        RecommendationsTuningOptions admitsAwful = RecommendationsTuningOptions.Default.With(RecommendationTuningOption.CandidateMinimumSignal, (int)SignalBucket.Awful);

        RecommendationPlan zeroMultiplier = RecommendationPlan.Build(colony, admitsAwful);
        RecommendationPlan skillMultiplier = RecommendationPlan.Build(colony, admitsAwful.With(RecommendationTuningOption.ChampionAwfulMultiplierQuarterUnits, 2));
        HashSet<int> zeroMultiplierFirstAssignments = [.. Enumerable.Range(0, zeroMultiplier.RoleCountAt(0)).Select(index => zeroMultiplier.RoleAt(0, index))];
        HashSet<int> zeroMultiplierSecondAssignments = [.. Enumerable.Range(0, zeroMultiplier.RoleCountAt(1)).Select(index => zeroMultiplier.RoleAt(1, index))];
        HashSet<int> skillMultiplierFirstAssignments = [.. Enumerable.Range(0, skillMultiplier.RoleCountAt(0)).Select(index => skillMultiplier.RoleAt(0, index))];
        HashSet<int> skillMultiplierSecondAssignments = [.. Enumerable.Range(0, skillMultiplier.RoleCountAt(1)).Select(index => skillMultiplier.RoleAt(1, index))];

        await Assert.That(zeroMultiplierFirstAssignments.SetEquals([1])).IsTrue();
        await Assert.That(zeroMultiplierSecondAssignments).IsEmpty();
        await Assert.That(zeroMultiplier.TryGetExplanation(1, role.Id, out RoleRecommendationExplanation removed)).IsTrue();
        await Assert.That(removed.RejectReason).IsEqualTo(PickRejectReason.RequiredCoverageFilled);
        await Assert.That(skillMultiplierFirstAssignments).IsEmpty();
        await Assert.That(skillMultiplierSecondAssignments.SetEquals([1])).IsTrue();
    }

    /// Two used-and-trained skills with gated content, Crafting primary:
    /// the two-skill covered set the optional-target rules read.
    private static void ApplyTwoSkillProfile(RoleView role, string token) =>
        RecsTestBed.SetSpec(role, RecsTestBed.Capability("Crafting", 0,
            RecsTestBed.Giver(token, used: ["Crafting"], trained: ["Crafting"], gates: ("Crafting", 1)),
            RecsTestBed.Giver($"{token}Research", used: ["Intellectual"], trained: ["Intellectual"], gates: ("Intellectual", 1))));
}
