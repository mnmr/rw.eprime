using System.Xml.Linq;
using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class WorkTypeCoverageTests
{
    private static JobEntry WT(string defName) => new(JobEntryKind.WorkType, defName);
    private static JobEntry WG(string defName) => new(JobEntryKind.WorkGiver, defName);

    [Test]
    public async Task GiversContributeParentTypes_BlockersContributeNothing()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients");
        var covered = WorkTypeCoverage.CoveredWorkTypes(new[]
        {
            ((IReadOnlyList<JobEntry>)new List<JobEntry> { WG("TendPatients") }, false),
            ((IReadOnlyList<JobEntry>)new List<JobEntry> { WT("Firefighter") }, true), // blocker
        }, catalog);
        await Assert.That(covered.Contains("Doctor")).IsTrue();
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

        var roles = new List<(IReadOnlyList<JobEntry>, bool)>();
        foreach (var def in XElement.Load(path).Elements())
        {
            var entries = new List<JobEntry>();
            foreach (var li in def.Element("entries")?.Elements("li") ?? Enumerable.Empty<XElement>())
                if (JobEntry.TryDecode(li.Value.Trim(), out var entry))
                    entries.Add(entry);
            roles.Add((entries, def.Element("blocker")?.Value.Trim() == "true"));
        }
        var covered = WorkTypeCoverage.CoveredWorkTypes(roles, catalog);
        foreach (var workType in VanillaGiverBaseline.GiverWorkType.Values.Distinct())
            await Assert.That(covered.Contains(workType)).IsTrue()
                .Because($"no shipped role covers the vanilla work type {workType}");
    }

    [Test]
    public async Task MovedGiversDetectsMovesSkipsUnmovedMissingAndKnown()
    {
        // Baseline says all four belong to Doctor; the CURRENT catalog has moved
        // TendAnimals to Veterinary and FeedAnimals to Handling; RemovedByMod is
        // gone entirely; TendPatients stayed.
        var catalog = new FakeCatalog()
            .WithWorkType("Doctor", "TendPatients")
            .WithWorkType("Veterinary", "TendAnimals")
            .WithWorkType("Handling", "FeedAnimals");
        var baseline = new Dictionary<string, string>
        {
            ["TendPatients"] = "Doctor",
            ["TendAnimals"] = "Doctor",
            ["FeedAnimals"] = "Doctor",
            ["RemovedByMod"] = "Doctor",
        };
        var snapshots = new Dictionary<string, List<string>>
        {
            ["Doctor"] = new List<string> { "FeedAnimals" }, // already remembered
        };
        var moved = WorkTypeCoverage.MovedGivers(
            new List<JobEntry> { WT("Doctor") }, snapshots, baseline, catalog);
        await Assert.That(string.Join(",", moved["Doctor"])).IsEqualTo("TendAnimals");
    }

    [Test]
    public async Task MovedGiversReturnsNullWhenNothingMoved()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients");
        var moved = WorkTypeCoverage.MovedGivers(
            new List<JobEntry> { WT("Doctor") },
            new Dictionary<string, List<string>>(),
            new Dictionary<string, string> { ["TendPatients"] = "Doctor" },
            catalog);
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
