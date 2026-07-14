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
        foreach (var def in XElement.Load(path).Elements())
        {
            var entries = new List<JobEntry>();
            foreach (var li in def.Element("entries")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                if (JobEntry.TryDecode(li.Value.Trim(), out var entry)) entries.Add(entry);
                else throw new InvalidDataException($"unparseable entry '{li.Value}'");
            }
            roles.Add(new RoleXml(def.Element("defName")!.Value, entries));
        }
        return roles;
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
    
    /// The role tree's parent/child pairs rest on literal entry subsets
    /// (EntryMath.Covers): each child's entries must appear verbatim in the parent.
    [Test]
    [Arguments("WS_Doctor", "WS_Medic")]
    [Arguments("WS_Doctor", "WS_Rescuer")]
    [Arguments("WS_Medic", "WS_Rescuer")]
    [Arguments("WS_Basics", "WS_Rescuer")]
    [Arguments("WS_Basics", "WS_Firefighter")]
    [Arguments("WS_Basics", "WS_Patient")]
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
        var parentEntries = Roles.Single(r => r.DefName == parent).Entries;
        var childEntries = Roles.Single(r => r.DefName == child).Entries;
        await Assert.That(EntryMath.Covers(parentEntries, childEntries)).IsTrue()
            .Because($"{child} is not a literal subset of {parent}");
    }
}
