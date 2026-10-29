using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

public class MechanicalWorkerScenarioTests
{
    [Test]
    public async Task CoverageRefreshKeepsHumanFactsAndListedVerdictsIndependent()
    {
        var recs = new RecsProjection().WorkType("Crafting", "Crafting", 400, "Craft");
        recs.RoleByWorkType(1, 1, 0, "Crafting");
        var human = new PawnView { SkillLevels = { ["Crafting"] = 12 }, CapableWorkTypes = { "Crafting" } };
        MechanicalWorkerView Worker(bool service) => new()
        {
            Id = 42, ProvidesService = service, ExecutableGivers = { "Craft" },
            CommittedGivers = { "Craft" }, ActiveRoleIds = { 1 },
            Facts = new PawnView { SkillLevels = { ["Crafting"] = 20 }, CapableWorkTypes = { "Crafting" } }
        };
        object owner = new(), catalog = new(), humanId = new(), robotId = new();
        object[] humans = [humanId], listed = [humanId, robotId];
        var cache = new RecommendationSnapshotCache<object>();
        int builds = 0;
        IReadOnlyList<PawnView>? reusedHumans = null;
        RecommendationSnapshot Read(MechanicalWorkerView? worker) => cache.For(owner, humans, catalog, 0, 0, 0,
            RecommendationsTuningOptions.Default, (_, previous) =>
            {
                builds++;
                reusedHumans = previous;
                ColonyView colony = recs.Colony(human);
                if (worker != null) { colony.IntegrateMechanicalWorkers = true; colony.MechanicalWorkers.Add(worker); }
                return colony;
            }, worker);
        RecommendationSnapshot Verdicts() => cache.For(owner, listed, catalog, 0, 0, 0,
            RecommendationsTuningOptions.Default, (_, _) => { builds++; return recs.Colony(human, Worker(false).Facts); });

        RecommendationSnapshot baseline = Read(null);
        await Assert.That(RecsProjection.Holds(baseline.Plan, 0, 1)).IsTrue();
        MechanicalWorkerView supply = Worker(true);
        RecommendationSnapshot covered = Read(supply);
        await Assert.That(RecsProjection.Holds(covered.Plan, 0, 1)).IsFalse();
        await Assert.That(ReferenceEquals(reusedHumans, baseline.Colony.Pawns)).IsTrue();
        var verdicts = Verdicts().Suitability;
        int before = builds;
        for (int i = 0; i < 5; i++)
        {
            await Assert.That(ReferenceEquals(covered, Read(supply))).IsTrue();
            await Assert.That(ReferenceEquals(verdicts, Verdicts().Suitability)).IsTrue();
        }
        await Assert.That(builds).IsEqualTo(before);
        await Assert.That(ReferenceEquals(covered, Read(Worker(true)))).IsTrue();
        await Assert.That(RecsProjection.Holds(Read(Worker(false)).Plan, 0, 1)).IsTrue();
        await Assert.That(ReferenceEquals(verdicts, Verdicts().Suitability)).IsTrue();
    }

    [Test]
    public async Task AcceptedExcludedDutyStillSuppliesOtherRecommendedServices()
    {
        var recs = new RecsProjection().WorkType("Cleaning", null, 100, "Clean");
        recs.RoleByWorkType(1, 0, 0, "Cleaning");
        recs.RoleByWorkType(2, 1, 0, "Cleaning");
        ColonyView colony = recs.Colony(new PawnView { CapableWorkTypes = { "Cleaning" } });
        colony.Roles[0].ExcludedFromRecommendations = true;
        colony.IntegrateMechanicalWorkers = true;
        colony.MechanicalWorkers.Add(new MechanicalWorkerView { Id = 42, ProvidesService = true,
            ExecutableGivers = { "Clean" }, CommittedGivers = { "Clean" }, ActiveRoleIds = { 1 },
            Facts = new PawnView { CapableWorkTypes = { "Cleaning" } } });
        await Assert.That(RecsProjection.Holds(RecommendationPlan.Build(colony), 0, 2)).IsFalse();
        await Assert.That(RecsProjection.Holds(RecommendationPlan.Build(colony), 0, 1)).IsFalse();
        colony.MechanicalWorkers[0].Facts.Existing.Add(new AssignmentView { RoleId = 1 });
        await Assert.That(string.Join(",", MechanicalDutyPlan.BuildFixTarget(colony, colony.MechanicalWorkers[0]).RoleIds)).IsEqualTo("1");
        colony.Roles[0].HasRules = true;
        colony.MechanicalWorkers[0].ActiveRoleIds.Add(2);
        await Assert.That(RecsProjection.Holds(RecommendationPlan.Build(colony), 0, 2)).IsTrue();
    }

    [Test]
    public async Task ApplyingMechanicalSuggestionsKeepsPrimaryDutyPinsConditionsAndBlockers()
    {
        var recs = new RecsProjection().WorkType("Crafting", "Crafting", 400, "Craft")
            .WorkType("Cleaning", null, 100, "Clean");
        recs.RoleByWorkType(1, 1, 0, "Crafting");
        recs.RoleByWorkType(2, 1, 0, "Cleaning");
        ColonyView colony = recs.Colony(new PawnView());
        colony.Roles.Add(new RoleView { Id = 3, HasRules = true });
        colony.Roles.Add(new RoleView { Id = 4, Blocker = true });
        colony.Roles.Add(new RoleView { Id = 5, ExcludedFromRecommendations = true });
        var robot = new MechanicalWorkerView
        {
            Id = 42, ExecutableGivers = { "Craft", "Clean" }, ActiveRoleIds = { 2, 1 },
            Facts = new PawnView { SkillLevels = { ["Crafting"] = 8 }, CapableWorkTypes = { "Crafting", "Cleaning" },
                Existing = { new() { RoleId = 3 }, new() { RoleId = 2 }, new() { RoleId = 4 },
                    new() { RoleId = 1 }, new() { RoleId = 5, Pinned = true } } }
        };
        PawnFixTarget target = MechanicalDutyPlan.BuildFixTarget(colony, robot);
        await Assert.That(string.Join(",", target.RoleIds)).IsEqualTo("3,2,4,1,5");
        await Assert.That(target.Changed).IsFalse();
    }

    [Test]
    public async Task NativeWorkTypeWithoutRecipeSkillOrContentPermissionCannotReplaceQualifiedHuman()
    {
        var recs = new RecsProjection().WorkType("Crafting", "Crafting", 400, "Craft");
        recs.RoleByWorkType(1, 1, 0, "Crafting");
        var human = new PawnView { SkillLevels = { ["Crafting"] = 20 }, CapableWorkTypes = { "Crafting" } };
        ColonyView colony = recs.Colony(human);
        RecsTestBed.SetSpec(colony.Roles[0], RecsTestBed.Capability("Crafting", 400,
            RecsTestBed.Giver("Craft", ["Crafting"], ["Crafting"], ("Crafting", 14))));
        var robot = new MechanicalWorkerView
        {
            Id = 42, ProvidesService = true, ExecutableGivers = { "Craft" }, CommittedGivers = { "Craft" }, ActiveRoleIds = { 1 },
            Facts = new PawnView { SkillLevels = { ["Crafting"] = 8 }, CapableWorkTypes = { "Crafting" } }
        };
        colony.MechanicalWorkers.Add(robot);
        colony.IntegrateMechanicalWorkers = true;
        await Assert.That(RecsProjection.Holds(RecommendationPlan.Build(colony), 0, 1)).IsTrue();
        robot.Facts.SkillLevels["Crafting"] = 14;
        await Assert.That(RecsProjection.Holds(RecommendationPlan.Build(colony), 0, 1)).IsFalse();
        robot.Facts.WorkEvidence = PawnWorkEvidence.Create([new("Craft", "Craft_Crafting14", "test", "denied recipe")], []);
        await Assert.That(RecsProjection.Holds(RecommendationPlan.Build(colony), 0, 1)).IsTrue();
    }

    [Test]
    public async Task BlockedWorkAndFinishingOnlyPermissionCannotRemoveHumanPreparation()
    {
        var recs = new RecsProjection().WorkType("Crafting", "Crafting", 400, "Craft");
        recs.RoleByWorkType(1, 1, 0, "Crafting");
        var human = new PawnView { SkillLevels = { ["Crafting"] = 12 }, CapableWorkTypes = { "Crafting" } };
        ColonyView colony = recs.Colony(human);
        colony.IntegrateMechanicalWorkers = true;
        var robot = new MechanicalWorkerView
        {
            Id = 42, ProvidesService = true, ExecutableGivers = { "Craft" }, CommittedGivers = { "Craft" }, ActiveRoleIds = { 1 },
            Facts = new PawnView { SkillLevels = { ["Crafting"] = 20 }, CapableWorkTypes = { "Crafting" } }
        };
        colony.MechanicalWorkers.Add(robot);
        robot.CommittedGivers.Clear();
        // A blocker vetoed the giver: capability alone is no commitment.
        await Assert.That(RecsProjection.Holds(RecommendationPlan.Build(colony), 0, 1)).IsTrue();
        robot.CommittedGivers.Add("Craft");
        robot.PartialServiceGivers.Add("Craft");
        await Assert.That(RecsProjection.Holds(RecommendationPlan.Build(colony), 0, 1)).IsTrue();
        await Assert.That(string.Join(",", MechanicalDutyPlan.Recommend(colony, robot))).IsEqualTo("1");
        robot.PartialServiceGivers.Clear();
        await Assert.That(RecsProjection.Holds(RecommendationPlan.Build(colony), 0, 1)).IsFalse();
    }

    [Test]
    public async Task CoverageChangesRefreshCachedPlansButEqualRefreshesAndHumanSuitabilityAreReused()
    {
        var recs = new RecsProjection().WorkType("Crafting", "Crafting", 400, "Craft");
        recs.RoleByWorkType(1, 1, 0, "Crafting");
        var human = new PawnView { SkillLevels = { ["Crafting"] = 12 }, CapableWorkTypes = { "Crafting" } };
        var cache = new RecommendationSnapshotCache<object>();
        object owner = new(), catalog = new();
        object[] pawns = [new()];
        RecommendationSnapshot Read(int revision, bool service) => cache.For(owner, pawns, catalog, 0, revision, 0,
            RecommendationsTuningOptions.Default, (_, _) =>
            {
                ColonyView colony = recs.Colony(human);
                colony.IntegrateMechanicalWorkers = true;
                colony.MechanicalWorkers.Add(new MechanicalWorkerView
                {
                    Id = 42, ProvidesService = service, ExecutableGivers = { "Craft" }, CommittedGivers = { "Craft" }, ActiveRoleIds = { 1 },
                    Facts = new PawnView { SkillLevels = { ["Crafting"] = 20 }, CapableWorkTypes = { "Crafting" } }
                });
                return colony;
            });
        RecommendationSnapshot absent = Read(0, false);
        var suitability = absent.Suitability;
        await Assert.That(RecsProjection.Holds(absent.Plan, 0, 1)).IsTrue();
        RecommendationSnapshot covered = Read(1, true);
        await Assert.That(RecsProjection.Holds(covered.Plan, 0, 1)).IsFalse();
        await Assert.That(ReferenceEquals(suitability, covered.Suitability)).IsTrue();
        await Assert.That(ReferenceEquals(covered, Read(2, true))).IsTrue();
        await Assert.That(RecsProjection.Holds(Read(3, false).Plan, 0, 1)).IsTrue();
    }

    [Test]
    public async Task ReplacementKeepsSelectedLearningAndReleasesItWhenTheTargetIsReached()
    {
        var recs = new RecsProjection().WorkType("Crafting", "Crafting", 400, "Craft", "Train");
        recs.RoleByGiver(1, 2, 0, "Craft");
        recs.RoleByGiver(2, 0, 0, "Train");
        recs.Path(new PathView { Id = 1, RoleIds = { 2, 1 }, BandMins = { 0, 15 }, BandMaxes = { 15, 21 } });
        var master = new PawnView { SkillLevels = { ["Crafting"] = 20 }, CapableWorkTypes = { "Crafting" } };
        var learner = new PawnView { SkillLevels = { ["Crafting"] = 8 }, CapableWorkTypes = { "Crafting" }, SignalBuckets = { ["Crafting"] = SignalBucket.Great } };
        ColonyView colony = recs.Colony(master, learner);
        RecommendationPlan baseline = RecommendationPlan.Build(colony);
        await Assert.That(RecsProjection.Holds(baseline, 1, 2)).IsTrue();
        await Assert.That(baseline.PathCountAt(1)).IsEqualTo(1);
        for (int id = 10; id < 12; id++) colony.MechanicalWorkers.Add(new MechanicalWorkerView
        {
            Id = id, ProvidesService = true, ExecutableGivers = { "Craft", "Train" }, CommittedGivers = { "Craft", "Train" }, ActiveRoleIds = { 1, 2 },
            Facts = new PawnView { SkillLevels = { ["Crafting"] = 20 }, CapableWorkTypes = { "Crafting" } }
        });
        colony.IntegrateMechanicalWorkers = true;
        RecommendationPlan covered = RecommendationPlan.Build(colony);
        await Assert.That(RecsProjection.Holds(covered, 0, 1)).IsFalse();
        await Assert.That(RecsProjection.Holds(covered, 1, 2)).IsTrue();
        await Assert.That(covered.PathCountAt(1)).IsEqualTo(1);
        await Assert.That(covered.PathActivatedAt(1, 0)).IsTrue();
        await Assert.That(covered.TryGetExplanation(1, 2, out var learning)).IsTrue();
        await Assert.That(learning.SelectionStage).IsEqualTo(RecommendationSelectionStage.TrainingWaiver);
        await Assert.That(learning.TrainingSkills.Count).IsEqualTo(1);
        await Assert.That(learning.TrainingSkills[0].PawnLevel).IsEqualTo(8);
        await Assert.That(learning.TrainingSkills[0].TargetMinimum).IsEqualTo(15);
        learner.SkillLevels["Crafting"] = 15;
        RecommendationPlan completed = RecommendationPlan.Build(colony);
        await Assert.That(RecsProjection.Holds(completed, 1, 1)).IsFalse();
        await Assert.That(RecsProjection.Holds(completed, 1, 2)).IsFalse();
    }

    [Test]
    public async Task CoveredServiceCanReplaceTheLastHumanWithoutCountingRobotAsAnotherColonist()
    {
        var recs = new RecsProjection().WorkType("Crafting", "Crafting", 400, "Craft");
        recs.RoleByWorkType(1, 1, 0, "Crafting");
        var human = new PawnView { SkillLevels = { ["Crafting"] = 12 }, CapableWorkTypes = { "Crafting" } };
        ColonyView colony = recs.Colony(human);
        colony.MechanicalWorkers.Add(new MechanicalWorkerView
        {
            Id = 42, ProvidesService = true, ExecutableGivers = { "Craft" }, CommittedGivers = { "Craft" }, ActiveRoleIds = { 1 },
            Facts = new PawnView { SkillLevels = { ["Crafting"] = 20 }, CapableWorkTypes = { "Crafting" } }
        });
        await Assert.That(RecsProjection.Holds(RecommendationPlan.Build(colony), 0, 1)).IsTrue();
        colony.IntegrateMechanicalWorkers = true;
        RecommendationPlan integrated = RecommendationPlan.Build(colony);
        await Assert.That(integrated.PawnCount).IsEqualTo(1);
        await Assert.That(RecsProjection.Holds(integrated, 0, 1)).IsFalse();
        colony.MechanicalWorkers[0].ProvidesService = false;
        await Assert.That(RecsProjection.Holds(RecommendationPlan.Build(colony), 0, 1)).IsTrue();
    }

    [Test]
    public async Task MechanicalSuggestionsUseExecutableWorkAndNeverPromiseTrainingOrDuplicateCoverage()
    {
        var recs = new RecsProjection()
            .WorkType("Crafting", "Crafting", 400, "Craft")
            .WorkType("Cleaning", null, 100, "Clean");
        recs.RoleByWorkType(1, 1, 0, "Crafting");
        recs.RoleByWorkType(2, 1, 0, "Cleaning");
        var human = new PawnView { SkillLevels = { ["Crafting"] = 12 }, CapableWorkTypes = { "Crafting", "Cleaning" } };
        ColonyView colony = recs.Colony(human);
        var robot = new MechanicalWorkerView
        {
            Id = 42, ProvidesService = true, ExecutableGivers = { "Craft", "Clean" }, CommittedGivers = { "Craft", "Clean" }, ActiveRoleIds = { 1, 2 },
            Facts = new PawnView { SkillLevels = { ["Crafting"] = 8 }, CapableWorkTypes = { "Crafting", "Cleaning" } }
        };
        await Assert.That(string.Join(",", MechanicalDutyPlan.Recommend(colony, robot))).IsEqualTo("1,2");
        colony.MechanicalWorkers.Add(robot);
        colony.IntegrateMechanicalWorkers = true;
        RecommendationPlan integrated = RecommendationPlan.Build(colony);
        await Assert.That(RecsProjection.Holds(integrated, 0, 1)).IsFalse();
        await Assert.That(RecsProjection.Holds(integrated, 0, 2)).IsTrue();
        robot.ExecutableGivers.Remove("Craft");
        await Assert.That(string.Join(",", MechanicalDutyPlan.Recommend(colony, robot))).IsEqualTo("2");
    }
}
