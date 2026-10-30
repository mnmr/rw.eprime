using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// Chip verdict markers must show the same per-(pawn, role) suitability the
/// planner ranks candidates with, so the map is asserted at this boundary.
public class RoleSuitabilityTests
{
    [Test]
    public async Task HumanBadgesRetainPreMechanicalRatingsWhenWorkIsUnavailable()
    {
        // These are the pre-feature player-visible ratings: human aptitude is
        // separate from the assigned chip's incapable-work warning.
        RoleView haul = RecsTestBed.Unskilled(1, "Hauling", "HaulGeneral");
        RoleView sow = RecsTestBed.Skilled(2, "Growing", "Plants", "GrowerSow");
        var human = new PawnView { CapableWorkTypes = { "Growing" } };
        human.SkillLevels["Plants"] = 12;
        human.SignalBuckets["Plants"] = SignalBucket.Great;
        human.WorkEvidence = PawnWorkEvidence.Create([
            new WorkCapabilityFact("GrowerSow", null, "ludeon.rimworld", "WR_WorkBlockedCapacity")], []);

        Dictionary<int, SignalBucket> verdicts =
            RoleSuitability.Verdicts(RecsTestBed.Colony([haul, sow], human))[0];

        await Assert.That(verdicts[haul.Id]).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(verdicts[sow.Id]).IsEqualTo(SignalBucket.Great);
    }

    // A robot whose native work excludes a role's jobs keeps the same skill
    // verdict a human would get; the assigned chip's capability marker, not
    // the stars, reports the unavailable work (owner decision 2026-10-30).
    [Test]
    public async Task MechanicalBadgesRateSkillFitEvenWhenNativeWorkExcludesTheJobs()
    {
        RoleView farm = RecsTestBed.Skilled(1, "Growing", "Plants", "Sow");
        RoleView haul = RecsTestBed.Unskilled(2, "Hauling", "Haul");
        RoleView clean = RecsTestBed.Unskilled(3, "Cleaning", "Clean");
        RoleView mixed = RecsTestBed.Unskilled(4, "Cleaning", "Clean", "Haul");
        RecsTestBed.SetSpec(mixed, RecsTestBed.Capability("Cleaning", 0, RecsTestBed.Giver("Clean")),
            RecsTestBed.Capability("Hauling", 0, RecsTestBed.Giver("Haul")));
        RoleView composite = RecsTestBed.Unskilled(5, "Hauling", "Haul", "Sow");
        RecsTestBed.SetSpec(composite, farm.WorkSpec.Capabilities[0], haul.WorkSpec.Capabilities[0]);
        composite.MemberRoleIds = [farm.Id, haul.Id];
        var cleaner = new PawnView { IsMechanical = true, CapableWorkTypes = { "Growing", "Hauling", "Cleaning" } };
        cleaner.SkillLevels["Plants"] = 20;
        cleaner.SignalBuckets["Plants"] = SignalBucket.Exceptional;
        cleaner.WorkEvidence = PawnWorkEvidence.Create([
            new WorkCapabilityFact("Sow", null, "robot", "unsupported"),
            new WorkCapabilityFact("Haul", null, "robot", "unsupported")], []);
        var colony = RecsTestBed.Colony([farm, haul, clean, mixed, composite], cleaner);

        Dictionary<int, SignalBucket> verdicts = RoleSuitability.Verdicts(colony)[0];

        await Assert.That(verdicts[farm.Id]).IsEqualTo(SignalBucket.Exceptional);
        await Assert.That(verdicts[haul.Id]).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(verdicts[composite.Id]).IsEqualTo(SignalBucket.Strong);
        await Assert.That(verdicts[clean.Id]).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(verdicts[mixed.Id]).IsEqualTo(SignalBucket.Neutral);
    }

    [Test]
    public async Task EmptyRoleSkillProfileDoesNotFallBackToParentWorkTypeSkills()
    {
        var role = RecsTestBed.Unskilled(1, "Doctor", "Rescue");
        var pawn = new PawnView { CapableWorkTypes = { "Doctor" } };
        pawn.SkillLevels["Medicine"] = 18;
        pawn.SignalBuckets["Medicine"] = SignalBucket.Exceptional;
        ColonyView colony = RecsTestBed.Colony([role], pawn);

        List<Dictionary<int, SignalBucket>> verdicts =
            RoleSuitability.Verdicts(colony);

        await Assert.That(verdicts[0][role.Id])
            .IsEqualTo(SignalBucket.Neutral);
    }

    [Test]
    public async Task VerdictsMatchTheEngineSignalPerPawnAndRole()
    {
        RoleView cook = RecsTestBed.Role(1, "Cooking");
        RoleView doctor = RecsTestBed.Role(2, "Doctor");
        RoleView hauler = RecsTestBed.Unskilled(3, "Hauling");

        PawnView chef = RecsTestBed.Pawn();
        chef.SkillLevels["Cooking"] = 12;
        chef.SignalBuckets["Cooking"] = SignalBucket.Great;

        PawnView medic = RecsTestBed.Pawn();
        medic.SkillLevels["Cooking"] = 3;
        medic.SkillLevels["Medicine"] = 15;
        medic.SignalBuckets["Medicine"] = SignalBucket.Exceptional;

        ColonyView colony = RecsTestBed.Colony([cook, doctor, hauler], chef, medic);

        List<Dictionary<int, SignalBucket>> verdicts = RoleSuitability.Verdicts(colony);

        await Assert.That(verdicts.Count).IsEqualTo(2);
        // Chef: strong cook, has never touched Medicine (missing required
        // primary skill reads Awful, exactly as the planner rejects it).
        await Assert.That(verdicts[0][cook.Id]).IsEqualTo(SignalBucket.Great);
        await Assert.That(verdicts[0][doctor.Id]).IsEqualTo(SignalBucket.Awful);
        // Medic: unclassified Cooking skill stays Neutral; the skill-less
        // hauling role has no signal for anyone.
        await Assert.That(verdicts[1][cook.Id]).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(verdicts[1][doctor.Id]).IsEqualTo(SignalBucket.Exceptional);
        await Assert.That(verdicts[0][hauler.Id]).IsEqualTo(SignalBucket.Neutral);
        await Assert.That(verdicts[1][hauler.Id]).IsEqualTo(SignalBucket.Neutral);
    }

    [Test]
    public async Task CompositeVerdictIsTheMemberAverageRoundedDown()
    {
        RoleView farmer = RecsTestBed.Role(1, "Crafting");
        RoleView doctor = RecsTestBed.Role(2, "Doctor");
        RoleView composite = RecsTestBed.Role(3, "Crafting");
        composite.MemberRoleIds = [farmer.Id, doctor.Id];
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = 15;
        pawn.SignalBuckets["Crafting"] = SignalBucket.Exceptional;
        pawn.SkillLevels["Medicine"] = 3;
        pawn.SignalBuckets["Medicine"] = SignalBucket.Poor;
        ColonyView colony = RecsTestBed.Colony(
            [farmer, doctor, composite], pawn);

        List<Dictionary<int, SignalBucket>> verdicts =
            RoleSuitability.Verdicts(colony);

        await Assert.That(verdicts[0][composite.Id])
            .IsEqualTo(SignalBucket.Strong);
    }
}
