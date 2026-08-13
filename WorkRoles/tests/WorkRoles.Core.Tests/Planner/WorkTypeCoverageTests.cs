using System.Xml.Linq;

namespace WorkRoles.Core.Tests.Planner;

public class WorkTypeCoverageTests
{
    private static JobEntry WT(string defName) => new(JobEntryKind.WorkType, defName);

    private static JobEntry WG(string defName) => new(JobEntryKind.WorkGiver, defName);

    [Test]
    public async Task GiverContributesItsParentWorkType()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients");
        List<(IReadOnlyList<JobEntry>, bool)> roles = [([WG("TendPatients")], false)];

        var covered = WorkTypeCoverage.CoveredWorkTypes(roles, catalog);

        await Assert.That(covered.Contains("Doctor")).IsTrue();
    }

    [Test]
    public async Task BlockerContributesNoCoveredWorkTypes()
    {
        var catalog = new FakeCatalog().WithWorkType("Firefighter", "FightFires");
        List<(IReadOnlyList<JobEntry>, bool)> roles = [([WT("Firefighter")], true)];

        var covered = WorkTypeCoverage.CoveredWorkTypes(roles, catalog);

        await Assert.That(covered.Contains("Firefighter")).IsFalse();
    }

    [Test]
    public async Task ShippedCatalogCoversEveryVanillaWorkType()
    {
        // Data guard: the roles we ship must reach every giver-bearing vanilla
        // work type (invisible ones route to Odd Jobs at runtime, but all vanilla
        // types are visible).
        var path = Path.Combine(RepoRoot(), "mod", "1.6", "Defs", "Roles.xml");
        var catalog = new FakeCatalog();
        foreach (var group in VanillaGiverBaseline.GiverWorkType.GroupBy(kv => kv.Value))
            catalog.WithWorkType(group.Key, group.Select(kv => kv.Key).ToArray());

        List<(IReadOnlyList<JobEntry>, bool)> roles = [];
        foreach (var def in XElement.Load(path).Elements("WorkRoles.RoleDef"))
        {
            List<JobEntry> entries = [];
            foreach (var li in def.Element("entries")?.Elements("li") ?? [])
                if (JobEntry.TryDecode(li.Value.Trim(), out var entry))
                    entries.Add(entry);
            roles.Add((entries, def.Element("blocker")?.Value.Trim() == "true"));
        }
        var covered = WorkTypeCoverage.CoveredWorkTypes(roles, catalog);
        foreach (var workType in VanillaGiverBaseline.GiverWorkType.Values.Distinct())
            await Assert.That(covered.Contains(workType)).IsTrue().Because($"no shipped role covers the vanilla work type {workType}");
    }

    [Test]
    public async Task MovedGiversDetectsMovesSkipsUnmovedMissingAndKnown()
    {
        // Baseline says all four belong to Doctor; the CURRENT catalog has moved
        // TendAnimals to Veterinary and FeedAnimals to Handling; RemovedByMod is
        // gone entirely; TendPatients stayed.
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients").WithWorkType("Veterinary", "TendAnimals").WithWorkType("Handling", "FeedAnimals");
        var baseline = new Dictionary<string, string>
        {
            ["TendPatients"] = "Doctor",
            ["TendAnimals"] = "Doctor",
            ["FeedAnimals"] = "Doctor",
            ["RemovedByMod"] = "Doctor",
        };
        var snapshots = new Dictionary<string, List<string>>
        {
            ["Doctor"] = ["FeedAnimals"], // already remembered
        };
        var moved = WorkTypeCoverage.MovedGivers([WT("Doctor")], snapshots, baseline, catalog);
        await Assert.That(string.Join(",", moved["Doctor"])).IsEqualTo("TendAnimals");
    }

    [Test]
    public async Task MovedGiversSkipsGiversCarriedAsExplicitEntries()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients").WithWorkType("Rescuing", "DoctorRescue");
        var baseline = new Dictionary<string, string> { ["TendPatients"] = "Doctor", ["DoctorRescue"] = "Doctor" };
        // The role already carries the moved giver as its own entry (e.g. a
        // reset appended it): nothing is lost, nothing to recover.
        var moved = WorkTypeCoverage.MovedGivers([WT("Doctor"), WG("DoctorRescue")], new Dictionary<string, List<string>>(), baseline, catalog);
        await Assert.That(moved == null).IsTrue();
    }

    [Test]
    public async Task MovedGiversReturnsNullWhenNothingMoved()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients");
        var moved = WorkTypeCoverage.MovedGivers([WT("Doctor")], new Dictionary<string, List<string>>(), new Dictionary<string, string> { ["TendPatients"] = "Doctor" }, catalog);
        await Assert.That(moved == null).IsTrue();
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        return dir!.FullName;
    }
}
