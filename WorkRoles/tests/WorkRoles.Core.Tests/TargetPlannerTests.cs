using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// Invariants of the plan's ordering rules — the machinery behind Recommended
/// Roles, Make It So and Fix My Colony.
public class TargetPlannerTests
{
    /// Coverage tokens stand in for expanded giver sets.
    private static TargetRole Role(int id, bool auto = false, bool rules = false,
        bool blocker = false, bool unskilled = false, bool doctoring = false,
        float priority = 0f, params string[] coverage) => new()
    {
        Id = id, AutoAssign = auto, HasRules = rules, Blocker = blocker,
        Unskilled = unskilled, Doctoring = doctoring, NaturalPriority = priority,
        Coverage = new HashSet<string>(coverage),
    };

    private static PlannedAssignment Held(int id, bool enabled = true, bool pinned = false) =>
        new() { RoleId = id, Enabled = enabled, Pinned = pinned };

    private static string Ids(IEnumerable<PlannedAssignment> plan) =>
        string.Join(",", plan.Select(a => a.RoleId));

    [Test]
    public async Task AutosLeadByNaturalPriority_ThenRecs_ThenUnskilledTail()
    {
        var catalog = new List<TargetRole>
        {
            Role(1, auto: true, priority: 10f),
            Role(2, auto: true, priority: 90f),
            Role(3),                       // skilled rec
            Role(4, unskilled: true),      // grunt-style rec
        };
        var plan = TargetPlanner.Build(new List<PlannedAssignment>(), catalog,
            recommendations: new List<int> { 3, 4 }, extraIds: null, promoted: null,
            hunterTier: -1, hunterRoleId: -1);
        await Assert.That(Ids(plan)).IsEqualTo("2,1,3,4");
    }

    [Test]
    public async Task ProtectedAssignmentsReenterAtTheirOriginalPosition()
    {
        var catalog = new List<TargetRole>
        {
            Role(1, auto: true, priority: 1f),
            Role(2, rules: true),   // auto (rule-carrying) role held at index 1
            Role(3),                // skilled rec
            Role(4, blocker: true), // blocker held at index 0
        };
        var existing = new List<PlannedAssignment> { Held(4, enabled: false), Held(2) };
        var plan = TargetPlanner.Build(existing, catalog,
            new List<int> { 3 }, null, null, -1, -1);
        // Blocker back at index 0 (with its per-pawn toggle), rules-role at 1.
        await Assert.That(Ids(plan)).IsEqualTo("4,2,1,3");
        await Assert.That(plan[0].Enabled).IsFalse();
    }

    [Test]
    public async Task PinningBlockersOrRuleCarryingRolesChangesNothing()
    {
        // Blockers and rule-carrying (auto) roles are already fully protected:
        // never removed, never moved. A pin on such an assignment is redundant
        // — the UI therefore shows no pin marker on them.
        var catalog = new List<TargetRole>
        {
            Role(2, rules: true),
            Role(3),
            Role(4, blocker: true),
        };
        var unpinned = TargetPlanner.Build(
            new List<PlannedAssignment> { Held(4), Held(2) },
            catalog, new List<int> { 3 }, null, null, -1, -1);
        var pinned = TargetPlanner.Build(
            new List<PlannedAssignment> { Held(4, pinned: true), Held(2, pinned: true) },
            catalog, new List<int> { 3 }, null, null, -1, -1);
        await Assert.That(Ids(pinned)).IsEqualTo(Ids(unpinned));
    }

    [Test]
    public async Task ExtraIdsAppendAfterRecsInOrder_SkippingAutosRulesAndDuplicates()
    {
        // extraIds = the virtual-set remainder Fix My Colony passes: roles the
        // colony pass granted that recommendations didn't mention.
        var catalog = new List<TargetRole>
        {
            Role(1, auto: true, priority: 5f),
            Role(2),                 // rec
            Role(3),                 // extra only
            Role(4),                 // extra AND rec: no duplicate
            Role(5, rules: true),    // extra with rules: never inserted here
        };
        var plan = TargetPlanner.Build(new List<PlannedAssignment>(), catalog,
            recommendations: new List<int> { 2, 4 },
            extraIds: new List<int> { 4, 3, 5 }, promoted: null, -1, -1);
        await Assert.That(Ids(plan)).IsEqualTo("1,2,4,3");
    }

    [Test]
    public async Task UnskilledExtrasSinkToTheTail_AndCoveredOnesDrop()
    {
        var haul = Role(10, unskilled: true, coverage: "Hauling");
        var grunt = Role(12, unskilled: true, coverage: new[] { "Hauling", "Cleaning" });
        var skilled = Role(3);

        // No coverer in the plan: the unskilled extra tails after skilled recs.
        var tailed = TargetPlanner.Build(new List<PlannedAssignment>(),
            new List<TargetRole> { haul, skilled },
            recommendations: new List<int> { 3 },
            extraIds: new List<int> { 10 }, promoted: null, -1, -1);
        await Assert.That(Ids(tailed)).IsEqualTo("3,10");

        // Grunt in the plan covers Hauling: the unskilled extra drops.
        var covered = TargetPlanner.Build(new List<PlannedAssignment>(),
            new List<TargetRole> { haul, grunt, skilled },
            recommendations: new List<int> { 12, 3 },
            extraIds: new List<int> { 10 }, promoted: null, -1, -1);
        await Assert.That(Ids(covered)).IsEqualTo("3,12");
    }

    [Test]
    public async Task CoveredUnskilledSinglesDropInFavorOfTheirCoverer()
    {
        var hauler = Role(10, unskilled: true, coverage: "Hauling");
        var cleaner = Role(11, unskilled: true, coverage: "Cleaning");
        var grunt = Role(12, unskilled: true, coverage: new[] { "Hauling", "Cleaning" });
        var catalog = new List<TargetRole> { hauler, cleaner, grunt };

        var existing = new List<PlannedAssignment> { Held(10), Held(11) };
        var plan = TargetPlanner.Build(existing, catalog,
            recommendations: new List<int> { 12 }, extraIds: null, promoted: null, -1, -1);
        await Assert.That(Ids(plan)).IsEqualTo("12");
    }

    [Test]
    public async Task PinnedCoveredSingleSurvives()
    {
        var hauler = Role(10, unskilled: true, coverage: "Hauling");
        var grunt = Role(12, unskilled: true, coverage: new[] { "Hauling", "Cleaning" });
        var plan = TargetPlanner.Build(
            new List<PlannedAssignment> { Held(10, pinned: true) },
            new List<TargetRole> { hauler, grunt },
            new List<int> { 12 }, null, null, -1, -1);
        await Assert.That(Ids(plan)).IsEqualTo("10,12");
    }

    [Test]
    public async Task HunterTiersPlaceCorrectly_AndNeverOutrankDoctoring()
    {
        var catalog = new List<TargetRole>
        {
            Role(1, auto: true, priority: 5f),
            Role(2, doctoring: true),  // Doctor (promoted essential)
            Role(3),                   // Hunter
            Role(4),                   // other skilled rec
        };
        // Tier 0: hunter would lead after autos — but doctoring wins.
        var tier0 = TargetPlanner.Build(new List<PlannedAssignment>(), catalog,
            new List<int> { 4 }, null, promoted: new List<int> { 2 }, hunterTier: 0, hunterRoleId: 3);
        await Assert.That(Ids(tier0)).IsEqualTo("1,2,3,4");

        // Tier 2: dead last.
        var tier2 = TargetPlanner.Build(new List<PlannedAssignment>(), catalog,
            new List<int> { 4 }, null, promoted: new List<int> { 2 }, hunterTier: 2, hunterRoleId: 3);
        await Assert.That(Ids(tier2)).IsEqualTo("1,2,4,3");
    }

    [Test]
    public async Task DesignatedUnskilledKeepEarlySlot_TrailingUnskilledGoLate()
    {
        var catalog = new List<TargetRole>
        {
            Role(10, unskilled: true),
            Role(11, unskilled: true),
            Role(20), // skilled
            Role(21), // skilled rec
        };
        // Hauler held BEFORE the first skilled role = designated; cleaner after = trailing.
        var existing = new List<PlannedAssignment> { Held(10), Held(20), Held(11) };
        var plan = TargetPlanner.Build(existing, catalog,
            new List<int> { 20, 21 }, null, null, -1, -1);
        await Assert.That(Ids(plan)).IsEqualTo("10,20,21,11");
    }

    [Test]
    public async Task RetainedRolesKeepTheirToggle_NewOnesStartEnabled()
    {
        var catalog = new List<TargetRole> { Role(1), Role(2) };
        var plan = TargetPlanner.Build(
            new List<PlannedAssignment> { Held(1, enabled: false) }, catalog,
            new List<int> { 1, 2 }, null, null, -1, -1);
        await Assert.That(plan.First(a => a.RoleId == 1).Enabled).IsFalse();
        await Assert.That(plan.First(a => a.RoleId == 2).Enabled).IsTrue();
    }
}
