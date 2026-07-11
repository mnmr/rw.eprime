using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// Invariants of the plan's ordering rules — the machinery behind Recommended
/// Roles, Make It So and Fix My Colony.
public class TargetPlannerTests
{
    private static JobEntry WT(string defName) => new(JobEntryKind.WorkType, defName);

    private static TargetRole Role(int id, bool auto = false, bool rules = false,
        bool blocker = false, bool unskilled = false, bool doctoring = false,
        float priority = 0f, params JobEntry[] entries) => new()
    {
        Id = id, AutoAssign = auto, HasRules = rules, Blocker = blocker,
        Unskilled = unskilled, Doctoring = doctoring, NaturalPriority = priority,
        Entries = entries.ToList(),
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
    public async Task CoveredUnskilledSinglesDropInFavorOfTheirCoverer()
    {
        var hauler = Role(10, unskilled: true, entries: WT("Hauling"));
        var cleaner = Role(11, unskilled: true, entries: WT("Cleaning"));
        var grunt = Role(12, unskilled: true);
        grunt.Entries = new List<JobEntry> { WT("Hauling"), WT("Cleaning") };
        var catalog = new List<TargetRole> { hauler, cleaner, grunt };

        var existing = new List<PlannedAssignment> { Held(10), Held(11) };
        var plan = TargetPlanner.Build(existing, catalog,
            recommendations: new List<int> { 12 }, extraIds: null, promoted: null, -1, -1);
        await Assert.That(Ids(plan)).IsEqualTo("12");
    }

    [Test]
    public async Task PinnedCoveredSingleSurvives()
    {
        var hauler = Role(10, unskilled: true, entries: WT("Hauling"));
        var grunt = Role(12, unskilled: true);
        grunt.Entries = new List<JobEntry> { WT("Hauling"), WT("Cleaning") };
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
