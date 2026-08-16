using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// Chip verdict markers must show the same per-(pawn, role) suitability the
/// planner ranks candidates with, so the map is asserted at this boundary.
public class RoleSuitabilityTests
{
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
