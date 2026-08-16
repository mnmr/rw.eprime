using System.Xml.Linq;
using WorkRoles.Core.Tests.Planner;

namespace WorkRoles.Core.Tests.Persistence;

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
        List<MigrationRole> roles = [new(1, [WT("Doctor"), WG("TendPatients"), WG("FeedPatients")], false)];
        var plan = MigrationPlanner.Plan(roles, new Dictionary<string, int> { ["Doctor"] = 1 }, ["Doctor"], catalog);
        HashSet<int> assignments = [.. plan];

        await Assert.That(assignments.SetEquals([1])).IsTrue();
    }

    [Test]
    public async Task ComboIsUsedWhenAllCapableMembersShareOnePriority()
    {
        var catalog = new FakeCatalog().WithWorkType("Hauling", "HaulGeneral").WithWorkType("Cleaning", "CleanFilth");
        List<MigrationRole> roles = [new(1, [WT("Hauling"), WT("Cleaning")], false), new(2, [WT("Hauling")], false), new(3, [WT("Cleaning")], false)];
        List<string> order = ["Hauling", "Cleaning"];

        var shared = MigrationPlanner.Plan(roles, new Dictionary<string, int> { ["Hauling"] = 3, ["Cleaning"] = 3 }, order, catalog);
        HashSet<int> assignments = [.. shared];

        await Assert.That(assignments.SetEquals([1])).IsTrue();
    }

    [Test]
    public async Task SingleRolesAreUsedWhenComboMembersHaveDifferentPriorities()
    {
        var catalog = new FakeCatalog().WithWorkType("Hauling", "HaulGeneral").WithWorkType("Cleaning", "CleanFilth");
        List<MigrationRole> roles = [new(1, [WT("Hauling"), WT("Cleaning")], false), new(2, [WT("Hauling")], false), new(3, [WT("Cleaning")], false)];
        List<string> order = ["Hauling", "Cleaning"];

        var split = MigrationPlanner.Plan(roles, new Dictionary<string, int> { ["Hauling"] = 2, ["Cleaning"] = 4 }, order, catalog);
        HashSet<int> assignments = [.. split];

        await Assert.That(assignments.SetEquals([2, 3])).IsTrue();
    }

    // ----- Shipped-data round trip -----

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "mod", "1.6", "Defs")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }

    /// AllowTool's FinishingOff ships visible=false; migration's
    /// MigratableEntries drops invisible work types from role matching, so
    /// the fixture must too.
    private static readonly string[] InvisibleWorkTypes = ["FinishingOff"];

    private static List<MigrationRole> ShippedRoles(params string[] thirdPartyMods)
    {
        var path = Path.Combine(RepoRoot(), "mod", "1.6", "Defs", "Roles.xml");
        List<MigrationRole> roles = [];
        int id = 1;
        foreach (var def in XElement.Load(path).Elements("WorkRoles.RoleDef"))
        {
            List<JobEntry> entries = [];
            foreach (var li in def.Element("entries")?.Elements("li") ?? [])
            {
                // Third-party-gated entries load only for mods the scenario
                // declares installed; DLC gates (Ludeon.*) always load.
                string? mayRequire = li.Attribute("MayRequire")?.Value;
                if (mayRequire != null && !mayRequire.StartsWith("Ludeon.") && !thirdPartyMods.Contains(mayRequire))
                    continue;
                if (!JobEntry.TryDecode(li.Value.Trim(), out var entry))
                    continue;
                if (entry.Kind == JobEntryKind.WorkType && InvisibleWorkTypes.Contains(entry.DefName))
                    continue;
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
        Dictionary<string, int> priorities = [];
        for (int i = 0; i < workTypes.Count; i++)
            priorities[workTypes[i]] = priorityOfIndex(i);

        var plan = MigrationPlanner.Plan(roles, priorities, workTypes, catalog);
        var byId = roles.ToDictionary(r => r.Id);
        var compiled = JobOrderCompiler.Compile(plan.Select(id => (byId[id].Entries, false)), catalog, _ => true);

        // (a) Presence: every enabled work type survives; every disabled one is absent.
        foreach (var pair in priorities)
        {
            bool present = compiled.WorkTypePriorities.ContainsKey(pair.Key);
            if (pair.Value > 0)
                await Assert.That(present).IsTrue().Because($"{pair.Key} (priority {pair.Value}) was dropped by migration");
            else
                await Assert.That(present).IsFalse().Because($"{pair.Key} (priority 0) appeared after migration");
        }

        // (b) Ordering: a higher vanilla priority (lower number) ranks earlier.
        foreach (var a in priorities.Where(p => p.Value > 0))
        foreach (var b in priorities.Where(p => p.Value > 0))
            if (a.Value < b.Value)
                await Assert.That(compiled.WorkTypePriorities[a.Key] < compiled.WorkTypePriorities[b.Key]).IsTrue().Because($"{a.Key} (prio {a.Value}) should rank above {b.Key} (prio {b.Value})");
    }

    [Test]
    public async Task ShippedCatalogFlatGridRoundTrips() => await AssertGridRoundTrips(_ => 3);

    [Test]
    public async Task ShippedCatalogCyclicGridRoundTrips() => await AssertGridRoundTrips(i => i % 5); // 0-4: exercises singles + disabled

    [Test]
    public async Task ShippedCatalogAlternatingGridRoundTrips() => await AssertGridRoundTrips(i => i % 2 == 0 ? 1 : 4);

    // ----- AllowTool adoption scenarios -----
    // HaulingUrgent's only shipped carrier is the multi-type Basics role, so a
    // grid that ranks Haul+ apart from bed rest/basic exercises the relaxed
    // Basics match, the colony-majority entry reorder and carrier creation.

    private static FakeCatalog AllowToolCatalog() => BaselineCatalog().WithWorkType("HaulingUrgent", "HaulUrgently");

    private static Dictionary<string, int> Grid(List<string> workTypes, int haulPlus, int bedRest, int basic) =>
        workTypes.ToDictionary(
            t => t,
            t =>
                t switch
                {
                    "HaulingUrgent" => haulPlus,
                    "PatientBedRest" => bedRest,
                    "BasicWorker" => basic,
                    _ => 3,
                }
        );

    /// Models Seeding's adoption flow: colony-majority entry reorder, then
    /// per-pawn planning with carrier roles materialized once and reused.
    private static (List<List<int>> assignments, List<MigrationRole> roles, List<string> carriers) MigrateColony(
        List<MigrationRole> roles,
        FakeCatalog catalog,
        List<Dictionary<string, int>> grids,
        List<string> workTypes
    )
    {
        roles = [.. roles];
        var relaxed = MigrationPlanner.RelaxedMatchRoles(roles, catalog);
        var votes = grids.Cast<IReadOnlyDictionary<string, int>>().ToList();
        foreach (var (roleId, memberOrder) in MigrationPlanner.PreferredMemberOrders(roles, relaxed, votes))
        {
            int at = roles.FindIndex(r => r.Id == roleId);
            var entries = roles[at].Entries.ToList();
            List<int> memberSlots = [];
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Kind == JobEntryKind.WorkType && memberOrder.Contains(entries[i].DefName))
                    memberSlots.Add(i);
            for (int k = 0; k < memberSlots.Count; k++)
                entries[memberSlots[k]] = WT(memberOrder[k]);
            roles[at] = new MigrationRole(roleId, entries, roles[at].Excluded);
        }

        List<string> carriers = [];
        List<List<int>> assignments = [];
        int nextId = roles.Max(r => r.Id) + 1;
        foreach (var grid in grids)
        {
            var slots = MigrationPlanner.PlanSlots(roles, grid, workTypes, catalog, relaxed, t => catalog.WorkGiversOf(t).Count > 0);
            List<int> ids = [];
            foreach (var slot in slots)
            {
                if (slot.CarrierWorkType == null)
                {
                    ids.Add(slot.RoleId);
                    continue;
                }
                var carrier = new MigrationRole(nextId++, [WT(slot.CarrierWorkType)], false);
                roles.Add(carrier);
                carriers.Add(slot.CarrierWorkType);
                ids.Add(carrier.Id);
            }
            assignments.Add(ids);
        }
        return (assignments, roles, carriers);
    }

    private static CompiledOrder CompilePawn(List<int> ids, List<MigrationRole> roles, FakeCatalog catalog)
    {
        var byId = roles.ToDictionary(r => r.Id);
        return JobOrderCompiler.Compile(ids.Select(id => (byId[id].Entries, false)), catalog, _ => true);
    }

    [Test]
    public async Task AllowToolTunedColonyPrefersBasicsAndCreatesOneCarrier()
    {
        var roles = ShippedRoles("UnlimitedHugs.AllowTool");
        var catalog = AllowToolCatalog();
        var workTypes = GiverBearingWorkTypes(catalog, roles);
        int maxSeedId = roles.Max(r => r.Id);
        const int basicsRoleId = 3;
        const int laborerRoleId = 7;
        const int bedrestRoleId = 11;

        // Majority (3 pawns): Haul+ above bed rest above basic. Minority (2
        // pawns): bed rest above Haul+ — incompatible with any one Basics order.
        List<Dictionary<string, int>> grids = [Grid(workTypes, 1, 2, 3), Grid(workTypes, 1, 2, 3), Grid(workTypes, 1, 2, 3), Grid(workTypes, 2, 1, 3), Grid(workTypes, 2, 1, 3)];
        var (assignments, migrated, carriers) = MigrateColony(roles, catalog, grids, workTypes);

        await AssertMajorityPawn(assignments[0]);
        await AssertMajorityPawn(assignments[1]);
        await AssertMajorityPawn(assignments[2]);
        await AssertMinorityPawn(assignments[3]);
        await AssertMinorityPawn(assignments[4]);
        HashSet<string> createdCarriers = [.. carriers];
        await Assert.That(createdCarriers.SetEquals(["HaulingUrgent"])).IsTrue().Because("one carrier role serves every minority pawn");

        async Task AssertMajorityPawn(List<int> assignedRoleIds)
        {
            HashSet<int> assignedRoles = [.. assignedRoleIds];
            var compiled = CompilePawn(assignedRoleIds, migrated, catalog);

            await Assert.That(compiled.WorkTypePriorities.ContainsKey("HaulingUrgent")).IsTrue().Because("HaulingUrgent was dropped from a majority pawn during migration");
            await Assert.That(assignedRoles.Contains(basicsRoleId)).IsTrue();
            await Assert.That(assignedRoles.Contains(bedrestRoleId)).IsFalse();
            await Assert.That(assignedRoles.Contains(laborerRoleId)).IsFalse();
            await Assert.That(assignedRoles.Any(id => id > maxSeedId)).IsFalse().Because("a majority pawn must not need a carrier role");
            await Assert.That(compiled.WorkTypePriorities["HaulingUrgent"]).IsEqualTo(1);
            await Assert.That(compiled.WorkTypePriorities["HaulingUrgent"] < compiled.WorkTypePriorities["PatientBedRest"]).IsTrue().Because("Basics must be reordered to the majority order");
        }

        async Task AssertMinorityPawn(List<int> assignedRoleIds)
        {
            HashSet<int> assignedRoles = [.. assignedRoleIds];
            var compiled = CompilePawn(assignedRoleIds, migrated, catalog);

            await Assert.That(compiled.WorkTypePriorities.ContainsKey("HaulingUrgent")).IsTrue().Because("HaulingUrgent was dropped from a minority pawn during migration");
            await Assert.That(assignedRoles.Contains(basicsRoleId)).IsFalse();
            await Assert.That(assignedRoles.Contains(bedrestRoleId)).IsTrue();
            await Assert.That(assignedRoles.Contains(laborerRoleId)).IsTrue();
            await Assert.That(compiled.WorkTypePriorities["PatientBedRest"] < compiled.WorkTypePriorities["HaulingUrgent"]).IsTrue().Because("bed rest must outrank Haul+");
            await Assert.That(compiled.WorkTypePriorities["HaulingUrgent"] < compiled.WorkTypePriorities["BasicWorker"]).IsTrue().Because("Haul+ must outrank basic work");
        }
    }

    [Test]
    public async Task AllowToolDefaultFlatColonyKeepsBasicsOrderWithoutExtraRoles()
    {
        var roles = ShippedRoles("UnlimitedHugs.AllowTool");
        var catalog = AllowToolCatalog();
        var workTypes = GiverBearingWorkTypes(catalog, roles);
        const int basicsRoleId = 3;

        List<Dictionary<string, int>> grids = [Grid(workTypes, 3, 3, 3), Grid(workTypes, 3, 3, 3), Grid(workTypes, 3, 3, 3)];
        var (assignments, migrated, carriers) = MigrateColony(roles, catalog, grids, workTypes);

        await Assert.That(carriers).IsEmpty().Because("a flat grid matches Basics strictly; nothing to generate");
        string actualEntryOrder = string.Join(",", migrated.Single(role => role.Id == basicsRoleId).Entries.Select(entry => entry.DefName));
        await Assert.That(actualEntryOrder).IsEqualTo("PatientBedRest,HaulingUrgent,BasicWorker").Because("the default order already matches every colonist");
        HashSet<int> firstPawnRoles = [.. assignments[0]];
        HashSet<int> secondPawnRoles = [.. assignments[1]];
        HashSet<int> thirdPawnRoles = [.. assignments[2]];
        await Assert.That(firstPawnRoles.Contains(basicsRoleId)).IsTrue();
        await Assert.That(secondPawnRoles.Contains(basicsRoleId)).IsTrue();
        await Assert.That(thirdPawnRoles.Contains(basicsRoleId)).IsTrue();
    }
}
