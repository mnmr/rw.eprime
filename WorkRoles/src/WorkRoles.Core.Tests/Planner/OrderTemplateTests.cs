using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// Template management over RoleViews: the shipped def-name default order,
/// the priority-derived fallback, stored-override resolution and Add-menu
/// candidates.
public class OrderTemplateTests
{
    private static RoleView Role(int id, int priority, params string[] coverage)
    {
        var role = RecsTestBed.Role(id, "Cooking", coverage);
        RecsTestBed.SetNaturalPriority(role, priority);
        return role;
    }

    private static RoleView Template(int id, string defName, params string[] coverage)
    {
        var role = Role(id, 0, coverage);
        role.TemplateDefName = defName;
        return role;
    }

    [Test]
    public async Task DerivedTemplateFollowsShippedDefNameOrder()
    {
        // Catalog order scrambled relative to the shipped list; Fabricator's
        // coverage is a subset of Smith's, which no longer excludes it, and
        // the shipped list places it before Smith.
        var smith = Template(1, "WS_Smith", "Weapons", "Fabricate");
        var fabricator = Template(2, "WS_Fabricator", "Fabricate");
        var cook = Template(3, "WS_Cook", "Meals");
        var doctor = Template(4, "WS_Doctor", "Tend");
        var custom = Role(5, 90, "Custom"); // unlisted: floats
        var blocked = Template(6, "WS_Grunt", "Haul");
        blocked.Blocker = true;
        List<RoleView> catalog = [smith, fabricator, custom, cook, blocked, doctor];
        await Assert.That(string.Join(",", OrderTemplate.DeriveTemplate(catalog))).IsEqualTo("4,3,2,1");
    }

    [Test]
    public async Task DerivedTemplateFallsBackToPriorityOrderWithoutTemplateRoles()
    {
        var farmer = Role(1, 50, "Grow", "Cut");
        var grower = Role(2, 40, "Grow"); // covered: floats
        var doctor = Role(3, 90, "Tend");
        var hunter = RecsTestBed.Role(4, "Hunting", "Hunt");
        RecsTestBed.SetNaturalPriority(hunter, 60);
        var blocker = Role(5, 99, "Veto");
        blocker.Blocker = true;
        List<RoleView> catalog = [farmer, grower, doctor, hunter, blocker];
        await Assert.That(string.Join(",", OrderTemplate.DeriveTemplate(catalog))).IsEqualTo("3,1");
    }

    [Test]
    public async Task StoredOverrideResolvesValidUniqueRoles()
    {
        var a = Role(1, 50, "A");
        var b = Role(2, 40, "B");
        var rules = Role(3, 60, "C");
        rules.HasRules = true;
        List<RoleView> catalog = [a, b, rules];
        await Assert.That(string.Join(",", OrderTemplate.ResolveTemplate([2, 3, 1, 2, 99], catalog))).IsEqualTo("2,1");
    }

    [Test]
    public async Task EmptyStoredOverrideFallsBackToDerivedTemplate()
    {
        var a = Role(1, 50, "A");
        var b = Role(2, 40, "B");
        var rules = Role(3, 60, "C");
        rules.HasRules = true;
        List<RoleView> catalog = [a, b, rules];

        await Assert.That(string.Join(",", OrderTemplate.ResolveTemplate([], catalog))).IsEqualTo("1,2");
    }

    [Test]
    [Arguments("2,1", true)]
    [Arguments("1,2", false)]
    [Arguments("2", false)]
    public async Task StoredListMatchesTheOldSeedOnlyWhenMembershipAndOrderAreIdentical(string storedRoleIds, bool expected)
    {
        var farmer = Template(1, "WS_Farmer", "Grow", "Cut");
        RecsTestBed.SetNaturalPriority(farmer, 50);
        var doctor = Template(2, "WS_Doctor", "Tend");
        RecsTestBed.SetNaturalPriority(doctor, 90);
        List<RoleView> catalog = [farmer, doctor];
        List<int> stored = storedRoleIds.Length == 0 ? [] : [.. storedRoleIds.Split(',').Select(int.Parse)];

        bool result = OrderTemplate.MatchesPriorityDerivedTemplate(stored, catalog);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task EveryPinnableRoleIsPinnedOrAddable()
    {
        var a = Role(1, 50, "A");
        var covered = Role(2, 40, "A2"); // distinct coverage: pinnable + uncovered
        var blocker = Role(3, 99, "V");
        blocker.Blocker = true;
        List<RoleView> catalog = [a, covered, blocker];
        var template = OrderTemplate.ResolveTemplate([1], catalog);
        var addable = OrderTemplate.AddCandidates(catalog, template);
        await Assert.That(string.Join(",", addable)).IsEqualTo("2");
    }
}
