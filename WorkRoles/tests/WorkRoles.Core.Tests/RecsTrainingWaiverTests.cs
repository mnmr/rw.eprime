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

    [Test]
    public async Task WaiverRoleSharedByPathsUsesTheSelectedTargetsPathPlacement()
    {
        var core = RecsTestBed.Role(1, "Cooking", "Core");
        core.AutoAssign = true;
        var selectedTarget = RecsTestBed.Role(2, "Doctor", "SelectedTarget");
        selectedTarget.HolderMode = RoleHolderMode.Custom;
        selectedTarget.MinHolders = 1;
        selectedTarget.TrainingWaivers = 1;
        var basics = RecsTestBed.Role(3, "Cooking", "Basics");
        basics.AutoAssign = true;
        var otherTarget = RecsTestBed.Role(4, "Doctor", "OtherTarget");
        var shared = RecsTestBed.Role(5, "Doctor", "Shared");
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Medicine"] = 5;
        var colony = RecsTestBed.Colony(
            new List<RoleView> { core, selectedTarget, basics, otherTarget, shared }, pawn);
        colony.OrderTemplate = new List<int>
            { core.Id, selectedTarget.Id, basics.Id, otherTarget.Id, shared.Id };
        var selectedPath = RecsTestBed.Path(1,
            (shared.Id, 0, 10), (selectedTarget.Id, 10, 21));
        selectedPath.AnchorRoleId = core.Id;
        selectedPath.AnchorBefore = false;
        var unrelatedPath = RecsTestBed.Path(2,
            (shared.Id, 0, 10), (otherTarget.Id, 10, 21));
        unrelatedPath.AnchorRoleId = otherTarget.Id;
        unrelatedPath.AnchorBefore = false;
        colony.Paths.Add(selectedPath);
        colony.Paths.Add(unrelatedPath);

        PawnResult result = RecsEngine.Run(colony)[0];

        await Assert.That(RecsTestBed.Ids(result)).IsEqualTo("1,5,3");
        await Assert.That(result.Reasons[shared.Id].TowardRoleId)
            .IsEqualTo(selectedTarget.Id);
    }

    [Test]
    public async Task UnanchoredWaiverPathUsesTargetsConfiguredPosition()
    {
        var core = RecsTestBed.Role(1, "Cooking", "Core");
        core.AutoAssign = true;
        var target = RecsTestBed.Role(2, "Doctor", "Target");
        target.HolderMode = RoleHolderMode.Custom;
        target.MinHolders = 1;
        target.TrainingWaivers = 1;
        var basics = RecsTestBed.Role(3, "Cooking", "Basics");
        basics.AutoAssign = true;
        var trainee = RecsTestBed.Role(4, "Doctor", "Trainee");
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Medicine"] = 5;
        var colony = RecsTestBed.Colony(
            new List<RoleView> { core, target, basics, trainee }, pawn);
        colony.OrderTemplate = new List<int>
            { core.Id, target.Id, basics.Id, trainee.Id };
        colony.Paths.Add(RecsTestBed.Path(1,
            (trainee.Id, 0, 10), (target.Id, 10, 21)));

        PawnResult result = RecsEngine.Run(colony)[0];

        await Assert.That(RecsTestBed.Ids(result)).IsEqualTo("1,4,3");
    }

    [Test]
    public async Task DirectTargetSelectedThroughAPathUsesThatPathsPlacement()
    {
        var core = RecsTestBed.Role(1, "Cooking", "Core");
        core.AutoAssign = true;
        var basics = RecsTestBed.Role(2, "Cooking", "Basics");
        basics.AutoAssign = true;
        var target = RecsTestBed.Role(3, "Doctor", "Target");
        target.HolderMode = RoleHolderMode.Custom;
        target.MinHolders = 1;
        target.TrainingWaivers = 1;
        var firstLower = RecsTestBed.Role(4, "Doctor", "FirstLower");
        var secondLower = RecsTestBed.Role(5, "Doctor", "SecondLower");
        var pawn = RecsTestBed.Pawn();
        pawn.SkillLevels["Medicine"] = 15;
        var colony = RecsTestBed.Colony(
            new List<RoleView> { core, basics, target, firstLower, secondLower }, pawn);
        colony.OrderTemplate = new List<int>
            { core.Id, basics.Id, target.Id, firstLower.Id, secondLower.Id };
        var selectedPath = RecsTestBed.Path(1,
            (firstLower.Id, 0, 10), (target.Id, 10, 21));
        selectedPath.AnchorRoleId = core.Id;
        selectedPath.AnchorBefore = false;
        var otherPath = RecsTestBed.Path(2,
            (secondLower.Id, 0, 10), (target.Id, 10, 21));
        otherPath.AnchorRoleId = basics.Id;
        otherPath.AnchorBefore = false;
        colony.Paths.Add(selectedPath);
        colony.Paths.Add(otherPath);

        PawnResult result = RecsEngine.Run(colony)[0];

        await Assert.That(RecsTestBed.Ids(result)).IsEqualTo("1,3,2");
        await Assert.That(result.Reasons[target.Id].RuleId).IsEqualTo("draft");
    }
}
