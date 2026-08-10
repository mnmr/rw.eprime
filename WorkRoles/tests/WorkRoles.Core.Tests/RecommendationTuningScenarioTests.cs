using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

public class RecommendationTuningScenarioTests
{
    [Test]
    public async Task CoverageRepairPublishesItsStageAndSlots()
    {
        // Coverage repair is a defensive post-draft transition. A final-plan
        // fixture cannot reliably request that transient state without
        // duplicating the overlap resolver, so this tests the published facts
        // at the transition that owns them.
        RoleView role = RecsTestBed.Role(1, "Crafting");
        RecsTestBed.Require(role, 2, trainingWaivers: 2);
        PawnView first = RecsTestBed.Pawn();
        first.SkillLevels["Crafting"] = 8;
        first.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        PawnView second = RecsTestBed.Pawn();
        second.SkillLevels["Crafting"] = 7;
        second.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        var formulas = new RecommendationFormulaEngine(
            RecommendationsTuningOptions.Default);
        RolePlan plan = RolePlan.Build(
            new EngineContext(RecsTestBed.Colony(
                new List<RoleView> { role }, first, second)),
            role,
            formulas);

        plan.SelectForCoverage(0, 1);
        plan.SelectForCoverage(1, 2);

        await Assert.That(plan.SelectionStageAt(0))
            .IsEqualTo(RecommendationSelectionStage.CoverageRepair);
        await Assert.That(plan.SelectionSlotAt(0)).IsEqualTo(1);
        await Assert.That(plan.SelectionStageAt(1))
            .IsEqualTo(RecommendationSelectionStage.CoverageRepair);
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
        neutralTwenty.Existing.Add(new AssignmentView
        {
            RoleId = crafter.Id,
            Enabled = true,
        });
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { crafter }, greatTen, neutralTwenty);

        RecommendationsTuningOptions noSurplus =
            RecommendationsTuningOptions.Default.With(
                RecommendationTuningOption.SurplusMinimumSignal,
                (int)SignalBucket.Exceptional);
        RecommendationPlan defaults = RecommendationPlan.Build(
            colony, noSurplus);
        RecommendationPlan reducedGreatMultiplier = RecommendationPlan.Build(
            colony,
            noSurplus.With(
                RecommendationTuningOption.ChampionGreatMultiplierHalfUnits,
                1));

        await Assert.That(RoleIds(defaults, 0)).IsEqualTo("1");
        await Assert.That(RoleIds(defaults, 1)).IsEqualTo("");
        await Assert.That(RoleIds(reducedGreatMultiplier, 0)).IsEqualTo("");
        await Assert.That(RoleIds(reducedGreatMultiplier, 1)).IsEqualTo("1");
        await Assert.That(defaults.TryGetExplanation(
            0, crafter.Id, out RoleRecommendationExplanation selected)).IsTrue();
        await Assert.That(selected.Recommended).IsTrue();
        await Assert.That(selected.SelectionStage)
            .IsEqualTo(RecommendationSelectionStage.Required);
        await Assert.That(defaults.TryGetExplanation(
            1, crafter.Id, out RoleRecommendationExplanation removed)).IsTrue();
        await Assert.That(removed.Recommended).IsFalse();
        await Assert.That(removed.RejectReason)
            .IsEqualTo(PickRejectReason.RequiredCoverageFilled);
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
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { doctor, cook }, pawn);

        RecommendationPlan defaults = RecommendationPlan.Build(
            colony, RecommendationsTuningOptions.Default);
        RecommendationPlan reducedGreatPoints = RecommendationPlan.Build(
            colony,
            RecommendationsTuningOptions.Default.With(
                RecommendationTuningOption.OrderingGreatSignalPoints,
                0));

        await Assert.That(RoleIds(defaults, 0)).IsEqualTo("2,1");
        await Assert.That(RoleIds(reducedGreatPoints, 0)).IsEqualTo("1,2");
    }

    [Test]
    public async Task SemanticallyEqualEditPreservesTheOptionsSnapshot()
    {
        RecommendationsTuningOptions defaults =
            RecommendationsTuningOptions.Default;

        RecommendationsTuningOptions normalized = defaults.With(
            RecommendationTuningOption.ChampionSkillDivisor,
            2);

        await Assert.That(normalized).IsSameReferenceAs(defaults);
    }

    [Test]
    public async Task EveryFormulaOptionPublishesPersistenceAndUiMetadata()
    {
        IReadOnlyList<RecommendationTuningDescriptor> descriptors =
            RecommendationsTuningOptions.Descriptors;
        RecommendationTuningOption[] options =
            Enum.GetValues<RecommendationTuningOption>();

        await Assert.That(descriptors.Count).IsEqualTo(options.Length);
        await Assert.That(descriptors.Select(item => item.Option).Distinct().Count())
            .IsEqualTo(options.Length);
        await Assert.That(descriptors.Select(item => item.StableKey).Distinct().Count())
            .IsEqualTo(options.Length);
        foreach (RecommendationTuningDescriptor descriptor in descriptors)
        {
            await Assert.That(descriptor.StableKey).IsNotNullOrEmpty();
            await Assert.That(descriptor.SectionLabelKey).IsNotNullOrEmpty();
            await Assert.That(descriptor.LabelKey).IsNotNullOrEmpty();
            await Assert.That(descriptor.DescriptionKey).IsNotNullOrEmpty();
            await Assert.That(
                    RecommendationsTuningOptions.Default.Get(descriptor.Option))
                .IsEqualTo(descriptor.DefaultValue);
        }
    }

    [Test]
    public async Task RoleWithoutScalePublishesNoAssignments()
    {
        RoleView crafter = RecsTestBed.Role(1, "Crafting");
        crafter.Scale = null;
        RoleView obsoleteChore = RecsTestBed.Unskilled(2, "Hauling");
        obsoleteChore.Scale = null;
        PawnView[] pawns = Enumerable.Range(0, 7)
            .Select(index =>
            {
                PawnView pawn = RecsTestBed.Pawn();
                pawn.SkillLevels["Crafting"] = 20 - index;
                pawn.SignalBuckets["Crafting"] = SignalBucket.Neutral;
                return pawn;
            })
            .ToArray();
        pawns[0].Existing.Add(new AssignmentView
        {
            RoleId = obsoleteChore.Id,
            Enabled = true,
        });
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { crafter, obsoleteChore }, pawns);

        RecommendationsTuningOptions noLevelPromotions =
            RecommendationsTuningOptions.Default
                .With(RecommendationTuningOption.OptionalTargetGreatLevel, 21)
                .With(RecommendationTuningOption.OptionalTargetStrongLevel, 21);
        RecommendationPlan plan = RecommendationPlan.Build(
            colony, noLevelPromotions);

        await Assert.That(HolderCount(plan, crafter.Id)).IsEqualTo(0);
        await Assert.That(HolderCount(plan, obsoleteChore.Id)).IsEqualTo(0);
    }

    [Test]
    public async Task MinimumPickBonusesChangeThePublishedRoleOrder()
    {
        RoleView crafter = RecsTestBed.Role(1, "Crafting", "CraftingWork");
        RecsTestBed.Require(crafter, 2);
        RoleView cook = RecsTestBed.Role(2, "Cooking", "CookingWork");
        RecsTestBed.Require(cook, 1);

        PawnView first = RecsTestBed.Pawn();
        first.SkillLevels["Crafting"] = 12;
        first.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        first.SkillLevels["Cooking"] = 5;
        first.SignalBuckets["Cooking"] = SignalBucket.Neutral;
        PawnView second = RecsTestBed.Pawn();
        second.SkillLevels["Crafting"] = 10;
        second.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        second.SkillLevels["Cooking"] = 12;
        second.SignalBuckets["Cooking"] = SignalBucket.Neutral;
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { crafter, cook }, first, second);

        RecommendationPlan defaults = RecommendationPlan.Build(
            colony, RecommendationsTuningOptions.Default);
        RecommendationPlan noFirstBonus = RecommendationPlan.Build(
            colony,
            RecommendationsTuningOptions.Default.With(
                RecommendationTuningOption.FirstMinimumPickBonus,
                0));

        await Assert.That(RoleIds(defaults, 1)).IsEqualTo("2,1");
        await Assert.That(RoleIds(noFirstBonus, 1)).IsEqualTo("1,2");
    }

    [Test]
    public async Task OptionalTargetPointsChangeThePublishedTrainingRole()
    {
        RoleView target = RecsTestBed.Role(1, "Crafting", "TargetWork");
        target.Skills = TwoSkillProfile();
        target.PrimarySkill = "Crafting";
        RoleView trainee = RecsTestBed.Role(2, "Crafting", "TrainingWork");
        trainee.Skills = TwoSkillProfile();
        trainee.PrimarySkill = "Crafting";
        PathView path = RecsTestBed.Path(
            10, (trainee.Id, 0, 15), (target.Id, 15, 21));

        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = 10;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        pawn.SkillLevels["Intellectual"] = 10;
        pawn.SignalBuckets["Intellectual"] = SignalBucket.Neutral;
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { target, trainee }, pawn);
        colony.Paths.Add(path);

        RecommendationPlan defaults = RecommendationPlan.Build(
            colony, RecommendationsTuningOptions.Default);
        RecommendationPlan higherMinimum = RecommendationPlan.Build(
            colony,
            RecommendationsTuningOptions.Default.With(
                RecommendationTuningOption.OptionalTargetMinimumPoints,
                3));
        RecommendationPlan higherSkillCountThreshold = RecommendationPlan.Build(
            colony,
            RecommendationsTuningOptions.Default.With(
                RecommendationTuningOption.OptionalTargetMinimumSkillCount,
                3));

        await Assert.That(RoleIds(defaults, 0)).IsEqualTo("2");
        await Assert.That(RoleIds(higherMinimum, 0)).IsEqualTo("");
        await Assert.That(RoleIds(higherSkillCountThreshold, 0)).IsEqualTo("");
        await Assert.That(defaults.TryGetExplanation(
            0, trainee.Id, out RoleRecommendationExplanation training)).IsTrue();
        await Assert.That(training.RelatedRoleId).IsEqualTo(target.Id);
        await Assert.That(training.SelectionStage)
            .IsEqualTo(RecommendationSelectionStage.Surplus);
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

        RecommendationsTuningOptions loaded =
            RecommendationsTuningOptions.FromValues(persisted);

        await Assert.That(loaded.Get(
                RecommendationTuningOption.OptionalTargetStrongLevel))
            .IsEqualTo(18);
        await Assert.That(loaded.Get(
                RecommendationTuningOption.OptionalTargetGreatLevel))
            .IsEqualTo(20);
        await Assert.That(loaded.Get(
                RecommendationTuningOption.HunterFirstTierMaximum))
            .IsEqualTo(12);
        await Assert.That(loaded.Get(
                RecommendationTuningOption.HunterSecondTierMaximum))
            .IsEqualTo(16);
        await Assert.That(loaded.Get(
                RecommendationTuningOption.HunterThirdTierMaximum))
            .IsEqualTo(19);
    }

    [Test]
    public async Task ZeroMinimumBonusStillPublishesCoverageDraftExplanation()
    {
        RoleView role = RecsTestBed.Role(1, "Crafting");
        RecsTestBed.Require(role, 1);
        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = 10;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { role }, pawn);
        RecommendationsTuningOptions options =
            RecommendationsTuningOptions.Default.With(
                RecommendationTuningOption.FirstMinimumPickBonus,
                0);

        RecommendationPlan plan = RecommendationPlan.Build(colony, options);

        await Assert.That(RoleIds(plan, 0)).IsEqualTo("1");
        await Assert.That(plan.TryGetExplanation(
            0,
            role.Id,
            out RoleRecommendationExplanation explanation)).IsTrue();
        await Assert.That(explanation.SelectionStage)
            .IsEqualTo(RecommendationSelectionStage.Required);
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
        pawn.Existing.Add(new AssignmentView
        {
            RoleId = role.Id,
            Enabled = true,
        });
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { role }, pawn);
        RecommendationsTuningOptions options =
            RecommendationsTuningOptions.Default.With(
                RecommendationTuningOption.CandidateMinimumSignal,
                (int)SignalBucket.Strong);

        RecommendationPlan plan = RecommendationPlan.Build(colony, options);

        await Assert.That(RoleIds(plan, 0)).IsEqualTo("");
        await Assert.That(plan.TryGetExplanation(
            0,
            role.Id,
            out RoleRecommendationExplanation explanation)).IsTrue();
        await Assert.That(explanation.RejectReason)
            .IsEqualTo(PickRejectReason.WeakSignal);
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
        second.Existing.Add(new AssignmentView
        {
            RoleId = role.Id,
            Enabled = true,
        });
        ColonyView colony = RecsTestBed.Colony(
            new List<RoleView> { role }, first, second);
        RecommendationsTuningOptions admitsAwful =
            RecommendationsTuningOptions.Default.With(
                RecommendationTuningOption.CandidateMinimumSignal,
                (int)SignalBucket.Awful);

        RecommendationPlan zeroMultiplier = RecommendationPlan.Build(
            colony, admitsAwful);
        RecommendationPlan skillMultiplier = RecommendationPlan.Build(
            colony,
            admitsAwful.With(
                RecommendationTuningOption.ChampionAwfulMultiplierHalfUnits,
                2));

        await Assert.That(RoleIds(zeroMultiplier, 0)).IsEqualTo("1");
        await Assert.That(RoleIds(zeroMultiplier, 1)).IsEqualTo("");
        await Assert.That(zeroMultiplier.TryGetExplanation(
            1,
            role.Id,
            out RoleRecommendationExplanation removed)).IsTrue();
        await Assert.That(removed.RejectReason)
            .IsEqualTo(PickRejectReason.RequiredCoverageFilled);
        await Assert.That(RoleIds(skillMultiplier, 0)).IsEqualTo("");
        await Assert.That(RoleIds(skillMultiplier, 1)).IsEqualTo("1");
    }

    private static string RoleIds(RecommendationPlan plan, int pawnIndex)
    {
        var ids = new List<int>();
        for (int index = 0; index < plan.RoleCountAt(pawnIndex); index++)
            ids.Add(plan.RoleAt(pawnIndex, index));
        return string.Join(",", ids);
    }

    private static int HolderCount(RecommendationPlan plan, int roleId)
    {
        int count = 0;
        for (int pawnIndex = 0; pawnIndex < plan.PawnCount; pawnIndex++)
            for (int roleIndex = 0;
                 roleIndex < plan.RoleCountAt(pawnIndex);
                 roleIndex++)
                if (plan.RoleAt(pawnIndex, roleIndex) == roleId)
                    count++;
        return count;
    }

    private static List<RoleSkillView> TwoSkillProfile() =>
        new List<RoleSkillView>
        {
            new RoleSkillView
            {
                SkillDefName = "Crafting",
                Primary = true,
                RequiredContent = 1,
            },
            new RoleSkillView
            {
                SkillDefName = "Intellectual",
                RequiredContent = 1,
            },
        };
}
