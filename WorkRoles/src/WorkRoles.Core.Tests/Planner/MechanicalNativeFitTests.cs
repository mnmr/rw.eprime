using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

public class MechanicalNativeFitTests
{
    // The same native work must survive a better role label, including when
    // that work already supplies human staffing. The real projection retains
    // partial Basics/Grunt, automatic roles and non-template specialist roles.
    [Test]
    public async Task SpecialistsGetUsefulExactRolesWithoutOverridingPinsOrExclusions()
    {
        var recs = new RecsProjection()
            .WorkType("BasicWorker", null, 1150, "Flick")
            .WorkType("PatientBedRest", null, 1200, "Recuperate")
            .WorkType("Hauling", null, 200, "HaulGeneral", "HaulMerge", "Refuel")
            .WorkType("Cleaning", null, 100, "CleanFilth", "ClearSnow", "ClearPollution");
        var basics = recs.RoleByWorkType(1, 0, 100, "PatientBedRest", "BasicWorker");
        basics.TemplateDefName = "WS_Basics"; basics.AutoAssign = true;
        var grunt = recs.RoleByWorkType(2, 0, 100, "Hauling", "Cleaning");
        grunt.TemplateDefName = "WS_Grunt"; grunt.AutoAssign = true;
        recs.RoleByWorkType(3, 0, 0, "BasicWorker").TemplateDefName = "WS_Laborer";
        recs.RoleByWorkType(4, 1, 0, "Hauling").TemplateDefName = "WS_Hauler";
        recs.RoleByWorkType(5, 0, 0, "Cleaning").TemplateDefName = "WS_Cleaner";
        var human = new PawnView { CapableWorkTypes = { "BasicWorker", "Hauling", "Cleaning", "PatientBedRest" } };
        ColonyView colony = recs.Colony(human);
        colony.OrderTemplate = OrderTemplate.DeriveTemplate(colony.Roles);
        var cleaner = Worker(["Flick", "CleanFilth", "ClearSnow", "ClearPollution"], ["BasicWorker", "Cleaning"]);
        var hauler = Worker(["HaulGeneral", "HaulMerge", "Refuel"], ["Hauling"]);
        var omni = Worker(["HaulGeneral", "HaulMerge", "Refuel", "CleanFilth", "ClearSnow", "ClearPollution"], ["Hauling", "Cleaning"]);

        await Assert.That(Ids(MechanicalDutyPlan.BuildFixTarget(colony, cleaner))).IsEqualTo("3,5");
        await Assert.That(Ids(MechanicalDutyPlan.BuildFixTarget(colony, hauler))).IsEqualTo("4");
        await Assert.That(Ids(MechanicalDutyPlan.BuildFixTarget(colony, omni))).IsEqualTo("2");
        hauler.ActiveRoleIds.Add(2); hauler.CommittedGivers.UnionWith(hauler.ExecutableGivers);
        hauler.Facts.Existing.Add(new AssignmentView { RoleId = 2, Enabled = true });
        hauler.ProvidesService = true;
        colony.IntegrateMechanicalWorkers = true; colony.MechanicalWorkers.Add(hauler);
        string beforeHumans = HumanRoles(RecommendationPlan.Build(colony));
        await Assert.That(Ids(MechanicalDutyPlan.BuildFixTarget(colony, hauler))).IsEqualTo("4");
        hauler.ActiveRoleIds[0] = 4;
        hauler.Facts.Existing[0] = new AssignmentView { RoleId = 4, Enabled = true };
        await Assert.That(HumanRoles(RecommendationPlan.Build(colony))).IsEqualTo(beforeHumans);

        cleaner.ActiveRoleIds.AddRange([1, 2]); cleaner.CommittedGivers.UnionWith(cleaner.ExecutableGivers);
        cleaner.Facts.Existing.AddRange([new() { RoleId = 1, Enabled = true }, new() { RoleId = 2, Enabled = true, Pinned = true }]);
        await Assert.That(Ids(MechanicalDutyPlan.BuildFixTarget(colony, cleaner))).IsEqualTo("3,2");
        cleaner.Facts.Existing.Clear(); cleaner.ActiveRoleIds.Clear(); cleaner.CommittedGivers.Clear();
        colony.Roles.Single(r => r.Id == 5).ExcludedFromRecommendations = true;
        await Assert.That(Ids(MechanicalDutyPlan.BuildFixTarget(colony, cleaner))).IsEqualTo("3,2");
        colony.Roles.Single(r => r.Id == 3).ExcludedFromRecommendations = true;
        await Assert.That(Ids(MechanicalDutyPlan.BuildFixTarget(colony, cleaner))).IsEqualTo("1,2");
    }

    [Test]
    public async Task NativeMiningAndGrowingRemainUsefulWithoutPretendingUnsupportedTasksAreCovered()
    {
        var recs = new RecsProjection()
            .WorkType("Mining", "Mining", 300, "Mine", "Drill")
            .WorkType("Growing", "Plants", 600, "GrowerSow", "GrowerHarvest", "Replant", "ExtractTree", "PruneGauranlenTree")
            .WorkType("PlantCutting", "Plants", 500, "PlantsCut");
        recs.RoleByWorkType(1, 2, 20, "Mining").TemplateDefName = "WS_Miner";
        recs.RoleByWorkType(2, 2, 20, "Growing", "PlantCutting").TemplateDefName = "WS_Farmer";
        recs.RoleByWorkType(3, 0, 0, "Growing").TemplateDefName = "WS_Grower";
        recs.RoleByWorkType(4, 0, 0, "PlantCutting").TemplateDefName = "WS_PlantCutter";
        recs.RoleByWorkType(5, 0, 0, "Growing", "Mining");
        recs.Path(new PathView { Id = 2, RoleIds = { 4, 3, 2 }, BandMins = { 0, 4, 12 }, BandMaxes = { 12, 12, 21 } });
        PawnView HumanMiner() => new() { CapableWorkTypes = { "Mining" }, SkillLevels = { ["Mining"] = 8 } };
        ColonyView colony = recs.Colony(HumanMiner(), HumanMiner());
        colony.OrderTemplate = OrderTemplate.DeriveTemplate(colony.Roles);
        var tunneler = Worker(["Mine"], ["Mining"]); tunneler.Facts.SkillLevels["Mining"] = 10;
        tunneler.Facts.WorkEvidence = PawnWorkEvidence.Create([new("Drill", null, "native", "unsupported")], []);
        var agrihand = Worker(["GrowerSow", "GrowerHarvest", "PlantsCut"], ["Growing", "PlantCutting"]);
        agrihand.Facts.SkillLevels["Plants"] = 10;
        agrihand.Facts.WorkEvidence = PawnWorkEvidence.Create([
            new("Replant", null, "native", "unsupported"), new("ExtractTree", null, "native", "unsupported"),
            new("PruneGauranlenTree", null, "native", "unsupported")], []);

        await Assert.That(Ids(MechanicalDutyPlan.BuildFixTarget(colony, tunneler))).IsEqualTo("1");
        // Fixed native skills never train, so the Farmer band (12+) does not
        // demote the Plants-10 agrihand to the Grower/Plant Cutter steps.
        await Assert.That(Ids(MechanicalDutyPlan.BuildFixTarget(colony, agrihand))).IsEqualTo("2");
        tunneler.ActiveRoleIds.Add(1); tunneler.CommittedGivers.Add("Mine"); tunneler.ProvidesService = true;
        colony.IntegrateMechanicalWorkers = true; colony.MechanicalWorkers.Add(tunneler);
        RecommendationPlan plan = RecommendationPlan.Build(colony);
        // Both human miners are still needed: the native mining-only mech
        // cannot satisfy the role's drilling service, despite holding Miner.
        await Assert.That(RecsProjection.Holds(plan, 0, 1)).IsTrue();
        await Assert.That(RecsProjection.Holds(plan, 1, 1)).IsTrue();
    }

    [Test]
    public async Task NativeHuntingWorkDoesNotRecommendHuntingWithoutAnEquippedRangedWeapon()
    {
        var recs = new RecsProjection().WorkType("Hunting", "Shooting", 700, "HunterHunt");
        recs.RoleByWorkType(1, 0, 0, "Hunting").TemplateDefName = "WS_Hunter";
        ColonyView colony = recs.Colony(); colony.OrderTemplate = [1];
        var worker = Worker(["HunterHunt"], ["Hunting"]); worker.Facts.SkillLevels["Shooting"] = 8;
        await Assert.That(Ids(MechanicalDutyPlan.BuildFixTarget(colony, worker))).IsEqualTo("");
        worker.Facts.HasRangedWeapon = true;
        await Assert.That(Ids(MechanicalDutyPlan.BuildFixTarget(colony, worker))).IsEqualTo("1");
        worker.ActiveRoleIds.Add(1); worker.CommittedGivers.Add("HunterHunt");
        worker.Facts.Existing.Add(new AssignmentView { RoleId = 1, Enabled = true });
        worker.Facts.HasRangedWeapon = false;
        await Assert.That(Ids(MechanicalDutyPlan.BuildFixTarget(colony, worker))).IsEqualTo("");
    }

    [Test]
    public async Task NewWorkersUseNormalDutyOrderWhileExistingPrimaryWorkKeepsItsPriority()
    {
        var recs = new RecsProjection()
            .WorkType("Construction", "Construction", 600, "Repair", "Construct")
            .WorkType("Mining", "Mining", 300, "Mine");
        recs.RoleByWorkType(1, 2, 15, "Construction").TemplateDefName = "WS_Builder";
        recs.RoleByWorkType(2, 2, 20, "Mining").TemplateDefName = "WS_Miner";
        recs.RoleByGiver(3, 0, 0, "Repair").TemplateDefName = "WS_Handyman";
        recs.Path(new PathView { Id = 1, RoleIds = { 3, 1 }, BandMins = { 0, 8 }, BandMaxes = { 8, 21 } });
        ColonyView colony = recs.Colony(); colony.OrderTemplate = OrderTemplate.DeriveTemplate(colony.Roles);
        var worker = Worker(["Repair", "Construct", "Mine"], ["Construction", "Mining"]);
        worker.Facts.SkillLevels["Construction"] = 4; worker.Facts.SkillLevels["Mining"] = 4;
        // The Builder band (8+) gates human trainees, not a Construction-4 robot
        // that natively constructs: it keeps Builder rather than Handyman only.
        await Assert.That(Ids(MechanicalDutyPlan.BuildFixTarget(colony, worker))).IsEqualTo("1,2");
        worker.ActiveRoleIds.Add(2); worker.CommittedGivers.Add("Mine");
        worker.Facts.Existing.Add(new AssignmentView { RoleId = 2, Enabled = true });
        await Assert.That(Ids(MechanicalDutyPlan.BuildFixTarget(colony, worker))).IsEqualTo("2,1");
    }

    private static MechanicalWorkerView Worker(string[] givers, string[] workTypes) => new()
    {
        ExecutableGivers = new(givers), Facts = new PawnView { IsMechanical = true, CapableWorkTypes = new(workTypes) }
    };
    private static string Ids(PawnFixTarget target) => string.Join(",", target.RoleIds);
    private static string HumanRoles(RecommendationPlan plan) => string.Join("|", Enumerable.Range(0, plan.PawnCount)
        .Select(p => string.Join(",", Enumerable.Range(0, plan.RoleCountAt(p)).Select(i => plan.RoleAt(p, i)))));
}
