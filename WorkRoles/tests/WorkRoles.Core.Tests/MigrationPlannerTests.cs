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

    /// AllowTool's FinishingOff ships visible=false; migration's
    /// MigratableEntries drops invisible work types from role matching, so
    /// the fixture must too.
    private static readonly string[] InvisibleWorkTypes = { "FinishingOff" };

    private static List<MigrationRole> ShippedRoles(params string[] thirdPartyMods)
    {
        var path = Path.Combine(RepoRoot(), "mod", "1.6", "Defs", "Roles.xml");
        var roles = new List<MigrationRole>();
        int id = 1;
        foreach (var def in XElement.Load(path).Elements("WorkRoles.RoleDef"))
        {
            var entries = new List<JobEntry>();
            foreach (var li in def.Element("entries")?.Elements("li") ?? Enumerable.Empty<XElement>())
            {
                // Third-party-gated entries load only for mods the scenario
                // declares installed; DLC gates (Ludeon.*) always load.
                string mayRequire = li.Attribute("MayRequire")?.Value;
                if (mayRequire != null && !mayRequire.StartsWith("Ludeon.")
                    && !thirdPartyMods.Contains(mayRequire)) continue;
                if (!JobEntry.TryDecode(li.Value.Trim(), out var entry)) continue;
                if (entry.Kind == JobEntryKind.WorkType
                    && InvisibleWorkTypes.Contains(entry.DefName)) continue;
                entries.Add(entry);
            }
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

    // ----- AllowTool adoption scenarios -----
    // HaulingUrgent's only shipped carrier is the multi-type Basics role, so a
    // grid that ranks Haul+ apart from bed rest/basic exercises the relaxed
    // Basics match, the colony-majority entry reorder and carrier creation.

    private static FakeCatalog AllowToolCatalog() =>
        BaselineCatalog().WithWorkType("HaulingUrgent", "HaulUrgently");

    private static int SingleTypeRoleId(List<MigrationRole> roles, string workType) =>
        roles.Single(r => r.Entries.Count == 1
            && r.Entries[0].Kind == JobEntryKind.WorkType
            && r.Entries[0].DefName == workType).Id;

    private static int BasicsRoleId(List<MigrationRole> roles) =>
        roles.Single(r =>
            r.Entries.Any(e => e.Kind == JobEntryKind.WorkType && e.DefName == "HaulingUrgent")
            && r.Entries.Any(e => e.Kind == JobEntryKind.WorkType && e.DefName == "BasicWorker")).Id;

    private static Dictionary<string, int> Grid(
        List<string> workTypes, int haulPlus, int bedRest, int basic) =>
        workTypes.ToDictionary(t => t, t => t switch
        {
            "HaulingUrgent" => haulPlus,
            "PatientBedRest" => bedRest,
            "BasicWorker" => basic,
            _ => 3,
        });

    /// Models Seeding's adoption flow: colony-majority entry reorder, then
    /// per-pawn planning with carrier roles materialized once and reused.
    private static (List<List<int>> assignments, List<MigrationRole> roles, List<string> carriers)
        MigrateColony(List<MigrationRole> roles, FakeCatalog catalog,
            List<Dictionary<string, int>> grids, List<string> workTypes)
    {
        roles = new List<MigrationRole>(roles);
        var relaxed = MigrationPlanner.RelaxedMatchRoles(roles, catalog);
        var votes = grids.Cast<IReadOnlyDictionary<string, int>>().ToList();
        foreach (var (roleId, memberOrder) in
                 MigrationPlanner.PreferredMemberOrders(roles, relaxed, votes))
        {
            int at = roles.FindIndex(r => r.Id == roleId);
            var entries = roles[at].Entries.ToList();
            var memberSlots = new List<int>();
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Kind == JobEntryKind.WorkType
                    && memberOrder.Contains(entries[i].DefName)) memberSlots.Add(i);
            for (int k = 0; k < memberSlots.Count; k++)
                entries[memberSlots[k]] = WT(memberOrder[k]);
            roles[at] = new MigrationRole(roleId, entries, roles[at].Excluded);
        }

        var carriers = new List<string>();
        var assignments = new List<List<int>>();
        int nextId = roles.Max(r => r.Id) + 1;
        foreach (var grid in grids)
        {
            var slots = MigrationPlanner.PlanSlots(roles, grid, workTypes, catalog,
                relaxed, t => catalog.WorkGiversOf(t).Count > 0);
            var ids = new List<int>();
            foreach (var slot in slots)
            {
                if (slot.CarrierWorkType == null)
                {
                    ids.Add(slot.RoleId);
                    continue;
                }
                var carrier = new MigrationRole(nextId++,
                    new List<JobEntry> { WT(slot.CarrierWorkType) }, false);
                roles.Add(carrier);
                carriers.Add(slot.CarrierWorkType);
                ids.Add(carrier.Id);
            }
            assignments.Add(ids);
        }
        return (assignments, roles, carriers);
    }

    private static CompiledOrder CompilePawn(
        List<int> ids, List<MigrationRole> roles, FakeCatalog catalog)
    {
        var byId = roles.ToDictionary(r => r.Id);
        return JobOrderCompiler.Compile(
            ids.Select(id => (byId[id].Entries, false)), catalog, _ => true);
    }

    [Test]
    public async Task AllowTool_TunedColony_PrefersBasicsAndCreatesOneCarrier()
    {
        var roles = ShippedRoles("UnlimitedHugs.AllowTool");
        var catalog = AllowToolCatalog();
        var workTypes = GiverBearingWorkTypes(catalog, roles);
        int maxSeedId = roles.Max(r => r.Id);
        int basics = BasicsRoleId(roles);
        int bedrest = SingleTypeRoleId(roles, "PatientBedRest");
        int laborer = SingleTypeRoleId(roles, "BasicWorker");

        // Majority (3 pawns): Haul+ above bed rest above basic. Minority (2
        // pawns): bed rest above Haul+ — incompatible with any one Basics order.
        var grids = new List<Dictionary<string, int>>
        {
            Grid(workTypes, 1, 2, 3),
            Grid(workTypes, 1, 2, 3),
            Grid(workTypes, 1, 2, 3),
            Grid(workTypes, 2, 1, 3),
            Grid(workTypes, 2, 1, 3),
        };
        var (assignments, migrated, carriers) = MigrateColony(roles, catalog, grids, workTypes);

        for (int pawn = 0; pawn < 3; pawn++)
        {
            var compiled = CompilePawn(assignments[pawn], migrated, catalog);
            await Assert.That(compiled.WorkTypePriorities.ContainsKey("HaulingUrgent")).IsTrue()
                .Because($"pawn {pawn}: HaulingUrgent (priority 1) was dropped by migration");
            // Basics alone carries the trio: no singles, no generated carrier.
            await Assert.That(assignments[pawn]).Contains(basics);
            await Assert.That(assignments[pawn]).DoesNotContain(bedrest);
            await Assert.That(assignments[pawn]).DoesNotContain(laborer);
            await Assert.That(assignments[pawn].Any(id => id > maxSeedId)).IsFalse()
                .Because($"pawn {pawn}: a majority pawn must not need a carrier role");
            // The reordered Basics leads with Haul+, so it keeps its top rank.
            await Assert.That(compiled.WorkTypePriorities["HaulingUrgent"])
                .IsEqualTo(compiled.WorkTypePriorities.Values.Min())
                .Because($"pawn {pawn}: Haul+ was the grid's highest priority");
            await Assert.That(compiled.WorkTypePriorities["HaulingUrgent"]
                    < compiled.WorkTypePriorities["PatientBedRest"]).IsTrue()
                .Because($"pawn {pawn}: Basics must be reordered to the majority order");
        }

        for (int pawn = 3; pawn < 5; pawn++)
        {
            var compiled = CompilePawn(assignments[pawn], migrated, catalog);
            await Assert.That(compiled.WorkTypePriorities.ContainsKey("HaulingUrgent")).IsTrue()
                .Because($"pawn {pawn}: HaulingUrgent (priority 2) was dropped by migration");
            // Incompatible order: exact split via singles plus the carrier.
            await Assert.That(assignments[pawn]).DoesNotContain(basics);
            await Assert.That(assignments[pawn]).Contains(bedrest);
            await Assert.That(assignments[pawn]).Contains(laborer);
            await Assert.That(compiled.WorkTypePriorities["PatientBedRest"]
                    < compiled.WorkTypePriorities["HaulingUrgent"]).IsTrue()
                .Because($"pawn {pawn}: bed rest (1) outranks Haul+ (2)");
            await Assert.That(compiled.WorkTypePriorities["HaulingUrgent"]
                    < compiled.WorkTypePriorities["BasicWorker"]).IsTrue()
                .Because($"pawn {pawn}: Haul+ (2) outranks basic (3)");
        }

        await Assert.That(string.Join(",", carriers)).IsEqualTo("HaulingUrgent")
            .Because("one carrier role serves every minority pawn");
    }

    [Test]
    public async Task AllowTool_DefaultFlatColony_KeepsBasicsOrderWithoutExtraRoles()
    {
        var roles = ShippedRoles("UnlimitedHugs.AllowTool");
        var catalog = AllowToolCatalog();
        var workTypes = GiverBearingWorkTypes(catalog, roles);
        int basics = BasicsRoleId(roles);
        var originalOrder = roles.Single(r => r.Id == basics).Entries
            .Select(e => e.DefName).ToList();

        var grids = new List<Dictionary<string, int>>
        {
            Grid(workTypes, 3, 3, 3),
            Grid(workTypes, 3, 3, 3),
            Grid(workTypes, 3, 3, 3),
        };
        var (assignments, migrated, carriers) = MigrateColony(roles, catalog, grids, workTypes);

        await Assert.That(carriers).IsEmpty()
            .Because("a flat grid matches Basics strictly; nothing to generate");
        await Assert.That(string.Join(",", migrated.Single(r => r.Id == basics).Entries
                .Select(e => e.DefName)))
            .IsEqualTo(string.Join(",", originalOrder))
            .Because("the default order already matches every colonist");
        foreach (var ids in assignments)
            await Assert.That(ids).Contains(basics);
    }
}
