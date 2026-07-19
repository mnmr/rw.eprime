using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

public class RecsTrainingWaiverTests
{
    private static (ColonyView colony, RoleView nurse, RoleView medic, RoleView doctor)
        DoctorColony(int waivers, params int[] levels)
    {
        var nurse = RecsTestBed.Role(1, "Doctor", "Nurse");
        var medic = RecsTestBed.Role(2, "Doctor", "Medic");
        var doctor = RecsTestBed.Role(3, "Doctor", "Doctor");
        doctor.HolderMode = RoleHolderMode.Custom;
        doctor.MinHolders = 3;
        doctor.TrainingWaivers = waivers;
        var pawns = levels.Select(level =>
        {
            var pawn = RecsTestBed.Pawn();
            pawn.SkillLevels["Medicine"] = level;
            return pawn;
        }).ToArray();
        var colony = RecsTestBed.Colony(
            new List<RoleView> { nurse, medic, doctor }, pawns);
        colony.Paths.Add(RecsTestBed.Path(1,
            (nurse.Id, 0, 5), (medic.Id, 5, 15), (doctor.Id, 15, 21)));
        return (colony, nurse, medic, doctor);
    }

    [Test]
    public async Task TwoWaiversKeepBothSelectedMedicBandPawnsInTraining()
    {
        var (colony, _, medic, doctor) = DoctorColony(2, 7, 11, 15);

        List<PawnResult> results = RecsEngine.Run(colony);

        await Assert.That(RecsTestBed.Ids(results[0])).IsEqualTo(medic.Id.ToString());
        await Assert.That(RecsTestBed.Ids(results[1])).IsEqualTo(medic.Id.ToString());
        await Assert.That(RecsTestBed.Ids(results[2])).IsEqualTo(doctor.Id.ToString());
    }

    [Test]
    public async Task PromotionsTakeHighestSkillAndWaiversTakeHighestRemainingPawns()
    {
        var (colony, nurse, medic, doctor) = DoctorColony(2, 3, 7, 11);

        List<PawnResult> results = RecsEngine.Run(colony);

        await Assert.That(RecsTestBed.Ids(results[0])).IsEqualTo(nurse.Id.ToString());
        await Assert.That(RecsTestBed.Ids(results[1])).IsEqualTo(medic.Id.ToString());
        await Assert.That(RecsTestBed.Ids(results[2])).IsEqualTo(doctor.Id.ToString());
    }

    [Test]
    public async Task OneWaiverPromotesTheTwoClosestPawns()
    {
        var (colony, nurse, _, doctor) = DoctorColony(1, 3, 7, 11);

        List<PawnResult> results = RecsEngine.Run(colony);

        await Assert.That(RecsTestBed.Ids(results[0])).IsEqualTo(nurse.Id.ToString());
        await Assert.That(RecsTestBed.Ids(results[1])).IsEqualTo(doctor.Id.ToString());
        await Assert.That(RecsTestBed.Ids(results[2])).IsEqualTo(doctor.Id.ToString());
    }

    [Test]
    public async Task InboundTrainingAssignmentsAreAdditiveToTheTrainingRolesMinimum()
    {
        var researcher = RecsTestBed.Role(1, "Doctor", "Research");
        researcher.HolderMode = RoleHolderMode.Custom;
        researcher.MinHolders = 2;
        var target = RecsTestBed.Role(2, "Doctor", "DrugMaking");
        target.HolderMode = RoleHolderMode.Custom;
        target.MinHolders = 1;
        target.TrainingWaivers = 1;
        var low = RecsTestBed.Pawn(); low.SkillLevels["Medicine"] = 3;
        var middle = RecsTestBed.Pawn(); middle.SkillLevels["Medicine"] = 4;
        var trainee = RecsTestBed.Pawn(); trainee.SkillLevels["Medicine"] = 5;
        var colony = RecsTestBed.Colony(
            new List<RoleView> { researcher, target }, low, middle, trainee);
        colony.Paths.Add(RecsTestBed.Path(1,
            (researcher.Id, 0, 12), (target.Id, 12, 21)));

        List<PawnResult> results = RecsEngine.Run(colony);

        await Assert.That(results.Count(r =>
            r.Assignments.Any(a => a.RoleId == researcher.Id))).IsEqualTo(3);
        await Assert.That(results.Count(r =>
            r.Assignments.Any(a => a.RoleId == target.Id))).IsEqualTo(0);
    }

    [Test]
    public async Task MultiSkillPathAssignsEveryMatchingRoleAndOrdersEqualBandsByWeakestSkill()
    {
        var crafter = RecsTestBed.Role(1, "Crafting", "Craft");
        crafter.Skills.Add(new RoleSkillView
            { SkillDefName = "Crafting", Primary = true, Importance = 2 });
        var researcher = RecsTestBed.Role(2, "Doctor", "Research");
        researcher.Skills.Add(new RoleSkillView
            { SkillDefName = "Intellectual", Primary = true, Importance = 2 });
        var target = RecsTestBed.Role(3, "Crafting", "DrugMaking");
        target.Skills.Add(new RoleSkillView
            { SkillDefName = "Crafting", Primary = true, Importance = 2 });
        target.Skills.Add(new RoleSkillView
            { SkillDefName = "Intellectual", Importance = 1 });
        target.HolderMode = RoleHolderMode.Custom;
        target.MinHolders = 1;
        target.TrainingWaivers = 1;
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = 10;
        pawn.SkillLevels["Intellectual"] = 8;
        var colony = RecsTestBed.Colony(
            new List<RoleView> { crafter, researcher, target }, pawn);
        var path = RecsTestBed.Path(1,
            (crafter.Id, 0, 12), (researcher.Id, 0, 12), (target.Id, 12, 21));
        path.AnchorRoleId = target.Id;
        path.AnchorBefore = true;
        colony.Paths.Add(path);

        PawnResult result = RecsEngine.Run(colony)[0];

        await Assert.That(RecsTestBed.Ids(result)).IsEqualTo("2,1");
        await Assert.That(result.Reasons[crafter.Id].TowardRoleId).IsEqualTo(target.Id);
        await Assert.That(result.Reasons[researcher.Id].TowardRoleId).IsEqualTo(target.Id);
    }

    [Test]
    public async Task PinnedLowerBandAssignmentIsNotReclassifiedAsTraining()
    {
        var medic = RecsTestBed.Role(1, "Doctor", "Medic");
        var doctor = RecsTestBed.Role(2, "Doctor", "Doctor");
        doctor.HolderMode = RoleHolderMode.Custom;
        doctor.MinHolders = 1;
        doctor.TrainingWaivers = 1;
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Medicine"] = 8;
        pawn.Existing.Add(new AssignmentView
            { RoleId = medic.Id, Enabled = true, Pinned = true });
        var colony = RecsTestBed.Colony(
            new List<RoleView> { medic, doctor }, pawn);
        colony.Paths.Add(RecsTestBed.Path(1,
            (medic.Id, 5, 15), (doctor.Id, 15, 21)));

        PawnResult result = RecsEngine.Run(colony)[0];

        await Assert.That(result.Assignments.Any(a => a.RoleId == medic.Id)).IsTrue();
        await Assert.That(result.Assignments.Any(a => a.RoleId == doctor.Id)).IsTrue();
        await Assert.That(result.Reasons.ContainsKey(medic.Id)).IsFalse();
    }

    [Test]
    public async Task TargetChoosesAPathWhoseLowerRolesCoverEveryRequiredSkill()
    {
        var crafter = RecsTestBed.Role(1, "Crafting", "Craft");
        crafter.Skills.Add(new RoleSkillView
            { SkillDefName = "Crafting", Primary = true });
        var researcher = RecsTestBed.Role(2, "Doctor", "Research");
        researcher.Skills.Add(new RoleSkillView
            { SkillDefName = "Intellectual", Primary = true });
        var target = RecsTestBed.Role(3, "Crafting", "Target");
        target.Skills.Add(new RoleSkillView
            { SkillDefName = "Intellectual", Primary = true });
        target.HolderMode = RoleHolderMode.Custom;
        target.MinHolders = 1;
        target.TrainingWaivers = 1;
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Crafting"] = 5;
        pawn.SkillLevels["Intellectual"] = 5;
        var colony = RecsTestBed.Colony(
            new List<RoleView> { crafter, researcher, target }, pawn);
        colony.Paths.Add(RecsTestBed.Path(1,
            (crafter.Id, 0, 10), (target.Id, 10, 21)));
        colony.Paths.Add(RecsTestBed.Path(2,
            (researcher.Id, 0, 10), (target.Id, 10, 21)));

        PawnResult result = RecsEngine.Run(colony)[0];

        await Assert.That(RecsTestBed.Ids(result)).IsEqualTo(researcher.Id.ToString());
        await Assert.That(result.Reasons[researcher.Id].TowardRoleId).IsEqualTo(target.Id);
    }
}
