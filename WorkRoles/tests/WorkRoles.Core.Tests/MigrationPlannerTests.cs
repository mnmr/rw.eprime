using System.Xml.Linq;
using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// Guards migration against catalog-shape regressions — including the shipped
/// Roles.xml, parsed straight from the repo, against the vanilla giver baseline.
public class MigrationPlannerTests
{
    private static JobEntry WT(string defName) => new(JobEntryKind.WorkType, defName);
    private static JobEntry WG(string defName) => new(JobEntryKind.WorkGiver, defName);

    [Test]
    public async Task SameTypeGiverEntriesDoNotDisqualifyASingleTypeRole()
    {
        // The 1.0.3 regression: Doctor carries Medic's givers (literal subset),
        // and migration stopped matching it — dropping the Doctor work type.
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients", "FeedPatients");
        var roles = new List<MigrationRole>
        {
            new(1, new List<JobEntry> { WT("Doctor"), WG("TendPatients"), WG("FeedPatients") }, false),
        };
        var plan = MigrationPlanner.Plan(roles,
            new Dictionary<string, int> { ["Doctor"] = 1 },
            new List<string> { "Doctor" }, catalog);
        await Assert.That(string.Join(",", plan)).IsEqualTo("1");
    }

    [Test]
    public async Task ComboUsedOnlyWhenAllCapableMembersShareOnePriority()
    {
        var catalog = new FakeCatalog()
            .WithWorkType("Hauling", "HaulGeneral")
            .WithWorkType("Cleaning", "CleanFilth");
        var roles = new List<MigrationRole>
        {
            new(1, new List<JobEntry> { WT("Hauling"), WT("Cleaning") }, false), // Grunt
            new(2, new List<JobEntry> { WT("Hauling") }, false),
            new(3, new List<JobEntry> { WT("Cleaning") }, false),
        };
        var order = new List<string> { "Hauling", "Cleaning" };

        var shared = MigrationPlanner.Plan(roles,
            new Dictionary<string, int> { ["Hauling"] = 3, ["Cleaning"] = 3 }, order, catalog);
        await Assert.That(string.Join(",", shared)).IsEqualTo("1");

        var split = MigrationPlanner.Plan(roles,
            new Dictionary<string, int> { ["Hauling"] = 2, ["Cleaning"] = 4 }, order, catalog);
        await Assert.That(string.Join(",", split)).IsEqualTo("2,3");
    }

    // ----- Shipped-data round trip -----

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    private static List<MigrationRole> ShippedRoles()
    {
        var path = Path.Combine(RepoRoot(), "mod", "1.6", "Defs", "Roles.xml");
        var roles = new List<MigrationRole>();
        int id = 1;
        foreach (var def in XElement.Load(path).Elements())
        {
            var entries = new List<JobEntry>();
            foreach (var li in def.Element("entries")?.Elements("li") ?? Enumerable.Empty<XElement>())
                if (JobEntry.TryDecode(li.Value.Trim(), out var entry))
                    entries.Add(entry);
            bool blocker = def.Element("blocker")?.Value.Trim() == "true";
            roles.Add(new MigrationRole(id++, entries, blocker));
        }
        return roles;
    }

    private static FakeCatalog BaselineCatalog()
    {
        var catalog = new FakeCatalog();
        foreach (var group in VanillaGiverBaseline.GiverWorkType.GroupBy(kv => kv.Value))
            catalog.WithWorkType(group.Key, group.Select(kv => kv.Key).ToArray());
        return catalog;
    }

    /// Work types the round trip can verify: reachable by at least one giver.
    private static List<string> GiverBearingWorkTypes(FakeCatalog catalog, List<MigrationRole> roles)
    {
        var types = VanillaGiverBaseline.GiverWorkType.Values.Distinct().ToList();
        foreach (var role in roles)
            foreach (var entry in role.Entries)
                if (entry.Kind == JobEntryKind.WorkType && !types.Contains(entry.DefName))
                    types.Add(entry.DefName);
        return types.Where(t => catalog.WorkGiversOf(t).Count > 0).ToList();
    }

    private static async Task AssertGridRoundTrips(Func<int, int> priorityOfIndex)
    {
        var catalog = BaselineCatalog();
        var roles = ShippedRoles();
        var workTypes = GiverBearingWorkTypes(catalog, roles);
        var priorities = new Dictionary<string, int>();
        for (int i = 0; i < workTypes.Count; i++)
            priorities[workTypes[i]] = priorityOfIndex(i);

        var plan = MigrationPlanner.Plan(roles, priorities, workTypes, catalog);
        var byId = roles.ToDictionary(r => r.Id);
        var compiled = JobOrderCompiler.Compile(
            plan.Select(id => (byId[id].Entries, false)), catalog, _ => true);

        // (a) Presence: every enabled work type survives; every disabled one is absent.
        foreach (var pair in priorities)
        {
            bool present = compiled.WorkTypePriorities.ContainsKey(pair.Key);
            if (pair.Value > 0)
                await Assert.That(present).IsTrue()
                    .Because($"{pair.Key} (priority {pair.Value}) was dropped by migration");
            else
                await Assert.That(present).IsFalse()
                    .Because($"{pair.Key} (priority 0) appeared after migration");
        }

        // (b) Ordering: a higher vanilla priority (lower number) ranks earlier.
        foreach (var a in priorities.Where(p => p.Value > 0))
            foreach (var b in priorities.Where(p => p.Value > 0))
                if (a.Value < b.Value)
                    await Assert.That(
                            compiled.WorkTypePriorities[a.Key] < compiled.WorkTypePriorities[b.Key]).IsTrue()
                        .Because($"{a.Key} (prio {a.Value}) should rank above {b.Key} (prio {b.Value})");
    }

    [Test]
    public async Task ShippedCatalog_FlatGridRoundTrips() =>
        await AssertGridRoundTrips(_ => 3);

    [Test]
    public async Task ShippedCatalog_CyclicGridRoundTrips() =>
        await AssertGridRoundTrips(i => i % 5); // 0-4: exercises singles + disabled

    [Test]
    public async Task ShippedCatalog_AlternatingGridRoundTrips() =>
        await AssertGridRoundTrips(i => i % 2 == 0 ? 1 : 4);
}
