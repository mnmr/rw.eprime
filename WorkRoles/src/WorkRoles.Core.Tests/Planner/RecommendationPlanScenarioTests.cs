using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

public class RecommendationPlanScenarioTests
{
    [Test]
    public async Task ExplicitRequiredSkillDisqualifiesOnlyThePawnMissingThatSkill()
    {
        var recs = new RecsProjection().WorkType(
            "Crafting", "Crafting", 400, "Craft");
        RecommendationRoleSource maker = recs.RoleByWorkType(
            1, 0, 100, "Crafting");
        maker.DeclaredRequiredSkills = ["Medicine"];
        var missing = new PawnView { CapableWorkTypes = { "Crafting" } };
        missing.SkillLevels["Crafting"] = 12;
        missing.SignalBuckets["Crafting"] = SignalBucket.Great;
        var qualified = new PawnView { CapableWorkTypes = { "Crafting" } };
        qualified.SkillLevels["Crafting"] = 4;
        qualified.SkillLevels["Medicine"] = 0;
        qualified.SignalBuckets["Crafting"] = SignalBucket.Neutral;

        RecommendationPlan plan = recs.Plan(missing, qualified);

        await Assert.That(RecsProjection.Holds(plan, 0, maker.Id)).IsFalse();
        await Assert.That(RecsProjection.Holds(plan, 1, maker.Id)).IsTrue();
    }

    [Test]
    public async Task UnskilledRoleAllowsAPawnWhoCanDoOnlySomeOfItsWorkTypes()
    {
        var recs = new RecsProjection()
            .WorkType("Hauling", null, 100, "Haul")
            .WorkType("Cleaning", null, 90, "Clean");
        RecommendationRoleSource core = recs.RoleByWorkType(
            1, 0, 100, "Hauling", "Cleaning");
        var partial = new PawnView { CapableWorkTypes = { "Hauling" } };

        RecommendationPlan plan = recs.Plan(partial);

        await Assert.That(RecsProjection.Holds(plan, 0, core.Id)).IsTrue();
    }

    [Test]
    public async Task SkilledRoleStillRequiresEveryCoveredWorkType()
    {
        var recs = new RecsProjection()
            .WorkType("Tailoring", "Crafting", 400, "Tailor")
            .WorkType("Smithing", "Crafting", 390, "Smith");
        RecommendationRoleSource crafter = recs.RoleByWorkType(
            1, 0, 100, "Tailoring", "Smithing");
        var partial = new PawnView { CapableWorkTypes = { "Tailoring" } };
        partial.SkillLevels["Crafting"] = 12;
        partial.SignalBuckets["Crafting"] = SignalBucket.Great;

        RecommendationPlan plan = recs.Plan(partial);

        await Assert.That(RecsProjection.Holds(plan, 0, crafter.Id)).IsFalse();
    }

    [Test]
    public async Task AutoAssignUsesTheSameSkilledVersusUnskilledCapabilityRule()
    {
        var recs = new RecsProjection()
            .WorkType("EssentialA", null, 1000, "EssentialAJob")
            .WorkType("EssentialB", null, 990, "EssentialBJob")
            .WorkType("SkilledA", "Crafting", 500, "SkilledAJob")
            .WorkType("SkilledB", "Crafting", 490, "SkilledBJob");
        RecommendationRoleSource unskilled = recs.RoleByWorkType(
            1, 0, 0, "EssentialA", "EssentialB");
        RecommendationRoleSource skilled = recs.RoleByWorkType(
            2, 0, 0, "SkilledA", "SkilledB");
        recs.AutoAssign(unskilled.Id).AutoAssign(skilled.Id);
        var pawn = new PawnView
        {
            CapableWorkTypes = { "EssentialA", "SkilledA" },
        };
        pawn.SkillLevels["Crafting"] = 12;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Great;

        RecommendationPlan plan = recs.Plan(pawn);

        await Assert.That(RecsProjection.Holds(plan, 0, unskilled.Id)).IsTrue();
        await Assert.That(RecsProjection.Holds(plan, 0, skilled.Id)).IsFalse();
    }

    [Test]
    public async Task ScaleRequiredTotalIncludesTrainingWaiverAssignments()
    {
        RoleView target = CraftingRole(100, "TargetWork");
        RecsTestBed.AddGate(target, "Intellectual");
        RecsTestBed.Require(target, 3);

        RoleView craftTrainee = CraftingRole(101, "CraftTraineeWork");
        RoleView researchTrainee = SkilledRole(102, "ResearchTraineeWork", "Intellectual");

        PathView path = RecsTestBed.Path(200, (craftTrainee.Id, 0, 15), (researchTrainee.Id, 0, 15), (target.Id, 15, 21));
        PathView lessSuitablePath = RecsTestBed.Path(201, (craftTrainee.Id, 0, 15), (researchTrainee.Id, 0, 15), (target.Id, 18, 21));

        PawnView firstDirect = CraftingPawn(16, ("TargetWork", SignalBucket.Neutral), ("CraftTraineeWork", SignalBucket.Neutral), ("ResearchTraineeWork", SignalBucket.Neutral));
        firstDirect.SkillLevels["Intellectual"] = 16;
        firstDirect.SignalBuckets["Intellectual"] = SignalBucket.Neutral;
        PawnView secondDirect = CraftingPawn(15, ("TargetWork", SignalBucket.Neutral), ("CraftTraineeWork", SignalBucket.Neutral), ("ResearchTraineeWork", SignalBucket.Neutral));
        secondDirect.SkillLevels["Intellectual"] = 15;
        secondDirect.SignalBuckets["Intellectual"] = SignalBucket.Neutral;
        PawnView trainingWaiver = CraftingPawn(10, ("TargetWork", SignalBucket.Neutral), ("CraftTraineeWork", SignalBucket.Neutral), ("ResearchTraineeWork", SignalBucket.Neutral));
        trainingWaiver.SkillLevels["Intellectual"] = 11;
        trainingWaiver.SignalBuckets["Intellectual"] = SignalBucket.Neutral;

        ColonyView colony = RecsTestBed.Colony([target, craftTrainee, researchTrainee], firstDirect, secondDirect, trainingWaiver);
        colony.Paths.Add(path);
        colony.Paths.Add(lessSuitablePath);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> firstDirectAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> secondDirectAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];
        HashSet<int> trainingWaiverAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(2)).Select(index => plan.RoleAt(2, index))];

        await Assert.That(firstDirectAssignments.SetEquals([100])).IsTrue();
        await Assert.That(secondDirectAssignments.SetEquals([100])).IsTrue();
        await Assert.That(trainingWaiverAssignments.SetEquals([101, 102])).IsTrue();
        await Assert.That(plan.PathAt(2, 0)).IsEqualTo(200);
        await Assert.That(plan.PathActivatedAt(2, 0)).IsTrue();
        await Assert.That(plan.TryGetExplanation(0, target.Id, out RoleRecommendationExplanation first)).IsTrue();
        await Assert.That(first.SelectionStage).IsEqualTo(RecommendationSelectionStage.Required);
        await Assert.That(first.SelectionSlot).IsEqualTo(1);
        await Assert.That(first.SelectionSlotCount).IsEqualTo(3);
        await Assert.That(plan.TryGetExplanation(1, target.Id, out RoleRecommendationExplanation second)).IsTrue();
        await Assert.That(second.SelectionStage).IsEqualTo(RecommendationSelectionStage.Required);
        await Assert.That(second.SelectionSlot).IsEqualTo(2);
        await Assert.That(second.SelectionSlotCount).IsEqualTo(3);
        await Assert.That(plan.TryGetExplanation(2, craftTrainee.Id, out RoleRecommendationExplanation explanation)).IsTrue();
        await Assert.That(explanation.RelatedRoleId).IsEqualTo(100);
        await Assert.That(explanation.RequiredTotal).IsEqualTo(3);
        // Derived demand: with colonyMin 3 one slot is guaranteed direct and
        // the remaining total − min(1, colonyMin) = 2 are waivable to trainees.
        await Assert.That(explanation.TrainingWaivers).IsEqualTo(2);
        await Assert.That(explanation.SelectionStage).IsEqualTo(RecommendationSelectionStage.TrainingWaiver);
        await Assert.That(explanation.CandidateRank).IsEqualTo(3);
        await Assert.That(explanation.CandidateCount).IsEqualTo(3);
        // The band-qualified second pawn consumed waiver rank 1 (published as
        // Required); the trainee pick is the second waivable slot.
        await Assert.That(explanation.StageRank).IsEqualTo(2);
        await Assert.That(explanation.SelectionSlot).IsEqualTo(3);
        await Assert.That(explanation.SelectionSlotCount).IsEqualTo(3);
        await Assert.That(explanation.TrainingSkills.Count).IsEqualTo(2);
        await Assert.That(explanation.TrainingSkills[0].SkillDefName).IsEqualTo("Crafting");
        await Assert.That(explanation.TrainingSkills[0].PawnLevel).IsEqualTo(10);
        await Assert.That(explanation.TrainingSkills[0].TargetMinimum).IsEqualTo(15);
        await Assert.That(explanation.TrainingSkills[1].SkillDefName).IsEqualTo("Intellectual");
        await Assert.That(explanation.TrainingSkills[1].PawnLevel).IsEqualTo(11);
        await Assert.That(explanation.TrainingSkills[1].TargetMinimum).IsEqualTo(15);
        await Assert.That(plan.TryGetExplanation(2, researchTrainee.Id, out RoleRecommendationExplanation researchExplanation)).IsTrue();
        await Assert.That(researchExplanation.RelatedRoleId).IsEqualTo(100);
        await Assert.That(researchExplanation.SelectionStage).IsEqualTo(RecommendationSelectionStage.TrainingWaiver);
        await Assert.That(researchExplanation.SelectionSlot).IsEqualTo(3);
        await Assert.That(researchExplanation.SelectionSlotCount).IsEqualTo(3);
        await Assert.That(researchExplanation.TrainingSkills.Count).IsEqualTo(2);
        await Assert.That(researchExplanation.TrainingSkills[0].TargetMinimum).IsEqualTo(15);
        await Assert.That(researchExplanation.TrainingSkills[1].TargetMinimum).IsEqualTo(15);
    }

    [Test]
    public async Task MinimumAgeExcludesUnderAgePawnsFromFinalAssignments()
    {
        // The under-age pawn has the best skill: without the age gate it would
        // be picked first. At the gate (13) inclusive, so the exactly-13 teen
        // and the adult (default ancient age) fill the two required slots.
        RoleView role = CraftingRole(100, "CraftWork");
        role.MinAge = 13;
        RecsTestBed.Require(role, 2);

        PawnView child = CraftingPawn(12, ("CraftWork", SignalBucket.Neutral));
        child.BiologicalAgeTicks = 10L * BiologicalAge.TicksPerYear;
        PawnView teen = CraftingPawn(8, ("CraftWork", SignalBucket.Neutral));
        teen.BiologicalAgeTicks = 13L * BiologicalAge.TicksPerYear;
        PawnView adult = CraftingPawn(6, ("CraftWork", SignalBucket.Neutral));

        ColonyView colony = RecsTestBed.Colony([role], child, teen, adult);
        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> childAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> teenAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];
        HashSet<int> adultAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(2)).Select(index => plan.RoleAt(2, index))];

        await Assert.That(childAssignments).IsEmpty();
        await Assert.That(teenAssignments.SetEquals([100])).IsTrue();
        await Assert.That(adultAssignments.SetEquals([100])).IsTrue();
    }

    [Test]
    public async Task MaximumAgeExcludesOverAgePawnsFromFinalAssignments()
    {
        // The adult (default ancient age) has the best skill: without the age
        // cap it would be picked first. The cap (12) is inclusive, so the pawn
        // one tick short of turning 13 still counts as 12 and fills a slot
        // alongside the younger child.
        RoleView role = CraftingRole(100, "CraftWork");
        role.MaxAge = 12;
        RecsTestBed.Require(role, 2);

        PawnView adult = CraftingPawn(12, ("CraftWork", SignalBucket.Neutral));
        PawnView atCap = CraftingPawn(8, ("CraftWork", SignalBucket.Neutral));
        atCap.BiologicalAgeTicks = 13L * BiologicalAge.TicksPerYear - 1;
        PawnView child = CraftingPawn(6, ("CraftWork", SignalBucket.Neutral));
        child.BiologicalAgeTicks = 9L * BiologicalAge.TicksPerYear;

        ColonyView colony = RecsTestBed.Colony([role], adult, atCap, child);
        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> adultAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> atCapAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];
        HashSet<int> childAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(2)).Select(index => plan.RoleAt(2, index))];

        await Assert.That(adultAssignments).IsEmpty();
        await Assert.That(atCapAssignments.SetEquals([100])).IsTrue();
        await Assert.That(childAssignments.SetEquals([100])).IsTrue();
    }

    [Test]
    public async Task AgeExemptPawnsIgnoreRoleAgeLimitsInFinalAssignments()
    {
        // The role accepts ages 13 through 16. The ordinary under- and over-age
        // pawns stay excluded, while pawns whose race bypasses age work limits
        // remain eligible on either side of the range.
        RoleView role = CraftingRole(100, "CraftWork");
        role.MinAge = 13;
        role.MaxAge = 16;
        RecsTestBed.Require(role, 3);

        PawnView ordinaryChild = CraftingPawn(20, ("CraftWork", SignalBucket.Neutral));
        ordinaryChild.BiologicalAgeTicks = 10L * BiologicalAge.TicksPerYear;
        PawnView exemptChild = CraftingPawn(16, ("CraftWork", SignalBucket.Neutral));
        exemptChild.BiologicalAgeTicks = 10L * BiologicalAge.TicksPerYear;
        exemptChild.AgeLimitsApply = false;
        PawnView ordinaryTeen = CraftingPawn(12, ("CraftWork", SignalBucket.Neutral));
        ordinaryTeen.BiologicalAgeTicks = 15L * BiologicalAge.TicksPerYear;
        PawnView exemptAdult = CraftingPawn(8, ("CraftWork", SignalBucket.Neutral));
        exemptAdult.BiologicalAgeTicks = 40L * BiologicalAge.TicksPerYear;
        exemptAdult.AgeLimitsApply = false;
        PawnView ordinaryAdult = CraftingPawn(4, ("CraftWork", SignalBucket.Neutral));
        ordinaryAdult.BiologicalAgeTicks = 40L * BiologicalAge.TicksPerYear;

        ColonyView colony = RecsTestBed.Colony(
            [role], ordinaryChild, exemptChild, ordinaryTeen, exemptAdult, ordinaryAdult);
        RecommendationPlan plan = RecommendationPlan.Build(colony);

        HashSet<int>[] assignments = Enumerable.Range(0, 5)
            .Select(pawnIndex => Enumerable.Range(0, plan.RoleCountAt(pawnIndex))
                .Select(roleIndex => plan.RoleAt(pawnIndex, roleIndex))
                .ToHashSet())
            .ToArray();

        await Assert.That(assignments[0]).IsEmpty();
        await Assert.That(assignments[1].SetEquals([100])).IsTrue();
        await Assert.That(assignments[2].SetEquals([100])).IsTrue();
        await Assert.That(assignments[3].SetEquals([100])).IsTrue();
        await Assert.That(assignments[4]).IsEmpty();
    }

    [Test]
    public async Task UnskilledDemandAssignsEveryCapablePawnAndNamesOneChampion()
    {
        // Through the real projection: a skill-less role (Hauling) with demand
        // fills every capable pawn and names one required champion.
        var recs = new RecsProjection().WorkType("Hauling", null, 100);
        RecommendationRoleSource hauler = recs.RoleByWorkType(1, 1, 0, "Hauling");
        PawnView Worker() => new() { CapableWorkTypes = { "Hauling" } };

        RecommendationPlan plan = recs.Plan(Worker(), Worker(), Worker());
        HashSet<int> firstAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> secondAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];
        HashSet<int> thirdAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(2)).Select(index => plan.RoleAt(2, index))];

        await Assert.That(firstAssignments.SetEquals([1])).IsTrue();
        await Assert.That(secondAssignments.SetEquals([1])).IsTrue();
        await Assert.That(thirdAssignments.SetEquals([1])).IsTrue();
        await Assert.That(plan.TryGetExplanation(0, hauler.Id, out RoleRecommendationExplanation firstExplanation)).IsTrue();
        await Assert.That(plan.TryGetExplanation(1, hauler.Id, out RoleRecommendationExplanation secondExplanation)).IsTrue();
        await Assert.That(plan.TryGetExplanation(2, hauler.Id, out RoleRecommendationExplanation thirdExplanation)).IsTrue();
        RecommendationSelectionStage[] selectionStages = [firstExplanation.SelectionStage, secondExplanation.SelectionStage, thirdExplanation.SelectionStage];
        await Assert.That(selectionStages.Count(stage => stage == RecommendationSelectionStage.Required)).IsEqualTo(1);
    }

    [Test]
    public async Task UnskilledRoleWithoutDemandAssignsNoCapablePawns()
    {
        var recs = new RecsProjection().WorkType("Hauling", null, 100);
        recs.RoleByWorkType(1, 0, 0, "Hauling");
        PawnView Worker() => new() { CapableWorkTypes = { "Hauling" } };

        RecommendationPlan plan = recs.Plan(Worker(), Worker(), Worker());
        HashSet<int> firstAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> secondAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];
        HashSet<int> thirdAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(2)).Select(index => plan.RoleAt(2, index))];

        await Assert.That(firstAssignments).IsEmpty();
        await Assert.That(secondAssignments).IsEmpty();
        await Assert.That(thirdAssignments).IsEmpty();
    }

    [Test]
    public async Task UnskilledNoMinScaleAssignsEveryoneThroughTheRealProjection()
    {
        // Full recommendation pipeline: role sources -> catalog projection ->
        // colony -> plan, the same path the game adapter feeds. Grunt is an
        // Unskilled "NoMin" role: an all-zero scale, including Max. Everyone
        // capable must still be recommended it.
        var jobs = new FakeCatalog().WithWorkType("Hauling", "Haul").WithWorkType("Cleaning", "Clean");
        var grunt = new RecommendationRoleSource
        {
            Id = 1,
            Coverage = 100,
            Entries = [new JobEntry(JobEntryKind.WorkType, "Hauling"), new JobEntry(JobEntryKind.WorkType, "Cleaning")],
        };
        RecommendationCatalogProjection projection = RecommendationCatalogBuilder.Build(
            [grunt],
            [],
            jobs,
            new Dictionary<string, int> { ["Hauling"] = 100, ["Cleaning"] = 50 },
            UnskilledJobProfiles()
        );
        PawnView Worker() => new PawnView { CapableWorkTypes = { "Hauling", "Cleaning" } };
        ColonyView colony = projection.CreateColony([grunt.Id], [Worker(), Worker(), Worker()]);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> firstAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> secondAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];
        HashSet<int> thirdAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(2)).Select(index => plan.RoleAt(2, index))];

        await Assert.That(firstAssignments.SetEquals([1])).IsTrue();
        await Assert.That(secondAssignments.SetEquals([1])).IsTrue();
        await Assert.That(thirdAssignments.SetEquals([1])).IsTrue();
        // Full coverage publishes as "everyone capable", not a slot count.
        await Assert.That(plan.TryGetExplanation(
            0, grunt.Id, out RoleRecommendationExplanation explanation)).IsTrue();
        await Assert.That(explanation.EveryoneCapable).IsTrue();
    }

    [Test]
    public async Task MinimumAgeGatesAssignmentThroughTheRealProjection()
    {
        // Same all-zero-scale Unskilled shape as above (everyone capable gets
        // the role), plus a source MinAge: only the pawn past the floor holds
        // it, proving the age gate survives the catalog projection.
        var jobs = new FakeCatalog().WithWorkType("Hauling", "Haul");
        var grunt = new RecommendationRoleSource
        {
            Id = 1,
            MinAge = 3,
            Coverage = 100,
            Entries = [new JobEntry(JobEntryKind.WorkType, "Hauling")],
        };
        RecommendationCatalogProjection projection = RecommendationCatalogBuilder.Build([grunt], [], jobs, new Dictionary<string, int> { ["Hauling"] = 100 }, UnskilledJobProfiles());
        PawnView Worker(int ageYears) => new PawnView { CapableWorkTypes = { "Hauling" }, BiologicalAgeTicks = ageYears * BiologicalAge.TicksPerYear };
        ColonyView colony = projection.CreateColony([grunt.Id], [Worker(1), Worker(5)]);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> underAgeAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> eligibleAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];

        await Assert.That(underAgeAssignments).IsEmpty();
        await Assert.That(eligibleAssignments.SetEquals([1])).IsTrue();
    }

    [Test]
    public async Task MaximumAgeGatesAssignmentThroughTheRealProjection()
    {
        // Same all-zero-scale Unskilled shape as above, plus a source MaxAge:
        // the exactly-at-cap pawn holds it (inclusive), the older pawn does
        // not, proving the cap survives the catalog projection.
        var jobs = new FakeCatalog().WithWorkType("Hauling", "Haul");
        var grunt = new RecommendationRoleSource
        {
            Id = 1,
            MaxAge = 3,
            Coverage = 100,
            Entries = [new JobEntry(JobEntryKind.WorkType, "Hauling")],
        };
        RecommendationCatalogProjection projection = RecommendationCatalogBuilder.Build([grunt], [], jobs, new Dictionary<string, int> { ["Hauling"] = 100 }, UnskilledJobProfiles());
        PawnView Worker(int ageYears) => new PawnView { CapableWorkTypes = { "Hauling" }, BiologicalAgeTicks = ageYears * BiologicalAge.TicksPerYear };
        ColonyView colony = projection.CreateColony([grunt.Id], [Worker(3), Worker(4)]);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> atCapAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> overAgeAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];

        await Assert.That(atCapAssignments.SetEquals([1])).IsTrue();
        await Assert.That(overAgeAssignments).IsEmpty();
    }

    private static JobProfileIndex UnskilledJobProfiles()
    {
        var builder = new JobProfileIndexBuilder();
        JobProfileSkillSource[] none = [];
        builder.AddWorkType(1, "Hauling", none, ["Haul"]);
        builder.AddWorkType(2, "Cleaning", none, ["Clean"]);
        builder.AddGiver("Haul", 1, none, hasCuratedXp: true, curatedXpSkillDefNames: []);
        builder.AddGiver("Clean", 2, none, hasCuratedXp: true, curatedXpSkillDefNames: []);
        return builder.Build();
    }

    [Test]
    public async Task SurplusMedicPawnGetsNeverTraineeRoleViaPath()
    {
        // Through the real projection: Medic is a Never training role
        // controlled by Doctor; a surplus medical pawn in the Medic band should
        // still receive it via the path.
        var recs = new RecsProjection().WorkType("Doctoring", "Medicine", 100, "Operate", "Tend");
        RecommendationRoleSource doctor = recs.RoleByWorkType(1, 1, 0, "Doctoring");
        RecommendationRoleSource medic = recs.RoleByGiver(2, 0, 0, "Tend");
        PathView path = RecsTestBed.Path(10, (medic.Id, 5, 15), (doctor.Id, 15, 21));
        recs.Path(path);

        RecommendationPlan plan = recs.Plan(MedicPawn(18, SignalBucket.Strong), MedicPawn(11, SignalBucket.Strong));
        HashSet<int> doctorAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> medicAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];

        // Strong doctor fills the required slot; the surplus medical pawn gets
        // the Never Medic trainee via the path, not nothing.
        await Assert.That(doctorAssignments.SetEquals([1])).IsTrue();
        await Assert.That(medicAssignments.SetEquals([2])).IsTrue();
    }

    private static PawnView MedicPawn(int medicine, SignalBucket signal)
    {
        var pawn = new PawnView { CapableWorkTypes = { "Doctoring" } };
        pawn.SkillLevels["Medicine"] = medicine;
        pawn.SignalBuckets["Medicine"] = signal;
        return pawn;
    }

    [Test]
    public async Task NeverTraineeExplainsItsControllingTrainingTarget()
    {
        // Medic/Nurse are Never training roles controlled by Doctor. A pawn that
        // does not receive Medic must not be told the role is "not configured";
        // its explanation names the Doctor that controls it.
        var recs = new RecsProjection().WorkType("Doctoring", "Medicine", 100, "Operate", "Tend", "Feed");
        RecommendationRoleSource doctor = recs.RoleByGiver(1, 1, 0, "Operate", "Tend", "Feed");
        RecommendationRoleSource medic = recs.RoleByGiver(2, 0, 0, "Tend", "Feed");
        PathView path = RecsTestBed.Path(10, (medic.Id, 5, 15), (doctor.Id, 15, 21));
        recs.Path(path);

        // A strong doctor fills Doctor directly and never becomes a Medic; the
        // pawn currently holds Medic, so it gets a Medic explanation.
        PawnView doc = MedicPawn(18, SignalBucket.Strong);
        doc.Existing.Add(new AssignmentView { RoleId = medic.Id, Enabled = true });
        RecommendationPlan plan = recs.Plan(doc);
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];

        await Assert.That(assignments.SetEquals([1])).IsTrue();
        await Assert.That(plan.TryGetExplanation(0, medic.Id, out RoleRecommendationExplanation medicExplanation)).IsTrue();
        await Assert.That(medicExplanation.RejectReason).IsEqualTo(PickRejectReason.ControlledByTarget);
        await Assert.That(medicExplanation.RelatedRoleId).IsEqualTo(1);
    }

    [Test]
    public async Task PinnedAssignmentToANeverRoleIsCarriedIntoRecommendations()
    {
        // Through the real projection: a pawn pinned to a Never role keeps it.
        var recs = new RecsProjection().WorkType("Hauling", null, 100);
        RecommendationRoleSource never = recs.RoleByWorkType(1, 0, 0, "Hauling");
        var pawn = new PawnView { CapableWorkTypes = { "Hauling" } };
        pawn.Existing.Add(
            new AssignmentView
            {
                RoleId = never.Id,
                Enabled = true,
                Pinned = true,
            }
        );

        RecommendationPlan plan = recs.Plan(pawn);
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];

        await Assert.That(assignments.SetEquals([1])).IsTrue();
    }

    [Test]
    public async Task CoverageSatisfiesRequiredHoldersOfACoveredSubRole()
    {
        // Through the real projection: Grunt covers Hauling + Cleaning; Hauler
        // is the Hauling subset.
        var recs = new RecsProjection().WorkType("Hauling", null, 100).WorkType("Cleaning", null, 50);
        RecommendationRoleSource grunt = recs.RoleByWorkType(1, 0, 100, "Hauling", "Cleaning");
        RecommendationRoleSource hauler = recs.RoleByWorkType(2, 2, 100, "Hauling");
        PawnView Capable() => new PawnView { CapableWorkTypes = { "Hauling", "Cleaning" } };

        RecommendationPlan plan = recs.Plan(Capable(), Capable(), Capable(), Capable());
        HashSet<int> firstAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> secondAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];
        HashSet<int> thirdAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(2)).Select(index => plan.RoleAt(2, index))];
        HashSet<int> fourthAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(3)).Select(index => plan.RoleAt(3, index))];

        // Grunt covers everyone, which satisfies Hauler's required holders
        // too: a pawn already doing the work never re-picks the covered role.
        await Assert.That(firstAssignments.SetEquals([1])).IsTrue();
        await Assert.That(secondAssignments.SetEquals([1])).IsTrue();
        await Assert.That(thirdAssignments.SetEquals([1])).IsTrue();
        await Assert.That(fourthAssignments.SetEquals([1])).IsTrue();
    }

    [Test]
    public async Task AutoAssignRolesFollowTheDefaultOrderNotExistingOrder()
    {
        // Auto-assign roles are automatic, not user-placed: they follow the
        // default recommendation order (front), even if the pawn's existing
        // order lists a late chore ahead of them.
        var recs = new RecsProjection().WorkType("Essential", null, 1000, "Ess").WorkType("Hauling", null, 50, "Haul");
        RecommendationRoleSource essential = recs.RoleByWorkType(1, 0, 0, "Essential");
        recs.AutoAssign(essential.Id);
        RecommendationRoleSource hauler = recs.RoleByWorkType(2, 0, 100, "Hauling");
        var pawn = new PawnView { CapableWorkTypes = { "Essential", "Hauling" } };
        // Existing order puts the late chore before the auto-assign role.
        pawn.Existing.Add(new AssignmentView { RoleId = 2, Enabled = true });
        pawn.Existing.Add(new AssignmentView { RoleId = 1, Enabled = true });

        RecommendationPlan plan = recs.Plan(pawn);
        string actualOrder = string.Join(",", Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index)));

        await Assert.That(actualOrder).IsEqualTo("1,2");
    }

    [Test]
    public async Task UnskilledSubRolesFoldUnderACoveringUnskilledRole()
    {
        // Grunt (Unskilled NoMin) covers Hauling + Cleaning; Hauler and Cleaner
        // are req-0 Unskilled subsets. Everyone gets Grunt; the covered
        // sub-roles fold away entirely (no dedicated req to keep).
        var recs = new RecsProjection().WorkType("Hauling", null, 100).WorkType("Cleaning", null, 50);
        RecommendationRoleSource grunt = recs.RoleByWorkType(1, 0, 100, "Hauling", "Cleaning");
        RecommendationRoleSource hauler = recs.RoleByWorkType(2, 0, 100, "Hauling");
        RecommendationRoleSource cleaner = recs.RoleByWorkType(3, 0, 100, "Cleaning");
        // Pawns already hold all three (as the real colony does): the covered
        // sub-roles must still fold, not be retained as chores.
        PawnView Worker()
        {
            var pawn = new PawnView { CapableWorkTypes = { "Hauling", "Cleaning" } };
            pawn.Existing.Add(new AssignmentView { RoleId = 1, Enabled = true });
            pawn.Existing.Add(new AssignmentView { RoleId = 2, Enabled = true });
            pawn.Existing.Add(new AssignmentView { RoleId = 3, Enabled = true });
            return pawn;
        }

        RecommendationPlan plan = recs.Plan(Worker(), Worker(), Worker());
        HashSet<int> firstAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> secondAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];
        HashSet<int> thirdAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(2)).Select(index => plan.RoleAt(2, index))];

        await Assert.That(firstAssignments.SetEquals([1])).IsTrue();
        await Assert.That(secondAssignments.SetEquals([1])).IsTrue();
        await Assert.That(thirdAssignments.SetEquals([1])).IsTrue();
    }

    [Test]
    public async Task PinnedRoleKeepsItsExistingOrder()
    {
        // A pinned role is user-placed: it stays at its existing slot even when
        // the default order would sort it earlier.
        var recs = new RecsProjection().WorkType("Essential", null, 1000, "Ess").WorkType("Hauling", null, 50, "Haul");
        RecommendationRoleSource essential = recs.RoleByWorkType(1, 0, 0, "Essential");
        recs.AutoAssign(essential.Id);
        RecommendationRoleSource pinned = recs.RoleByWorkType(2, 1, 0, "Hauling");
        var pawn = new PawnView { CapableWorkTypes = { "Essential", "Hauling" } };
        pawn.SkillLevels["Hauling"] = 0;
        // Pinned to the late chore, listed first; the pin holds it ahead of the
        // auto-assign role.
        pawn.Existing.Add(
            new AssignmentView
            {
                RoleId = pinned.Id,
                Enabled = true,
                Pinned = true,
            }
        );
        pawn.Existing.Add(new AssignmentView { RoleId = 1, Enabled = true });

        RecommendationPlan plan = recs.Plan(pawn);
        string actualOrder = string.Join(",", Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index)));

        await Assert.That(actualOrder).IsEqualTo("2,1");
    }

    [Test]
    public async Task WaiverSlotFillsWithTraineeNotAWeakDirectTarget()
    {
        // Through the real projection: Artist covers all Art work (Sculpt +
        // Paint); Painter is the paint subset and Never (no own demand),
        // mirroring the shipped WS_PathArtist Painter -> Artist path.
        var recs = new RecsProjection().WorkType("Art", "Artistic", 100, "Sculpt", "Paint");
        RecommendationRoleSource artist = recs.RoleByWorkType(1, 2, 0, "Art");
        RecommendationRoleSource painter = recs.RoleByGiver(2, 0, 0, "Paint");
        PathView path = RecsTestBed.Path(10, (painter.Id, 0, 8), (artist.Id, 8, 21));
        recs.Path(path);

        RecommendationPlan plan = recs.Plan(ArtPawn(12), ArtPawn(5));
        HashSet<int> artistAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> painterAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];

        // Slot 1 = strong direct Artist; slot 2 = the Painter trainee, never a
        // weak direct Artist.
        await Assert.That(artistAssignments.SetEquals([1])).IsTrue();
        await Assert.That(painterAssignments.SetEquals([2])).IsTrue();
    }

    private static PawnView ArtPawn(int artistic)
    {
        var pawn = new PawnView { CapableWorkTypes = { "Art" } };
        pawn.SkillLevels["Artistic"] = artistic;
        pawn.SignalBuckets["Artistic"] = SignalBucket.Neutral;
        return pawn;
    }

    [Test]
    public async Task ProtectedAssignmentOffsetsPublishedSelectionSlot()
    {
        RoleView role = CraftingRole(105, "TargetWork");
        RecsTestBed.Require(role, 2);
        PawnView protectedHolder = CraftingPawn(8, ("TargetWork", SignalBucket.Neutral));
        protectedHolder.Existing.Add(
            new AssignmentView
            {
                RoleId = role.Id,
                Enabled = true,
                Pinned = true,
            }
        );
        PawnView selected = CraftingPawn(12, ("TargetWork", SignalBucket.Neutral));
        ColonyView colony = RecsTestBed.Colony([role], protectedHolder, selected);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> protectedHolderAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> selectedAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];

        await Assert.That(protectedHolderAssignments.SetEquals([105])).IsTrue();
        await Assert.That(selectedAssignments.SetEquals([105])).IsTrue();
        await Assert.That(plan.TryGetExplanation(1, role.Id, out RoleRecommendationExplanation explanation)).IsTrue();
        await Assert.That(explanation.SelectionStage).IsEqualTo(RecommendationSelectionStage.Required);
        await Assert.That(explanation.SelectionSlot).IsEqualTo(2);
        await Assert.That(explanation.SelectionSlotCount).IsEqualTo(2);
    }

    [Test]
    public async Task TargetQualifiedWaiverSlotPublishesDirectTargetSelection()
    {
        // Through the real projection: single-skill Crafting target + trainee on
        // a path; a pawn already in the target band fills the waiver slot as a
        // direct target.
        var recs = new RecsProjection().WorkType("TargetWork", "Crafting", 100).WorkType("TraineeWork", "Crafting", 90);
        RecommendationRoleSource target = recs.RoleByWorkType(106, 1, 0, "TargetWork");
        RecommendationRoleSource trainee = recs.RoleByWorkType(107, 0, 0, "TraineeWork");
        recs.Path(RecsTestBed.Path(206, (trainee.Id, 0, 21), (target.Id, 8, 21)));
        var qualified = new PawnView { CapableWorkTypes = { "TargetWork", "TraineeWork" } };
        qualified.SkillLevels["Crafting"] = 20;
        qualified.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        RecommendationPlan plan = recs.Plan(qualified);
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];

        await Assert.That(assignments.SetEquals([106, 107])).IsTrue();
        await Assert.That(plan.TryGetExplanation(0, target.Id, out RoleRecommendationExplanation targetExplanation)).IsTrue();
        await Assert.That(targetExplanation.SelectionStage).IsEqualTo(RecommendationSelectionStage.Required);
        await Assert.That(targetExplanation.SelectionSlot).IsEqualTo(1);
        await Assert.That(targetExplanation.SelectionSlotCount).IsEqualTo(1);
        await Assert.That(plan.TryGetExplanation(0, trainee.Id, out RoleRecommendationExplanation traineeExplanation)).IsTrue();
        // The no-demand trainee also qualifies on its own merits: the published
        // story is its surplus selection, not the path ride-along, so no
        // controlling target is named.
        await Assert.That(traineeExplanation.SelectionStage).IsEqualTo(RecommendationSelectionStage.Surplus);
        await Assert.That(traineeExplanation.RelatedRoleId).IsEqualTo(-1);
    }

    [Test]
    public async Task UnfilledTrainingWaiverPublishesRankedDirectFallback()
    {
        RoleView target = CraftingRole(110, "TargetWork");
        RecsTestBed.Require(target, 2);
        RoleView trainee = CraftingRole(111, "TraineeWork");
        PathView path = RecsTestBed.Path(210, (trainee.Id, 0, 5), (target.Id, 10, 21));

        PawnView first = CraftingPawn(16, ("TargetWork", SignalBucket.Neutral), ("TraineeWork", SignalBucket.Neutral));
        PawnView fallback = CraftingPawn(7, ("TargetWork", SignalBucket.Neutral), ("TraineeWork", SignalBucket.Neutral));
        PawnView lowerRankedFallback = CraftingPawn(6, ("TargetWork", SignalBucket.Neutral), ("TraineeWork", SignalBucket.Neutral));
        ColonyView colony = RecsTestBed.Colony([target, trainee], first, fallback, lowerRankedFallback);
        colony.Paths.Add(path);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> firstAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> fallbackAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];
        HashSet<int> lowerRankedFallbackAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(2)).Select(index => plan.RoleAt(2, index))];

        await Assert.That(firstAssignments.SetEquals([110])).IsTrue();
        await Assert.That(fallbackAssignments.SetEquals([110])).IsTrue();
        await Assert.That(lowerRankedFallbackAssignments).IsEmpty();
        await Assert.That(plan.TryGetExplanation(1, target.Id, out RoleRecommendationExplanation explanation)).IsTrue();
        await Assert.That(explanation.SelectionStage).IsEqualTo(RecommendationSelectionStage.Required);
        await Assert.That(explanation.CandidateRank).IsEqualTo(2);
        await Assert.That(explanation.CandidateCount).IsEqualTo(3);
        await Assert.That(explanation.StageRank).IsEqualTo(1);
        await Assert.That(explanation.SelectionSlot).IsEqualTo(2);
        await Assert.That(explanation.SelectionSlotCount).IsEqualTo(2);
    }

    [Test]
    public async Task AllocatesConfiguredWantAndStrongSurplusWithoutAwfulPawns()
    {
        /*
         * Default role recommendation order: Doctor > Cook > Hauler.
         * Training paths: none.
         * Role scales: Doctor custom direct 1 + training 1; Cook custom direct 2;
         * Hauler custom direct 1 (legacy/unskilled and therefore out of scope).
         * Cook's earlier minimum-pick bonus places it before Doctor on the two
         * pawns selected for both roles.
         */
        RoleView doctor = RecsTestBed.Role(1, "Doctor");
        RecsTestBed.Require(doctor, 2);

        RoleView cook = RecsTestBed.Role(2, "Cooking");
        RecsTestBed.Require(cook, 2);

        RoleView hauler = RecsTestBed.Unskilled(3, "Hauling");

        PawnView forcedDoctor = PawnWith(medicine: (14, SignalBucket.Neutral), cooking: (2, SignalBucket.Poor));
        PawnView strongLead = PawnWith(medicine: (8, SignalBucket.Strong), cooking: (10, SignalBucket.Strong));
        PawnView strongDoctorAndForcedCook = PawnWith(medicine: (4, SignalBucket.Strong), cooking: (8, SignalBucket.Neutral));
        PawnView awfulDespiteSkill = PawnWith(medicine: (20, SignalBucket.Awful), cooking: (20, SignalBucket.Awful));

        ColonyView colony = RecsTestBed.Colony([doctor, cook, hauler], forcedDoctor, strongLead, strongDoctorAndForcedCook, awfulDespiteSkill);

        RecommendationPlan plan = RecommendationPlan.Build(colony);

        HashSet<int> forcedDoctorAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> strongLeadAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];
        HashSet<int> strongDoctorAndForcedCookAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(2)).Select(index => plan.RoleAt(2, index))];
        HashSet<int> awfulDespiteSkillAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(3)).Select(index => plan.RoleAt(3, index))];

        await Assert.That(forcedDoctorAssignments.SetEquals([1])).IsTrue();
        await Assert.That(strongLeadAssignments.SetEquals([1, 2])).IsTrue();
        await Assert.That(strongDoctorAndForcedCookAssignments.SetEquals([1, 2])).IsTrue();
        await Assert.That(awfulDespiteSkillAssignments).IsEmpty();
        await Assert.That(plan.PathCountAt(0)).IsEqualTo(0);
        await Assert.That(plan.PathCountAt(1)).IsEqualTo(0);
        await Assert.That(plan.PathCountAt(2)).IsEqualTo(0);
        await Assert.That(plan.PathCountAt(3)).IsEqualTo(0);
    }

    [Test]
    public async Task SkillLevelPromotionMakesMedicASurplusAssignmentWithinItsBand()
    {
        RoleView doctor = SkilledRole(4, "Doctor", "Medicine", "Rescue", "Tend", "Operate");
        RoleView medic = SkilledRole(5, "Doctor", "Medicine", "Rescue", "Tend");
        RoleView nurse = SkilledRole(6, "Doctor", "Medicine", "Rescue");
        PathView path = RecsTestBed.Path(7, (nurse.Id, 0, 5), (medic.Id, 5, 15), (doctor.Id, 15, 21));

        PawnView doctorPawn = MultiSkillPawn(new Dictionary<string, (int, SignalBucket)> { ["Medicine"] = (16, SignalBucket.Neutral) }, ("Doctor", SignalBucket.Neutral));
        PawnView medicPawn = MultiSkillPawn(new Dictionary<string, (int, SignalBucket)> { ["Medicine"] = (11, SignalBucket.Neutral) }, ("Doctor", SignalBucket.Neutral));
        ColonyView colony = RecsTestBed.Colony([doctor, medic, nurse], doctorPawn, medicPawn);
        colony.Paths.Add(path);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> doctorAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> medicAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];

        await Assert.That(doctorAssignments.SetEquals([4])).IsTrue();
        await Assert.That(medicAssignments.SetEquals([5])).IsTrue();
        await Assert.That(plan.TryGetExplanation(1, medic.Id, out RoleRecommendationExplanation explanation)).IsTrue();
        await Assert.That(explanation.SignalBucket).IsEqualTo(SignalBucket.Strong);
        await Assert.That(explanation.BaseSignalBucket).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(explanation.SignalSkillLevel).IsEqualTo(11);
        await Assert.That(explanation.SelectionStage).IsEqualTo(RecommendationSelectionStage.Surplus);
        await Assert.That(explanation.SelectionSignalBucket).IsEqualTo(SignalBucket.Strong);
        await Assert.That(explanation.SurplusMinimumSignalBucket).IsEqualTo(SignalBucket.Strong);
        await Assert.That(explanation.SurplusQualifiedBySignal).IsTrue();
    }

    [Test]
    public async Task EarlierCoveringPickSuppressesOnlyThatPawnsStrongSurplusRole()
    {
        RoleView cook = SkilledRole(8, "CookingAll", "Cooking", "Cook", "Brew");
        RoleView brewer = SkilledRole(9, "Brewing", "Cooking", "Brew");

        PawnView cookAndBrewer = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)> { ["Cooking"] = (10, SignalBucket.Strong) },
            ("CookingAll", SignalBucket.Neutral),
            ("Brewing", SignalBucket.Neutral)
        );
        PawnView brewerOnly = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)> { ["Cooking"] = (10, SignalBucket.Strong) },
            ("CookingAll", SignalBucket.Awful),
            ("Brewing", SignalBucket.Neutral)
        );
        ColonyView colony = RecsTestBed.Colony([cook, brewer], cookAndBrewer, brewerOnly);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> cookAndBrewerAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> brewerOnlyAssignments = [.. Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index))];

        await Assert.That(cookAndBrewerAssignments.SetEquals([8])).IsTrue();
        await Assert.That(brewerOnlyAssignments.SetEquals([9])).IsTrue();
    }

    [Test]
    public async Task UnderBandChampionHoldsTheTargetAndActivatesItsPath()
    {
        /*
         * Fabricator demand: colonyMin 1, a forced champion.
         * Training path: Fabricator = Tailor[0,8), Smith[8,15), Fabricator[15,21).
         * The only capable pawn sits under the target band: the champion still
         * holds Fabricator itself and trains toward it through the path.
         */
        RoleView fabricator = CraftingRole(10, "Fabrication");
        RecsTestBed.Require(fabricator, 1);
        RoleView smith = CraftingRole(12, "Smithing");
        RoleView tailor = CraftingRole(13, "Tailoring");
        PathView fabricatorPath = RecsTestBed.Path(20, (tailor.Id, 0, 8), (smith.Id, 8, 15), (fabricator.Id, 15, 21));

        PawnView champion = CraftingPawn(14, ("Fabrication", SignalBucket.Neutral), ("Smithing", SignalBucket.Neutral), ("Tailoring", SignalBucket.Neutral));

        ColonyView colony = RecsTestBed.Colony([fabricator, smith, tailor], champion);
        colony.Paths.Add(fabricatorPath);

        RecommendationPlan plan = RecommendationPlan.Build(colony, WithoutLevelPromotions());
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> paths = [.. Enumerable.Range(0, plan.PathCountAt(0)).Select(index => plan.PathAt(0, index))];

        await Assert.That(assignments.SetEquals([10])).IsTrue();
        await Assert.That(paths.SetEquals([20])).IsTrue();
    }

    [Test]
    public async Task SharedTraineeRungActivatesEachTargetsOwnPath()
    {
        /*
         * Fabricator and Drug Maker demand: coverage 50 with colonyMin 0, one
         * fully waivable slot each at this colony size, no forced champion.
         * Their training paths share the Tailor and Smith rungs below each
         * target: the mid-band trainee lands once on the shared Smith rung and
         * activates each target's own path.
         */
        RoleView fabricator = CraftingRole(10, "Fabrication");
        RecsTestBed.Require(fabricator, 0, 50);
        RoleView drugMaker = CraftingRole(11, "DrugMaking");
        RecsTestBed.Require(drugMaker, 0, 50);
        RoleView smith = CraftingRole(12, "Smithing");
        RoleView tailor = CraftingRole(13, "Tailoring");
        PathView fabricatorPath = RecsTestBed.Path(20, (tailor.Id, 0, 8), (smith.Id, 8, 15), (fabricator.Id, 15, 21));
        PathView drugMakerPath = RecsTestBed.Path(21, (tailor.Id, 0, 8), (smith.Id, 8, 15), (drugMaker.Id, 15, 21));

        PawnView trainee = CraftingPawn(10, ("Fabrication", SignalBucket.Neutral), ("DrugMaking", SignalBucket.Neutral), ("Smithing", SignalBucket.Neutral), ("Tailoring", SignalBucket.Neutral));

        ColonyView colony = RecsTestBed.Colony([fabricator, drugMaker, smith, tailor], trainee);
        colony.Paths.AddRange([fabricatorPath, drugMakerPath]);

        RecommendationPlan plan = RecommendationPlan.Build(colony, WithoutLevelPromotions());
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> paths = [.. Enumerable.Range(0, plan.PathCountAt(0)).Select(index => plan.PathAt(0, index))];

        await Assert.That(assignments.SetEquals([12])).IsTrue();
        await Assert.That(paths.SetEquals([20, 21])).IsTrue();
        await Assert.That(plan.PathActivatedAt(0, 0)).IsTrue();
        await Assert.That(plan.PathActivatedAt(0, 1)).IsTrue();
    }

    [Test]
    public async Task PawnBelowEverySkilledRungLandsOnTheLowestPathRole()
    {
        /*
         * Smith demand: coverage 50 with colonyMin 0, one fully waivable slot.
         * Training path: Smith = Tailor[0,8), Smith[8,21). The pawn below the
         * Smith rung lands on Tailor through the path.
         */
        RoleView smith = CraftingRole(12, "Smithing");
        RecsTestBed.Require(smith, 0, 50);
        RoleView tailor = CraftingRole(13, "Tailoring");
        PathView smithPath = RecsTestBed.Path(22, (tailor.Id, 0, 8), (smith.Id, 8, 21));

        PawnView trainee = CraftingPawn(4, ("Smithing", SignalBucket.Strong), ("Tailoring", SignalBucket.Neutral));
        trainee.SignalBuckets["Crafting"] = SignalBucket.Strong;

        ColonyView colony = RecsTestBed.Colony([smith, tailor], trainee);
        colony.Paths.Add(smithPath);

        RecommendationPlan plan = RecommendationPlan.Build(colony, WithoutLevelPromotions());
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> paths = [.. Enumerable.Range(0, plan.PathCountAt(0)).Select(index => plan.PathAt(0, index))];

        await Assert.That(assignments.SetEquals([13])).IsTrue();
        await Assert.That(paths.SetEquals([22])).IsTrue();
    }

    [Test]
    public async Task MalformedAlternativeDoesNotDisplaceAValidTrainingPath()
    {
        RoleView specialist = CraftingRole(24, "SpecialistWork");
        RecsTestBed.Require(specialist, 0, 50);
        RoleView trainee = CraftingRole(25, "TraineeWork");
        PathView valid = RecsTestBed.Path(26, (trainee.Id, 0, 10), (specialist.Id, 10, 21));
        var malformed = new PathView { Id = 27 };
        malformed.RoleIds.Add(specialist.Id);
        PawnView pawn = CraftingPawn(5, ("SpecialistWork", SignalBucket.Neutral), ("TraineeWork", SignalBucket.Neutral));
        ColonyView colony = RecsTestBed.Colony([specialist, trainee], pawn);
        colony.Paths.AddRange([valid, malformed]);

        RecommendationPlan plan = RecommendationPlan.Build(colony, WithoutLevelPromotions());
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];
        HashSet<int> paths = [.. Enumerable.Range(0, plan.PathCountAt(0)).Select(index => plan.PathAt(0, index))];

        await Assert.That(assignments.SetEquals([25])).IsTrue();
        await Assert.That(paths.SetEquals([26])).IsTrue();
    }

    [Test]
    public async Task DirectSpecialistSurvivesAndPrecedesItsCoveringTrainer()
    {
        /*
         * Default role recommendation order: Crafter > Fabricator.
         * Training paths: Fabricator = Crafter[0,21), Fabricator[8,21).
         * Role scales: Crafter direct 1; Fabricator direct 1.
         */
        RoleView crafter = SkilledRole(70, "CraftingWork", "Crafting", "Fabricate", "Stonecut");
        RecsTestBed.Require(crafter, 1);
        RoleView fabricator = SkilledRole(71, "Fabrication", "Crafting", "Fabricate");
        RecsTestBed.Require(fabricator, 1);
        PathView path = RecsTestBed.Path(72, (crafter.Id, 0, 21), (fabricator.Id, 8, 21));

        PawnView pawn = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)> { ["Crafting"] = (12, SignalBucket.Neutral) },
            ("CraftingWork", SignalBucket.Neutral),
            ("Fabrication", SignalBucket.Neutral)
        );
        ColonyView colony = RecsTestBed.Colony([crafter, fabricator], pawn);
        colony.Paths.Add(path);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        string actualAssignmentOrder = string.Join(",", Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index)));
        HashSet<int> paths = [.. Enumerable.Range(0, plan.PathCountAt(0)).Select(index => plan.PathAt(0, index))];

        await Assert.That(paths.SetEquals([72])).IsTrue();
        await Assert.That(actualAssignmentOrder).IsEqualTo("71,70");
    }

    [Test]
    public async Task OrdersConnectedTargetsWithoutLeadDiversification()
    {
        /*
         * Default role recommendation order: Tailor > Smith > Fabricator > Crafter.
         * Training paths: Tailor = Crafter[0,21), Tailor[2,21); Smith =
         * Crafter[0,21), Smith[4,21); Fabricator = Crafter[0,21), Smith[4,21),
         * Fabricator[8,21).
         * Role scales: all four roles are interest-only; Strong signal supplies
         * their surplus memberships.
         * With lead diversification disabled, ordinary target minima and
         * recommendation scores order every qualified pawn consistently. The
         * weak fourth pawn remains below Fabricator's target minimum.
         */
        RoleView tailor = CraftingRole(80, "Tailoring");
        RoleView smith = CraftingRole(81, "Smithing");
        RoleView fabricator = CraftingRole(82, "Fabrication");
        RoleView crafter = CraftingRole(83, "CraftingWork");
        PathView tailorPath = RecsTestBed.Path(90, (crafter.Id, 0, 21), (tailor.Id, 2, 21));
        PathView smithPath = RecsTestBed.Path(91, (crafter.Id, 0, 21), (smith.Id, 4, 21));
        PathView fabricatorPath = RecsTestBed.Path(92, (crafter.Id, 0, 21), (smith.Id, 4, 21), (fabricator.Id, 8, 21));

        PawnView best = QualifiedCrafter(15);
        PawnView second = QualifiedCrafter(14);
        PawnView third = QualifiedCrafter(13);
        PawnView weak = QualifiedCrafter(4);
        ColonyView colony = RecsTestBed.Colony([tailor, smith, fabricator, crafter], best, second, third, weak);
        colony.Paths.AddRange([tailorPath, smithPath, fabricatorPath]);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        string actualBestAssignmentOrder = string.Join(",", Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index)));
        string actualSecondAssignmentOrder = string.Join(",", Enumerable.Range(0, plan.RoleCountAt(1)).Select(index => plan.RoleAt(1, index)));
        string actualThirdAssignmentOrder = string.Join(",", Enumerable.Range(0, plan.RoleCountAt(2)).Select(index => plan.RoleAt(2, index)));
        string actualWeakAssignmentOrder = string.Join(",", Enumerable.Range(0, plan.RoleCountAt(3)).Select(index => plan.RoleAt(3, index)));
        string actualBestPathOrder = string.Join(",", Enumerable.Range(0, plan.PathCountAt(0)).Select(index => plan.PathAt(0, index)));
        string actualSecondPathOrder = string.Join(",", Enumerable.Range(0, plan.PathCountAt(1)).Select(index => plan.PathAt(1, index)));
        string actualThirdPathOrder = string.Join(",", Enumerable.Range(0, plan.PathCountAt(2)).Select(index => plan.PathAt(2, index)));
        string actualWeakPathOrder = string.Join(",", Enumerable.Range(0, plan.PathCountAt(3)).Select(index => plan.PathAt(3, index)));

        await Assert.That(actualBestAssignmentOrder).IsEqualTo("82,81,80,83");
        await Assert.That(actualSecondAssignmentOrder).IsEqualTo("82,81,80,83");
        await Assert.That(actualThirdAssignmentOrder).IsEqualTo("82,81,80,83");
        await Assert.That(actualWeakAssignmentOrder).IsEqualTo("81,80,83");
        await Assert.That(actualBestPathOrder).IsEqualTo("90,91,92");
        await Assert.That(actualSecondPathOrder).IsEqualTo("90,91,92");
        await Assert.That(actualThirdPathOrder).IsEqualTo("90,91,92");
        await Assert.That(actualWeakPathOrder).IsEqualTo("92,90,91");
    }

    [Test]
    public async Task PreservesAutomaticHunterFireAndPinnedWork()
    {
        /*
         * Default role recommendation order: Core > Doctor > Basics > Hunter >
         * Hauler > Fire Blocker.
         * Training paths: none.
         * Demand: Doctor colonyMin 1, pinned on the pawn; Core and Basics
         * automatic; Hunter uses its weapon/tier policy; Hauler is a
         * demand-less chore whose unpinned disabled assignment drops; Fire
         * Blocker is granted by fire fear.
         */
        RoleView core = RecsTestBed.Unskilled(100, "CoreWork", "DoctorJob", "CoreJob");
        core.AutoAssign = true;
        RoleView doctor = SkilledRole(101, "Doctoring", "Medicine", "DoctorJob");
        RecsTestBed.Require(doctor, 1);
        RoleView basics = RecsTestBed.Unskilled(102, "BasicWorker");
        basics.AutoAssign = true;
        RoleView hunter = SkilledRole(103, "Hunting", "Shooting");        RoleView hauler = RecsTestBed.Unskilled(104, "Hauling");
        RoleView fireBlocker = RecsTestBed.Unskilled(105, "Firefighting");
        fireBlocker.Blocker = true;

        PawnView pawn = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)> { ["Medicine"] = (12, SignalBucket.Neutral), ["Shooting"] = (12, SignalBucket.Neutral) },
            ("CoreWork", SignalBucket.Neutral),
            ("Doctoring", SignalBucket.Neutral),
            ("BasicWorker", SignalBucket.Neutral),
            ("Hunting", SignalBucket.Neutral),
            ("Hauling", SignalBucket.Neutral),
            ("Firefighting", SignalBucket.Neutral)
        );
        pawn.HasRangedWeapon = true;
        pawn.ShootingLevel = 12;
        pawn.FireFear = true;
        pawn.Existing.Add(new AssignmentView { RoleId = hauler.Id, Enabled = false });
        pawn.Existing.Add(
            new AssignmentView
            {
                RoleId = doctor.Id,
                Enabled = true,
                Pinned = true,
            }
        );
        ColonyView colony = RecsTestBed.Colony([core, doctor, basics, hunter, hauler, fireBlocker], pawn);
        colony.HunterRoleId = hunter.Id;
        colony.FireBlockerRoleId = fireBlocker.Id;

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        string actualAssignmentOrder = string.Join(",", Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index)));

        await Assert.That(plan.PathCountAt(0)).IsEqualTo(0);
        // The pinned Doctor's only existing neighbor (Hauler) does not survive,
        // so its anchor falls back to the ordinal position (existing index 1).
        await Assert.That(actualAssignmentOrder).IsEqualTo("105,101,100,102,103");
        await Assert.That(plan.TryGetExplanation(0, hunter.Id, out RoleRecommendationExplanation hunterExplanation)).IsTrue();
        await Assert.That(hunterExplanation.DemandApplies).IsFalse();
        await Assert.That(plan.TryGetExplanation(0, hauler.Id, out RoleRecommendationExplanation haulerExplanation)).IsTrue();
        await Assert.That(haulerExplanation.DemandApplies).IsFalse();
        await Assert.That(haulerExplanation.Recommended).IsFalse();
        await Assert.That(haulerExplanation.RejectReason).IsEqualTo(PickRejectReason.ScaleNever);
    }

    [Test]
    [Arguments(4, "200,204,205,201,202,203,206,207,208")]
    [Arguments(8, "200,204,201,202,203,206,205,207,208")]
    [Arguments(12, "200,204,201,202,205,203,206,207,208")]
    [Arguments(16, "200,204,201,202,203,206,205,207,208")]
    [Arguments(17, "200,204,201,202,203,206,207,205,208")]
    public async Task HunterTierPlacesHunterAtTheExpectedRolePosition(int shooting, string expectedAssignmentOrder)
    {
        /*
         * Default role recommendation order: Basics > Crafter > Cook > Miner >
         * Smith > Doctor > Grunt > Researcher; the Important+PartTime ordering
         * points bubble Doctor to the front of the skilled block, so the
         * published base order is Basics > Doctor > Crafter > Cook > Miner >
         * Smith > Grunt > Researcher. Hunter is unlisted, so its slot follows
         * the shooting tier over the colonist's own role list (cutoff defaults
         * 4/8/12/16): tier 1 after the leading non-normal block (auto Basics
         * plus Important Doctor), tier 2 additionally after the minimum and
         * champion picks up to the unskilled Grunt (every skilled role here is
         * a single-slot minimum pick), tier 3 after the first full-time normal
         * role (Cook; Crafter is part-time), tier 4 after the third (Smith,
         * coinciding with tier 2 in this fixture), and tier 5 last, ahead of
         * the preserve-order Researcher.
         */
        RoleView basics = RecsTestBed.Unskilled(200, "BasicWorker");
        basics.AutoAssign = true;
        RoleView crafter = SkilledRole(201, "Crafting", "Crafting");
        RecsTestBed.Require(crafter, 1);
        crafter.Category = RoleCategory.Normal;
        crafter.Time = RoleTime.PartTime;
        RoleView cook = SkilledRole(202, "Cooking", "Cooking");
        RecsTestBed.Require(cook, 1);
        cook.Category = RoleCategory.Normal;
        cook.Time = RoleTime.FullTime;
        RoleView miner = SkilledRole(203, "Mining", "Mining");
        RecsTestBed.Require(miner, 1);
        miner.Category = RoleCategory.Normal;
        miner.Time = RoleTime.FullTime;
        RoleView smith = SkilledRole(206, "Smithing", "Smithing");
        RecsTestBed.Require(smith, 1);
        smith.Category = RoleCategory.Normal;
        smith.Time = RoleTime.FullTime;
        RoleView doctor = SkilledRole(204, "Doctoring", "Medicine");
        RecsTestBed.Require(doctor, 1);
        doctor.Category = RoleCategory.Important;
        doctor.Time = RoleTime.PartTime;
        RoleView grunt = RecsTestBed.Unskilled(207, "Hauling");
        grunt.AutoAssign = true;
        RoleView researcher = SkilledRole(208, "Research", "Intellectual");
        RecsTestBed.Require(researcher, 1);
        researcher.PreserveRecommendationOrder = true;
        RoleView hunter = SkilledRole(205, "Hunting", "Shooting");
        PawnView pawn = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)>
            {
                ["Crafting"] = (10, SignalBucket.Neutral),
                ["Cooking"] = (10, SignalBucket.Neutral),
                ["Mining"] = (10, SignalBucket.Neutral),
                ["Smithing"] = (10, SignalBucket.Neutral),
                ["Medicine"] = (10, SignalBucket.Neutral),
                ["Intellectual"] = (10, SignalBucket.Neutral),
                ["Shooting"] = (shooting, SignalBucket.Neutral),
            },
            ("BasicWorker", SignalBucket.Neutral),
            ("Crafting", SignalBucket.Neutral),
            ("Cooking", SignalBucket.Neutral),
            ("Mining", SignalBucket.Neutral),
            ("Smithing", SignalBucket.Neutral),
            ("Doctoring", SignalBucket.Neutral),
            ("Research", SignalBucket.Neutral),
            ("Hauling", SignalBucket.Neutral),
            ("Hunting", SignalBucket.Neutral)
        );
        pawn.HasRangedWeapon = true;
        pawn.ShootingLevel = shooting;

        // A low shooter owns the first tier, so the main pawn's tier is not
        // promoted (the engine promotes the lowest shooter when no pawn lands
        // in the first tier naturally).
        PawnView lowShooter = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)> { ["Shooting"] = (3, SignalBucket.Neutral) },
            ("BasicWorker", SignalBucket.Neutral),
            ("Hunting", SignalBucket.Neutral)
        );
        lowShooter.HasRangedWeapon = true;
        lowShooter.ShootingLevel = 3;
        lowShooter.CapableWorkTypes.Clear();
        lowShooter.CapableWorkTypes.Add("BasicWorker");
        lowShooter.CapableWorkTypes.Add("Hunting");

        ColonyView colony = RecsTestBed.Colony([basics, crafter, cook, miner, smith, doctor, grunt, researcher, hunter], pawn, lowShooter);
        colony.HunterRoleId = hunter.Id;
        colony.OrderTemplate = [basics.Id, crafter.Id, cook.Id, miner.Id, smith.Id, doctor.Id, grunt.Id, researcher.Id];

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        string actualAssignmentOrder = string.Join(",", Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index)));

        await Assert.That(actualAssignmentOrder).IsEqualTo(expectedAssignmentOrder);
    }

    [Test]
    public async Task DoesNotAddHunterWhenFinalRoleAlreadyCoversIt()
    {
        /*
         * Default role recommendation order: Sharpshooter > Hunter.
         * Training paths: none.
         * Role scales: Sharpshooter custom direct 1; Hunter uses its existing
         * weapon/tier policy but is redundant when Sharpshooter is selected.
         */
        RoleView sharpshooter = SkilledRole(106, "Sharpshooting", "Shooting", "Hunting", "MarksmanWork");
        RecsTestBed.Require(sharpshooter, 1);
        RoleView hunter = SkilledRole(107, "Hunting", "Shooting", "Hunting");        PawnView pawn = MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)> { ["Shooting"] = (14, SignalBucket.Strong) },
            ("Sharpshooting", SignalBucket.Strong),
            ("Hunting", SignalBucket.Strong)
        );
        pawn.HasRangedWeapon = true;
        pawn.ShootingLevel = 14;
        ColonyView colony = RecsTestBed.Colony([sharpshooter, hunter], pawn);
        colony.HunterRoleId = hunter.Id;

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];

        await Assert.That(plan.PathCountAt(0)).IsEqualTo(0);
        await Assert.That(assignments.SetEquals([106])).IsTrue();
    }

    [Test]
    public async Task CompositeSubstitutesForAnInOrderMemberRunAndMergesExplanations()
    {
        // FH bundles Farmer then Handler. A pawn strong at both holds Farmer
        // and Handler; Plants outranks Animals so they publish in member order
        // and collapse into FH, whose explanation carries both members'.
        RoleView farmer = SkilledRole(1, "Growing", "Plants", "Grow");
        RecsTestBed.Require(farmer, 1);
        RoleView handler = SkilledRole(2, "Handling", "Animals", "Handle");
        RecsTestBed.Require(handler, 1);
        RoleView composite = RecsTestBed.Role(3, "Growing", "Grow", "Handle");
        composite.MemberRoleIds = [farmer.Id, handler.Id];

        Dictionary<string, (int, SignalBucket)> skills = new() { ["Plants"] = (20, SignalBucket.Strong), ["Animals"] = (4, SignalBucket.Strong) };
        PawnView pawn = MultiSkillPawn(skills, ("Growing", SignalBucket.Strong), ("Handling", SignalBucket.Strong));

        ColonyView colony = RecsTestBed.Colony([farmer, handler, composite], pawn);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];

        await Assert.That(assignments.SetEquals([3])).IsTrue();
        await Assert.That(plan.TryGetExplanation(0, composite.Id, out RoleRecommendationExplanation bundled)).IsTrue();
        await Assert.That(bundled.SpecialPickReason).IsEqualTo(SpecialPickReason.Bundled);
        string actualBundledMemberOrder = string.Join(",", bundled.BundledMembers.Select(member => member.RoleId));
        await Assert.That(actualBundledMemberOrder).IsEqualTo("1,2");
    }

    [Test]
    public async Task CompositeIsNotSubstitutedWhenPawnMissesItsExplicitSkillGate()
    {
        RoleView farmer = SkilledRole(1, "Growing", "Plants", "Grow");
        RecsTestBed.Require(farmer, 1);
        RoleView handler = SkilledRole(2, "Handling", "Animals", "Handle");
        RecsTestBed.Require(handler, 1);
        RoleView composite = RecsTestBed.Role(3, "Growing", "Grow", "Handle");
        composite.MemberRoleIds = [farmer.Id, handler.Id];
        RecsTestBed.SetGates(composite, "Medicine");

        Dictionary<string, (int, SignalBucket)> skills = new()
        {
            ["Plants"] = (20, SignalBucket.Strong),
            ["Animals"] = (4, SignalBucket.Strong),
        };
        PawnView pawn = MultiSkillPawn(
            skills,
            ("Growing", SignalBucket.Strong),
            ("Handling", SignalBucket.Strong));
        ColonyView colony = RecsTestBed.Colony(
            [farmer, handler, composite], pawn);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        string assignments = string.Join(",", Enumerable.Range(
            0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index)));

        await Assert.That(assignments).IsEqualTo("1,2");
    }

    [Test]
    public async Task CompositeIsNotSubstitutedWhenMembersReorder()
    {
        // Reversed skills publish Handler before Farmer, so collapsing FH would
        // reorder jobs, so the members remain separate.
        RoleView farmer = SkilledRole(1, "Growing", "Plants", "Grow");
        RecsTestBed.Require(farmer, 1);
        RoleView handler = SkilledRole(2, "Handling", "Animals", "Handle");
        RecsTestBed.Require(handler, 1);
        RoleView composite = RecsTestBed.Role(3, "Growing", "Grow", "Handle");
        composite.MemberRoleIds = [farmer.Id, handler.Id];
        Dictionary<string, (int, SignalBucket)> skills = new() { ["Plants"] = (4, SignalBucket.Strong), ["Animals"] = (20, SignalBucket.Strong) };
        PawnView pawn = MultiSkillPawn(skills, ("Growing", SignalBucket.Strong), ("Handling", SignalBucket.Strong));
        ColonyView colony = RecsTestBed.Colony([farmer, handler, composite], pawn);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        string actualAssignmentOrder = string.Join(",", Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index)));

        await Assert.That(actualAssignmentOrder).IsEqualTo("2,1");
    }

    [Test]
    public async Task CompositeIsNotSubstitutedWhenAMemberIsAbsent()
    {
        RoleView farmer = SkilledRole(1, "Growing", "Plants", "Grow");
        RecsTestBed.Require(farmer, 1);
        RoleView handler = SkilledRole(2, "Handling", "Animals", "Handle");
        RecsTestBed.Require(handler, 1);
        RoleView composite = RecsTestBed.Role(3, "Growing", "Grow", "Handle");
        composite.MemberRoleIds = [farmer.Id, handler.Id];
        PawnView farmsOnly = RecsTestBed.Pawn();
        farmsOnly.CapableWorkTypes.Add("Growing");
        farmsOnly.SkillLevels["Plants"] = 20;
        farmsOnly.SignalBuckets["Plants"] = SignalBucket.Strong;
        ColonyView colony = RecsTestBed.Colony([farmer, handler, composite], farmsOnly);

        RecommendationPlan plan = RecommendationPlan.Build(colony);
        HashSet<int> assignments = [.. Enumerable.Range(0, plan.RoleCountAt(0)).Select(index => plan.RoleAt(0, index))];

        await Assert.That(assignments.SetEquals([1])).IsTrue();
    }

    private static PawnView PawnWith((int Level, SignalBucket Verdict) medicine, (int Level, SignalBucket Verdict) cooking)
    {
        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Medicine"] = medicine.Level;
        pawn.SignalBuckets["Medicine"] = medicine.Verdict;
        pawn.SkillLevels["Cooking"] = cooking.Level;
        pawn.SignalBuckets["Cooking"] = cooking.Verdict;
        return pawn;
    }

    private static RoleView CraftingRole(int id, string workType) => SkilledRole(id, workType, "Crafting");

    private static RoleView SkilledRole(int id, string workType, string skill, params string[] coverage) =>
        RecsTestBed.Skilled(id, workType, skill, coverage);

    private static PawnView CraftingPawn(int level, params (string WorkType, SignalBucket Verdict)[] signals)
    {
        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = level;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Neutral;
        pawn.WorkTypeSignalBuckets = signals.ToDictionary(signal => signal.WorkType, signal => signal.Verdict);
        foreach ((string workType, _) in signals)
            pawn.CapableWorkTypes.Add(workType);
        return pawn;
    }

    private static PawnView RolePawn(int cooking, int? medicine, params (string WorkType, SignalBucket Verdict)[] signals)
    {
        PawnView pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Cooking"] = cooking;
        pawn.SignalBuckets["Cooking"] = SignalBucket.Neutral;
        if (medicine.HasValue)
        {
            pawn.SkillLevels["Medicine"] = medicine.Value;
            pawn.SignalBuckets["Medicine"] = SignalBucket.Neutral;
        }
        pawn.WorkTypeSignalBuckets = signals.ToDictionary(signal => signal.WorkType, signal => signal.Verdict);
        foreach ((string workType, _) in signals)
            pawn.CapableWorkTypes.Add(workType);
        return pawn;
    }

    private static PawnView MultiSkillPawn(IReadOnlyDictionary<string, (int Level, SignalBucket Verdict)> skills, params (string WorkType, SignalBucket Verdict)[] workTypes)
    {
        PawnView pawn = RecsTestBed.Pawn();
        foreach (KeyValuePair<string, (int Level, SignalBucket Verdict)> skill in skills)
        {
            pawn.SkillLevels[skill.Key] = skill.Value.Level;
            pawn.SignalBuckets[skill.Key] = skill.Value.Verdict;
        }
        pawn.WorkTypeSignalBuckets = workTypes.ToDictionary(workType => workType.WorkType, workType => workType.Verdict);
        foreach ((string workType, _) in workTypes)
            pawn.CapableWorkTypes.Add(workType);
        return pawn;
    }

    private static PawnView QualifiedCrafter(int level) =>
        MultiSkillPawn(
            new Dictionary<string, (int, SignalBucket)> { ["Crafting"] = (level, SignalBucket.Strong) },
            ("Tailoring", SignalBucket.Neutral),
            ("Smithing", SignalBucket.Neutral),
            ("Fabrication", SignalBucket.Neutral),
            ("CraftingWork", SignalBucket.Neutral)
        );

    private static RecommendationsTuningOptions WithoutLevelPromotions() =>
        RecommendationsTuningOptions.Default.With(RecommendationTuningOption.OptionalTargetGreatLevel, 21).With(RecommendationTuningOption.OptionalTargetStrongLevel, 21);
}
