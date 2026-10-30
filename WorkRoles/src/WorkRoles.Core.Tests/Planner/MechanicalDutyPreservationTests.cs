using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

public class MechanicalDutyPreservationTests
{
    [Test]
    public async Task NewOmniUsesItsAcceptedFirstDutyForCoverageUntilThePlayerReordersIt()
    {
        var recs = new RecsProjection()
            .WorkType("Firefighter", null, 1500, "FightFires")
            .WorkType("Patient", null, 1400, "PatientGoToBedTreatment")
            .WorkType("Doctor", null, 1300, "DoctorRescue")
            .WorkType("Construction", "Construction", 600,
                "ConstructFinishFrames", "Repair", "Deconstruct");
        // Shipped Core entries and default Builder demand. Rescue is unskilled;
        // Omni can do firefighting/rescue but cannot be a human patient.
        var core = recs.RoleByGiver(1, 0, 100,
            "FightFires", "PatientGoToBedTreatment", "DoctorRescue");
        core.TemplateDefName = "WS_Core";
        core.AutoAssign = true;
        core.Time = RoleTime.PartTime;
        var build = recs.RoleByWorkType(2, 2, 15, "Construction");
        build.TemplateDefName = "WS_Builder";
        build.Time = RoleTime.FullTime;
        recs.RoleByGiver(3, 0, 0, "Repair", "Deconstruct");
        recs.Path(new PathView { Id = 2, RoleIds = { 3, 2 },
            BandMins = { 0, 8 }, BandMaxes = { 8, 21 } });
        PawnView Human() => new()
        {
            SkillLevels = { ["Construction"] = 12 },
            CapableWorkTypes = { "Construction", "Firefighter", "Patient", "Doctor" },
        };
        ColonyView colony = recs.Colony(Human(), Human());
        colony.IntegrateMechanicalWorkers = true;
        var worker = new MechanicalWorkerView
        {
            Id = 42, ProvidesService = true,
            ExecutableGivers = { "FightFires", "DoctorRescue", "ConstructFinishFrames", "Repair", "Deconstruct" },
            Facts = new PawnView
            {
                IsMechanical = true,
                SkillLevels = { ["Construction"] = 20 },
                CapableWorkTypes = { "Construction", "Firefighter", "Doctor" },
            },
        };
        colony.MechanicalWorkers.Add(worker);
        int HumanBuilders(RecommendationPlan plan) => Enumerable.Range(0, plan.PawnCount)
            .Count(i => RecsProjection.Holds(plan, i, 2));
        RoleRecommendationExplanation[] BuilderReasons(RecommendationPlan plan)
        {
            var reasons = new List<RoleRecommendationExplanation>();
            for (int i = 0; i < plan.PawnCount; i++)
                if (plan.TryGetExplanation(i, 2, out RoleRecommendationExplanation reason))
                    reasons.Add(reason);
            return reasons.ToArray();
        }

        await Assert.That(HumanBuilders(RecommendationPlan.Build(colony))).IsEqualTo(2);
        PawnFixTarget target = MechanicalDutyPlan.BuildFixTarget(colony, worker);
        await Assert.That(string.Join(",", target.RoleIds)).IsEqualTo("1,2");
        foreach (int id in target.RoleIds)
        {
            worker.ActiveRoleIds.Add(id);
            worker.Facts.Existing.Add(new AssignmentView { RoleId = id });
        }
        worker.CommittedGivers.UnionWith(new[] {
            "FightFires", "DoctorRescue", "ConstructFinishFrames", "Repair", "Deconstruct" });
        // Core is the accepted first duty. The fallback Builder must not earn
        // a second independent staffing credit, despite executable build work.
        RecommendationPlan coreFirst = RecommendationPlan.Build(colony);
        await Assert.That(HumanBuilders(coreFirst)).IsEqualTo(2);
        foreach (RoleRecommendationExplanation reason in BuilderReasons(coreFirst))
            await Assert.That(reason.RequiredTotal).IsEqualTo(2);
        worker.ActiveRoleIds.Reverse();
        worker.Facts.Existing.Reverse();
        await Assert.That(string.Join(",", MechanicalDutyPlan.BuildFixTarget(colony, worker).RoleIds)).IsEqualTo("2,1");
        RecommendationPlan builderFirst = RecommendationPlan.Build(colony);
        RoleRecommendationExplanation[] coveredReasons = BuilderReasons(builderFirst);
        await Assert.That(coveredReasons.Length).IsEqualTo(2);
        foreach (RoleRecommendationExplanation reason in coveredReasons)
            await Assert.That(reason.RequiredTotal).IsEqualTo(1);
        // At skill 12 the second human still earns an optional Builder role.
        // Coverage reduces required staffing; it does not suppress good surplus.
        await Assert.That(HumanBuilders(builderFirst)).IsEqualTo(2);
        await Assert.That(coveredReasons.Count(r => r.SelectionStage == RecommendationSelectionStage.Required)).IsEqualTo(1);
        await Assert.That(coveredReasons.Count(r => r.SelectionStage == RecommendationSelectionStage.Surplus)).IsEqualTo(1);

        // Skill 8 is inside Builder's band but below the default skill-10
        // surplus promotion. Here the same coverage also releases a holder.
        foreach (PawnView human in colony.Pawns) human.SkillLevels["Construction"] = 8;
        await Assert.That(HumanBuilders(RecommendationPlan.Build(colony))).IsEqualTo(1);
        worker.ActiveRoleIds.Reverse();
        worker.Facts.Existing.Reverse();
        await Assert.That(HumanBuilders(RecommendationPlan.Build(colony))).IsEqualTo(2);
    }

    [Test]
    public async Task MechanicalPreviewExplainsUsefulWorkAndNativeLimitsWithoutClaimingHumanDemandOrTraining()
    {
        var recs = new RecsProjection()
            .WorkType("Construction", "Construction", 400, "Construct")
            .WorkType("Cleaning", null, 100, "Clean")
            .WorkType("Research", "Intellectual", 300, "Research");
        recs.RoleByWorkType(1, 2, 0, "Construction");
        recs.RoleByWorkType(2, 1, 0, "Cleaning");
        recs.RoleByWorkType(3, 1, 0, "Research");
        ColonyView colony = recs.Colony(new PawnView());
        var worker = new MechanicalWorkerView
        {
            Id = 42, ProvidesService = true,
            ExecutableGivers = { "Construct", "Clean" },
            CommittedGivers = { "Construct" }, ActiveRoleIds = { 1 },
            Facts = new PawnView
            {
                IsMechanical = true,
                SkillLevels = { ["Construction"] = 20, ["Intellectual"] = 20 },
                SignalBuckets = { ["Construction"] = SignalBucket.Great, ["Intellectual"] = SignalBucket.Great },
                CapableWorkTypes = { "Construction", "Cleaning" },
                Existing = { new() { RoleId = 1 }, new() { RoleId = 3 } },
            },
        };
        var reasons = new Dictionary<int, RoleRecommendationExplanation>();
        var verdicts = new Dictionary<int, SignalBucket>();

        PawnFixTarget target = MechanicalDutyPlan.BuildFixTarget(colony, worker, reasons, verdicts);

        await Assert.That(string.Join(",", target.RoleIds)).IsEqualTo("1,2");
        await Assert.That(verdicts[1]).IsEqualTo(SignalBucket.Great);
        // Research is rejected as incapable, but its badge still rates the
        // robot's Intellectual skill like a human's would.
        await Assert.That(verdicts[3]).IsEqualTo(SignalBucket.Great);
        await Assert.That(reasons[1].SpecialPickReason).IsEqualTo(SpecialPickReason.MechanicalPrimary);
        await Assert.That(reasons[2].Recommended).IsTrue();
        await Assert.That(reasons[2].SpecialPickReason).IsEqualTo(SpecialPickReason.MechanicalWork);
        await Assert.That(reasons[3].Recommended).IsFalse();
        await Assert.That(reasons[3].RejectReason).IsEqualTo(PickRejectReason.Incapable);
        await Assert.That(reasons[3].SignalBucket).IsEqualTo(SignalBucket.Great);
        await Assert.That(reasons[1].RequiredSkills).Contains("Construction");
        foreach (RoleRecommendationExplanation reason in reasons.Values)
        {
            await Assert.That(reason.DemandApplies).IsFalse();
            await Assert.That(reason.TrainingSkills.Count).IsEqualTo(0);
            await Assert.That(reason.LearningProtectedByMechanicalCoverage).IsFalse();
        }

        worker.Facts.Incapacitated = true;
        worker.ProvidesService = false;
        target = MechanicalDutyPlan.BuildFixTarget(colony, worker, reasons, verdicts);
        await Assert.That(string.Join(",", target.RoleIds)).IsEqualTo("1,3");
        await Assert.That(target.Changed).IsFalse();
        foreach (RoleRecommendationExplanation reason in reasons.Values)
            await Assert.That(reason.SpecialPickReason).IsEqualTo(SpecialPickReason.MechanicalIncapacitated);
    }

    [Test]
    public async Task FixKeepsDownedWorkersDutiesInTheirExistingOrder()
    {
        var recs = new RecsProjection()
            .WorkType("Construction", "Construction", 400, "Construct")
            .WorkType("Cleaning", null, 100, "Clean")
            .WorkType("Firefighter", null, 1000, "FightFire");
        recs.RoleByWorkType(1, 1, 0, "Construction");
        recs.RoleByWorkType(2, 1, 0, "Cleaning");
        recs.RoleByWorkType(3, 0, 0, "Firefighter");
        recs.AutoAssign(3);
        ColonyView colony = recs.Colony(new PawnView());
        colony.Roles.Add(new RoleView { Id = 4, HasRules = true });
        colony.Roles.Add(new RoleView { Id = 5, Blocker = true });
        colony.Roles.Add(new RoleView { Id = 6, ExcludedFromRecommendations = true });
        colony.Roles.Add(new RoleView { Id = 7, ExcludedFromRecommendations = true });
        var worker = new MechanicalWorkerView
        {
            Id = 42, ProvidesService = false,
            ExecutableGivers = { "Construct", "Clean", "FightFire" },
            CommittedGivers = { "Construct", "Clean", "FightFire" },
            ActiveRoleIds = { 1, 3, 2 },
            Facts = new PawnView
            {
                Incapacitated = true,
                SkillLevels = { ["Construction"] = 20 },
                CapableWorkTypes = { "Construction", "Cleaning", "Firefighter" },
                Existing = { new() { RoleId = 4 }, new() { RoleId = 1 },
                    new() { RoleId = 5 }, new() { RoleId = 3 },
                    new() { RoleId = 2 }, new() { RoleId = 6, Pinned = true } },
            },
        };

        PawnFixTarget target = MechanicalDutyPlan.BuildFixTarget(colony, worker);

        // Incapacitation must not erase fallback or emergency work that will
        // become usable on recovery, or reorder manual protections.
        await Assert.That(string.Join(",", target.RoleIds)).IsEqualTo("4,1,5,3,2,6");
        await Assert.That(target.Changed).IsFalse();

        worker.Facts.Existing.Add(new AssignmentView { RoleId = 7 });
        target = MechanicalDutyPlan.BuildFixTarget(colony, worker);
        await Assert.That(string.Join(",", target.RoleIds)).IsEqualTo("4,1,5,3,2,6");
        await Assert.That(target.Changed).IsTrue();
    }

    [Test]
    public async Task FixKeepsAndAddsSupportedAutomaticEmergencyWorkWithoutAddingImpossibleDuties()
    {
        var recs = new RecsProjection()
            .WorkType("Construction", "Construction", 400, "Construct")
            .WorkType("Firefighter", null, 1000, "FightFire")
            .WorkType("Patient", null, 900, "Patient");
        recs.RoleByWorkType(1, 1, 0, "Construction");
        // The shipped Core duty mixes supported firefighting with human-only
        // patient work. Its unskilled partial work must still remain usable.
        recs.RoleByWorkType(2, 0, 0, "Firefighter", "Patient");
        recs.AutoAssign(2);
        recs.RoleByWorkType(3, 0, 0, "Patient");
        recs.AutoAssign(3);
        ColonyView colony = recs.Colony(new PawnView());
        var worker = new MechanicalWorkerView
        {
            Id = 42, ProvidesService = true,
            ExecutableGivers = { "Construct", "FightFire" },
            CommittedGivers = { "Construct", "FightFire" },
            ActiveRoleIds = { 1, 2 },
            Facts = new PawnView
            {
                SkillLevels = { ["Construction"] = 20 },
                CapableWorkTypes = { "Construction", "Firefighter" },
                Existing = { new() { RoleId = 1 }, new() { RoleId = 2 } },
            },
        };

        PawnFixTarget target = MechanicalDutyPlan.BuildFixTarget(colony, worker);
        await Assert.That(string.Join(",", target.RoleIds)).IsEqualTo("1,2");
        await Assert.That(target.Changed).IsFalse();

        worker.Facts.Existing.RemoveAt(1);
        worker.ActiveRoleIds.Remove(2);
        worker.CommittedGivers.Remove("FightFire");
        await Assert.That(string.Join(",", MechanicalDutyPlan.BuildFixTarget(colony, worker).RoleIds)).IsEqualTo("1,2");
    }
}
