using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// Rule 1 (exclusions veto) and rule 2 (auto roles for every capable pawn).
public class RecsExclusionAutoTests
{
    [Test]
    public async Task ExclusionsVetoRulesBlockersNeverResearchLockedAndDisabled()
    {
        var rules = RecsTestBed.Role(1, "Cooking"); rules.HasRules = true;
        var blocker = RecsTestBed.Role(2, "Cooking"); blocker.Blocker = true;
        var never = RecsTestBed.Role(3, "Cooking");
        never.HolderMode = RoleHolderMode.Never;
        never.MinHolders = 2;
        never.MaxHolders = 4;
        var locked = RecsTestBed.Role(4, "Cooking"); locked.Available = false;
        var disabled = RecsTestBed.Role(5, "Cooking"); disabled.Enabled = false;
        var plain = RecsTestBed.Role(6, "Cooking");
        var colony = RecsTestBed.Colony(
            new List<RoleView> { rules, blocker, never, locked, disabled, plain });
        var context = new EngineContext(colony);
        new ExclusionsRule().Apply(context);
        await Assert.That(string.Join(",", context.Vetoed.OrderBy(id => id)))
            .IsEqualTo("1,2,3,4,5");
    }

    [Test]
    public async Task NeverModeCannotBeForceAdded()
    {
        var never = RecsTestBed.Role(1, "Cooking");
        never.HolderMode = RoleHolderMode.Never;
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { never }, RecsTestBed.Pawn()));

        new ExclusionsRule().Apply(context);
        context.AddCandidate(0, never.Id,
            new Reason { RuleId = "forced" }, SignalBucket.Neutral, force: true);

        await Assert.That(context.Candidates[0].ContainsKey(never.Id)).IsFalse();
    }

    [Test]
    public async Task NeverModeCannotReturnThroughPinnedReentry()
    {
        var never = RecsTestBed.Role(1, "Cooking");
        never.HolderMode = RoleHolderMode.Never;
        var pawn = RecsTestBed.Pawn();
        pawn.Existing.Add(new AssignmentView { RoleId = never.Id, Pinned = true });
        var context = new EngineContext(RecsTestBed.Colony(
            new List<RoleView> { never }, pawn));

        new ProtectedReentryRule().Apply(context, 0);

        await Assert.That(context.Results[0].Assignments.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AutoRolesGoToEveryCapablePawn_VetoedAutosDoNot()
    {
        var basics = RecsTestBed.Role(1, "Cooking"); basics.AutoAssign = true;
        var lockedAuto = RecsTestBed.Role(2, "Cooking");
        lockedAuto.AutoAssign = true; lockedAuto.Available = false;
        var incapableOnly = RecsTestBed.Role(3, "Mining"); incapableOnly.AutoAssign = true;
        var capable = RecsTestBed.Pawn();
        var colony = RecsTestBed.Colony(
            new List<RoleView> { basics, lockedAuto, incapableOnly }, capable);
        var context = new EngineContext(colony);
        new ExclusionsRule().Apply(context);
        new AutoRolesRule().Apply(context, 0);
        await Assert.That(context.Candidates[0].ContainsKey(1)).IsTrue();
        await Assert.That(context.Candidates[0][1].Reason.RuleId).IsEqualTo("auto");
        await Assert.That(context.Candidates[0].ContainsKey(2)).IsFalse();
        await Assert.That(context.Candidates[0].ContainsKey(3)).IsFalse(); // not capable of Mining
    }
}
