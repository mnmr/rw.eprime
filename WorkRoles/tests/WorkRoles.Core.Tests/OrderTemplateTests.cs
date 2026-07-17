using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// Template management over RoleViews: the derived default (vanilla-grid
/// columns), stored-override resolution, Add-menu candidates, insert spots.
public class OrderTemplateTests
{
    private static RoleView Role(int id, float priority, params string[] coverage)
    {
        var role = RecsTestBed.Role(id, "Cooking", coverage);
        role.NaturalPriority = priority;
        return role;
    }

    [Test]
    public async Task DerivedTemplateIsPinnableUncoveredNonHuntingByPriority()
    {
        var farmer = Role(1, 50f, "Grow", "Cut");
        var grower = Role(2, 40f, "Grow");            // covered: floats
        var doctor = Role(3, 90f, "Tend");
        var hunter = Role(4, 60f, "Hunt"); hunter.Hunting = true;
        var blocker = Role(5, 99f, "Veto"); blocker.Blocker = true;
        var catalog = new List<RoleView> { farmer, grower, doctor, hunter, blocker };
        await Assert.That(string.Join(",", OrderTemplate.DeriveTemplate(catalog)))
            .IsEqualTo("3,1");
    }

    [Test]
    public async Task StoredOverrideResolvesPureAndFallsBackWhenEmpty()
    {
        var a = Role(1, 50f, "A");
        var b = Role(2, 40f, "B");
        var rules = Role(3, 60f, "C"); rules.HasRules = true;
        var catalog = new List<RoleView> { a, b, rules };
        await Assert.That(string.Join(",", OrderTemplate.ResolveTemplate(
            new List<int> { 2, 3, 1, 2, 99 }, catalog))).IsEqualTo("2,1");
        await Assert.That(string.Join(",", OrderTemplate.ResolveTemplate(
            new List<int>(), catalog))).IsEqualTo("1,2");
    }

    [Test]
    public async Task EveryPinnableRoleIsPinnedOrAddable()
    {
        var a = Role(1, 50f, "A");
        var covered = Role(2, 40f, "A2"); // distinct coverage: pinnable + uncovered
        var blocker = Role(3, 99f, "V"); blocker.Blocker = true;
        var catalog = new List<RoleView> { a, covered, blocker };
        var template = OrderTemplate.ResolveTemplate(new List<int> { 1 }, catalog);
        var addable = OrderTemplate.AddCandidates(catalog, template);
        await Assert.That(string.Join(",", addable)).IsEqualTo("2");
    }

    [Test]
    public async Task InsertIndexLandsAnUnlistedRoleAtItsFloatingSpot()
    {
        var top = Role(1, 100f, "T");
        var bottom = Role(2, 10f, "B");
        var mid = Role(3, 50f, "M"); // floats after top (>=50), before bottom
        var catalog = new List<RoleView> { top, bottom, mid };
        var template = new List<int> { 1, 2 };
        await Assert.That(OrderTemplate.InsertIndex(mid, template, catalog)).IsEqualTo(1);
    }
}
