using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

public class RecsHolderAllocationTests
{
    [Test]
    public async Task CustomMaximumKeepsOnlyTheBestDirectCandidates()
    {
        var role = RecsTestBed.Role(1, "Crafting");
        role.HolderMode = RoleHolderMode.Custom;
        role.MaxHolders = 1;
        var lower = RecsTestBed.Pawn();
        lower.SkillLevels["Crafting"] = 5;
        lower.SignalBuckets["Crafting"] = SignalBucket.Strong;
        var higher = RecsTestBed.Pawn();
        higher.SkillLevels["Crafting"] = 12;
        higher.SignalBuckets["Crafting"] = SignalBucket.Strong;

        List<PawnResult> results = RecsEngine.Run(
            RecsTestBed.Colony(new List<RoleView> { role }, lower, higher));

        await Assert.That(results[0].Assignments.Any(a => a.RoleId == role.Id)).IsFalse();
        await Assert.That(results[1].Assignments.Any(a => a.RoleId == role.Id)).IsTrue();
    }

    [Test]
    public async Task PinnedAssignmentConsumesTheMinimumAndMaximumWithoutBeingRemoved()
    {
        var role = RecsTestBed.Role(1, "Crafting");
        role.HolderMode = RoleHolderMode.Custom;
        role.MinHolders = 1;
        role.MaxHolders = 1;
        var pinned = RecsTestBed.Pawn();
        pinned.SkillLevels["Crafting"] = 0;
        pinned.SignalBuckets["Crafting"] = SignalBucket.Awful;
        pinned.Existing.Add(new AssignmentView
            { RoleId = role.Id, Enabled = true, Pinned = true });
        var interested = RecsTestBed.Pawn();
        interested.SkillLevels["Crafting"] = 12;
        interested.SignalBuckets["Crafting"] = SignalBucket.Strong;

        List<PawnResult> results = RecsEngine.Run(
            RecsTestBed.Colony(new List<RoleView> { role }, pinned, interested));

        await Assert.That(results[0].Assignments.Any(a => a.RoleId == role.Id)).IsTrue();
        await Assert.That(results[1].Assignments.Any(a => a.RoleId == role.Id)).IsFalse();
    }

    [Test]
    public async Task CustomUnskilledRoleUsesItsHolderMinimum()
    {
        var role = RecsTestBed.Unskilled(1, "Hauling");
        role.HolderMode = RoleHolderMode.Custom;
        role.MinHolders = 2;
        role.MaxHolders = 2;
        var colony = RecsTestBed.Colony(new List<RoleView> { role },
            RecsTestBed.Pawn(), RecsTestBed.Pawn(), RecsTestBed.Pawn());

        List<PawnResult> results = RecsEngine.Run(colony);

        await Assert.That(results.Count(r =>
            r.Assignments.Any(a => a.RoleId == role.Id))).IsEqualTo(2);
    }

    [Test]
    public async Task RuleBasedRolesDoNotCreateHolderDemand()
    {
        var role = RecsTestBed.Role(1, "Crafting");
        role.HasRules = true;
        role.HolderMode = RoleHolderMode.Custom;
        role.MinHolders = 1;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { role }, RecsTestBed.Pawn()));

        new CoverageScalingRule(new UnitScaling()).Apply(context);

        await Assert.That(context.Want.ContainsKey(role.Id)).IsFalse();
    }

    [Test]
    public async Task HunterMinimumNeverDraftsAnUnarmedPawn()
    {
        var hunter = RecsTestBed.Role(1, "Hunting");
        hunter.Hunting = true;
        hunter.HolderMode = RoleHolderMode.Custom;
        hunter.MinHolders = 2;
        var armed = RecsTestBed.Pawn();
        armed.HasRangedWeapon = true;
        armed.ShootingLevel = 8;
        armed.SkillLevels["Shooting"] = 8;
        var unarmed = RecsTestBed.Pawn();
        unarmed.SkillLevels["Shooting"] = 15;
        var colony = RecsTestBed.Colony(
            new List<RoleView> { hunter }, armed, unarmed);
        colony.HunterRoleId = hunter.Id;

        List<PawnResult> results = RecsEngine.Run(colony);

        await Assert.That(results[0].Assignments.Any(a => a.RoleId == hunter.Id)).IsTrue();
        await Assert.That(results[1].Assignments.Any(a => a.RoleId == hunter.Id)).IsFalse();
    }

    [Test]
    public async Task HunterMaximumReappliesTheLowTierPromotionAfterCapping()
    {
        var hunter = RecsTestBed.Role(1, "Hunting");
        hunter.Hunting = true;
        hunter.HolderMode = RoleHolderMode.Custom;
        hunter.MaxHolders = 1;
        var lower = RecsTestBed.Pawn();
        lower.HasRangedWeapon = true;
        lower.ShootingLevel = 16;
        lower.SkillLevels["Shooting"] = 16;
        var higher = RecsTestBed.Pawn();
        higher.HasRangedWeapon = true;
        higher.ShootingLevel = 20;
        higher.SkillLevels["Shooting"] = 20;
        var colony = RecsTestBed.Colony(
            new List<RoleView> { hunter }, lower, higher);
        colony.HunterRoleId = hunter.Id;

        List<PawnResult> results = RecsEngine.Run(colony);

        await Assert.That(results.Count(r =>
            r.Assignments.Any(a => a.RoleId == hunter.Id))).IsEqualTo(1);
        await Assert.That(results.Single(r =>
            r.Assignments.Any(a => a.RoleId == hunter.Id)).HunterTier).IsEqualTo(0);
    }
}
