using System.Xml.Linq;
using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// Certifies the shipped role defs against the vanilla work catalog
/// (VanillaWorkOrder, generated from the game's Data XML).
public class RoleDefVanillaTests
{
    private record RoleXml(string DefName, List<JobEntry> Entries);

    private static readonly List<RoleXml> Roles = LoadShippedRoles();

    /// giver defName -> work type, from the generated vanilla order table.
    private static readonly Dictionary<string, string> GiverType =
        VanillaWorkOrder.GiversInOrder
            .SelectMany(kv => kv.Value.Select(g => (g, kv.Key)))
            .ToDictionary(x => x.g, x => x.Key);

    private static List<RoleXml> LoadShippedRoles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        var path = Path.Combine(dir!.FullName, "mod", "1.6", "Defs", "Roles.xml");

        var roles = new List<RoleXml>();
        foreach (var def in XElement.Load(path).Elements("WorkRoles.RoleDef"))
        {
            var entries = new List<JobEntry>();
            foreach (var li in def.Element("entries")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                // Third-party-gated entries don't load in a vanilla+DLC game;
                // the tests emulate that runtime. DLC gates (Ludeon.*) do load.
                string mayRequire = li.Attribute("MayRequire")?.Value;
                if (mayRequire != null && !mayRequire.StartsWith("Ludeon.")) continue;
                if (JobEntry.TryDecode(li.Value.Trim(), out var entry)) entries.Add(entry);
                else throw new InvalidDataException($"unparseable entry '{li.Value}'");
            }
            roles.Add(new RoleXml(def.Element("defName")!.Value, entries));
        }
        return roles;
    }

    [Test]
    public async Task EveryColorRefResolvesInThePalette()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "WorkRoles.slnx")))
            dir = dir.Parent;
        var defsDir = Path.Combine(dir!.FullName, "mod", "1.6", "Defs");
        var palette = XElement.Load(Path.Combine(defsDir, "Palette.xml"))
            .Elements("WorkRoles.PaletteDef")
            .Select(d => d.Element("defName")!.Value)
            .ToHashSet();
        foreach (var def in XElement.Load(Path.Combine(defsDir, "Roles.xml"))
                     .Elements("WorkRoles.RoleDef"))
        {
            string colorRef = def.Element("colorRef")?.Value.Trim();
            if (colorRef == null) continue;
            await Assert.That(palette.Contains(colorRef)).IsTrue()
                .Because($"{def.Element("defName")?.Value}: unknown colorRef '{colorRef}'");
        }
    }

    [Test]
    public async Task EveryEntryExistsInVanilla()
    {
        foreach (var role in Roles)
        foreach (var entry in role.Entries)
        {
            var known = entry.Kind == JobEntryKind.WorkType
                ? VanillaWorkOrder.NaturalPriority.ContainsKey(entry.DefName)
                : GiverType.ContainsKey(entry.DefName);
            await Assert.That(known).IsTrue()
                .Because($"{role.DefName}: '{entry.DefName}' is not a vanilla {entry.Kind}");
        }
    }

    [Test]
    public async Task NoDuplicateEntriesWithinARole()
    {
        foreach (var role in Roles)
            await Assert.That(role.Entries.Distinct().Count()).IsEqualTo(role.Entries.Count)
                .Because($"{role.DefName} lists an entry twice");
    }

    /// Every vanilla work type — and therefore every work-tab giver — must be
    /// covered by the shipped roles. Givers without a work type are player-only
    /// actions and out of scope.
    [Test]
    public async Task RolesCoverEveryVanillaWorkTypeAndGiver()
    {
        var coveredTypes = Roles.SelectMany(r => r.Entries)
            .Where(e => e.Kind == JobEntryKind.WorkType)
            .Select(e => e.DefName)
            .ToHashSet();
        foreach (var type in VanillaWorkOrder.NaturalPriority.Keys)
            await Assert.That(coveredTypes).Contains(type)
                .Because($"no role covers work type {type}");

        var coveredGivers = Roles.SelectMany(r => r.Entries)
            .SelectMany(e => e.Kind == JobEntryKind.WorkType
                ? VanillaWorkOrder.GiversInOrder[e.DefName]
                : new[] { e.DefName })
            .ToHashSet();
        foreach (var giver in GiverType.Keys)
            await Assert.That(coveredGivers).Contains(giver)
                .Because($"no role covers giver {giver}");
    }
    
    /// The vanilla job catalog, for expanding role entries into coverage.
    private static readonly FakeCatalog JobCatalog = BuildJobCatalog();

    private static FakeCatalog BuildJobCatalog()
    {
        var catalog = new FakeCatalog();
        foreach (var group in VanillaGiverBaseline.GiverWorkType.GroupBy(kv => kv.Value))
            catalog.WithWorkType(group.Key, group.Select(kv => kv.Key).ToArray());
        return catalog;
    }

    /// The role tree's parent/child pairs rest on coverage subsets
    /// (CoverageMath.Covers): each child's expanded job set must sit strictly
    /// inside the parent's, however either role spells its entries.
    [Test]
    [Arguments("WS_Doctor", "WS_Medic")]
    [Arguments("WS_Doctor", "WS_Nurse")]
    [Arguments("WS_Doctor", "WS_Rescuer")]
    [Arguments("WS_Medic", "WS_Nurse")]
    [Arguments("WS_Medic", "WS_Rescuer")]
    [Arguments("WS_Warden", "WS_Jailor")]
    [Arguments("WS_Handler", "WS_Herder")]
    [Arguments("WS_Artist", "WS_Painter")]
    [Arguments("WS_Crafter", "WS_DrugMaker")]
    [Arguments("WS_Core", "WS_Rescuer")]
    [Arguments("WS_Core", "WS_Firefighter")]
    [Arguments("WS_Core", "WS_Patient")]
    [Arguments("WS_Basics", "WS_Bedrest")]
    [Arguments("WS_Basics", "WS_Laborer")]
    [Arguments("WS_Cook", "WS_Butcher")]
    [Arguments("WS_Cook", "WS_Brewer")]
    [Arguments("WS_Builder", "WS_Repairer")]
    [Arguments("WS_Smith", "WS_Fabricator")]
    [Arguments("WS_Farmer", "WS_Grower")]
    [Arguments("WS_Farmer", "WS_PlantCutter")]
    [Arguments("WS_Grunt", "WS_Hauler")]
    [Arguments("WS_Grunt", "WS_Cleaner")]
    public async Task ParentRolesCoverTheirChildren(string parent, string child)
    {
        var parentCoverage = CoverageMath.CoverageOf(Roles.Single(r => r.DefName == parent).Entries, JobCatalog);
        var childCoverage = CoverageMath.CoverageOf(Roles.Single(r => r.DefName == child).Entries, JobCatalog);
        await Assert.That(CoverageMath.Covers(parentCoverage, childCoverage)).IsTrue()
            .Because($"{child}'s coverage is not strictly inside {parent}'s");
    }
}
