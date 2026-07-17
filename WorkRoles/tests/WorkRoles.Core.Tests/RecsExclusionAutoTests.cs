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
        var never = RecsTestBed.Role(3, "Cooking"); never.MinHolders = RoleView.NeverHolders;
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
