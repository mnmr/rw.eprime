using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

public class MechanicalExclusionRegressionTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ExclusionsRemoveUnpinnedNonprimaryProtectionsButRetainPinsAndAcceptedPrimary(bool incapacitated)
    {
        var recs = new RecsProjection().WorkType("Construction", "Construction", 400, "Build", "Repair", "Deconstruct");
        var primary = recs.RoleByWorkType(1, 1, 0, "Construction");
        var conditional = recs.RoleByGiver(2, 0, 0, "Build");
        conditional.HasRules = true;
        var blocker = recs.RoleByGiver(3, 0, 0, "Repair");
        blocker.Blocker = true;
        var pinned = recs.RoleByGiver(4, 0, 0, "Deconstruct");
        pinned.HasRules = true;
        var worker = new MechanicalWorkerView
        {
            Id = 42, ProvidesService = !incapacitated,
            ExecutableGivers = { "Build", "Repair", "Deconstruct" },
            CommittedGivers = { "Build", "Repair", "Deconstruct" }, ActiveRoleIds = { 1 },
            Facts = new PawnView
            {
                IsMechanical = true, Incapacitated = incapacitated,
                CapableWorkTypes = { "Construction" }, SkillLevels = { ["Construction"] = 20 },
                Existing = { new() { RoleId = 3, Enabled = true }, new() { RoleId = 1, Enabled = true },
                    new() { RoleId = 2, Enabled = true }, new() { RoleId = 4, Enabled = true, Pinned = true } },
            },
        };
        ColonyView colony = recs.Colony(new PawnView());
        await Assert.That(string.Join(",", MechanicalDutyPlan.BuildFixTarget(colony, worker).RoleIds)).IsEqualTo("3,1,2,4");
        foreach (RoleView role in colony.Roles.Where(r => r.Id != 1)) role.ExcludedFromRecommendations = true;
        await Assert.That(string.Join(",", MechanicalDutyPlan.BuildFixTarget(colony, worker).RoleIds)).IsEqualTo("1,4");
        if (!incapacitated)
        {
            colony.Roles.Single(r => r.Id == 1).ExcludedFromRecommendations = true;
            await Assert.That(string.Join(",", MechanicalDutyPlan.BuildFixTarget(colony, worker).RoleIds)).IsEqualTo("1,4");
        }
        worker.Facts.Existing = [new() { RoleId = 1, Enabled = true }, new() { RoleId = 4, Enabled = true, Pinned = true }];
        await Assert.That(MechanicalDutyPlan.BuildFixTarget(colony, worker).Changed).IsFalse();
    }
}
