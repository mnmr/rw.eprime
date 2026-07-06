using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class JobOrderCompilerTests
{
    internal static JobEntry WT(string defName) => new(JobEntryKind.WorkType, defName);
    internal static JobEntry WG(string defName) => new(JobEntryKind.WorkGiver, defName);
    internal static List<IReadOnlyList<JobEntry>> Roles(params JobEntry[][] roles) =>
        roles.Select(r => (IReadOnlyList<JobEntry>)r.ToList()).ToList();
    internal static string Flat(IEnumerable<string> givers) => string.Join(",", givers);

    [Test]
    public async Task WorkTypeEntryExpandsToItsGiversInCatalogOrder()
    {
        var catalog = new FakeCatalog().WithWorkType("Hauling", "HaulCorpses", "HaulGeneral");
        var result = JobOrderCompiler.Compile(Roles(new[] { WT("Hauling") }), catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("HaulCorpses,HaulGeneral");
    }

    [Test]
    public async Task JobEntryCodecRoundTrips()
    {
        var wt = WT("Hauling");
        var wg = WG("HaulGeneral");
        // The encoded format is a save-file contract — changing it breaks existing saves.
        await Assert.That(wt.Encode()).IsEqualTo("WorkType:Hauling");
        await Assert.That(wg.Encode()).IsEqualTo("WorkGiver:HaulGeneral");
        bool wtOk = JobEntry.TryDecode(wt.Encode(), out var wt2)
            && wt2.Kind == JobEntryKind.WorkType && wt2.DefName == "Hauling";
        bool wgOk = JobEntry.TryDecode(wg.Encode(), out var wg2)
            && wg2.Kind == JobEntryKind.WorkGiver && wg2.DefName == "HaulGeneral";
        bool garbageOk = !JobEntry.TryDecode("garbage", out _);
        await Assert.That(wtOk).IsTrue();
        await Assert.That(wgOk).IsTrue();
        await Assert.That(garbageOk).IsTrue();
    }

    [Test]
    public async Task RolesConcatenateInOrder_FirstMentionWins()
    {
        var catalog = new FakeCatalog()
            .WithWorkType("Doctor", "TendPatients", "FeedPatients")
            .WithWorkType("Hauling", "HaulGeneral");
        // Role 1: Doctor (whole type). Role 2: Hauling + TendPatients again (dup, ignored).
        var roles = Roles(
            new[] { WT("Doctor") },
            new[] { WT("Hauling"), WG("TendPatients") });
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("TendPatients,FeedPatients,HaulGeneral");
    }

    [Test]
    public async Task LeafBeforeParent_LeafKeepsItsEarlierPosition()
    {
        var catalog = new FakeCatalog()
            .WithWorkType("Crafting", "MakeStoneBlocks", "Smelt", "MakeDrugs");
        // "Smelt first, then the rest of Crafting" inside one role.
        var roles = Roles(new[] { WG("Smelt"), WT("Crafting") });
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("Smelt,MakeStoneBlocks,MakeDrugs");
    }

    [Test]
    public async Task JobInNoRoleIsAbsent()
    {
        var catalog = new FakeCatalog()
            .WithWorkType("Doctor", "TendPatients")
            .WithWorkType("Cleaning", "CleanFilth");
        var roles = Roles(new[] { WT("Doctor") });
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(result.AllInOrder.Contains("CleanFilth")).IsFalse();
    }

    [Test]
    public async Task IncapableJobsAreDropped()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients", "FeedPatients");
        var roles = Roles(new[] { WT("Doctor") });
        var result = JobOrderCompiler.Compile(roles, catalog, g => g != "TendPatients");
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("FeedPatients");
    }

    [Test]
    public async Task MissingDefsAreInert()
    {
        var catalog = new FakeCatalog().WithWorkType("Hauling", "HaulGeneral");
        // Unknown work type and unknown workgiver (e.g. a removed mod) contribute nothing.
        var roles = Roles(new[] { WT("ModdedType"), WG("ModdedGiver"), WT("Hauling") });
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("HaulGeneral");
    }

    [Test]
    public async Task EmptyRolesProduceEmptyOrder()
    {
        var catalog = new FakeCatalog().WithWorkType("Hauling", "HaulGeneral");
        var result = JobOrderCompiler.Compile(Roles(), catalog, _ => true);
        await Assert.That(result.AllInOrder.Count).IsEqualTo(0);
        await Assert.That(result.Normal.Count).IsEqualTo(0);
        await Assert.That(result.Emergency.Count).IsEqualTo(0);
    }

    [Test]
    public async Task RanksFollowFirstGiverOrder()
    {
        var catalog = new FakeCatalog()
            .WithWorkType("A", "a1")
            .WithWorkType("B", "b1")
            .WithWorkType("C", "c1")
            .WithWorkType("D", "d1");
        var roles = Roles(new[] { WT("A"), WT("B"), WT("C"), WT("D") });
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        // Work types rank 1..N in order of first appearance.
        await Assert.That(result.WorkTypePriorities["A"]).IsEqualTo(1);
        await Assert.That(result.WorkTypePriorities["B"]).IsEqualTo(2);
        await Assert.That(result.WorkTypePriorities["C"]).IsEqualTo(3);
        await Assert.That(result.WorkTypePriorities["D"]).IsEqualTo(4);
    }

    [Test]
    public async Task RanksAreConsecutivePerWorkTypeNotPositionBased()
    {
        // Doctor's first giver sits at list index 7, but it's only the SECOND work type,
        // so its rank is 2 — ranks count work types, not giver positions.
        var catalog = new FakeCatalog()
            .WithWorkType("Hauling", "HaulGeneral", "h2", "h3", "h4", "h5", "h6", "h7")
            .WithWorkType("Doctor", "TendPatients");
        var roles = Roles(new[] { WT("Hauling"), WT("Doctor") });
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(result.WorkTypePriorities["Hauling"]).IsEqualTo(1);
        await Assert.That(result.WorkTypePriorities["Doctor"]).IsEqualTo(2);
    }

    [Test]
    public async Task AbsentWorkTypeHasNoRank()
    {
        var catalog = new FakeCatalog().WithWorkType("A", "a1").WithWorkType("B", "b1");
        var roles = Roles(new[] { WT("A") });
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(result.WorkTypePriorities.ContainsKey("B")).IsFalse();
    }

    [Test]
    public async Task WorkTypeRankComesFromItsFirstGiverEvenIfSplitAcrossRoles()
    {
        var catalog = new FakeCatalog()
            .WithWorkType("Crafting", "Smelt", "MakeDrugs")
            .WithWorkType("Hauling", "HaulGeneral");
        // Smelt is first overall; the rest of Crafting lands later — rank uses Smelt's position.
        var roles = Roles(new[] { WG("Smelt") }, new[] { WT("Hauling"), WT("Crafting") });
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(result.WorkTypePriorities["Crafting"]).IsEqualTo(1);
    }

    [Test]
    public async Task EmergencyFlaggedGiversAlwaysGoToEmergencyList()
    {
        var catalog = new FakeCatalog()
            .WithWorkType("Firefighter", "FightFires")
            .WithWorkType("Doctor", "TendEmergency", "TendPatients")
            .WithWorkType("Hauling", "HaulGeneral")
            .WithEmergency("FightFires", "TendEmergency");
        var roles = Roles(new[] { WT("Firefighter"), WT("Doctor") }, new[] { WT("Hauling") });
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        // Emergency membership follows the def flag alone; both lists preserve compiled order.
        await Assert.That(Flat(result.Emergency)).IsEqualTo("FightFires,TendEmergency");
        await Assert.That(Flat(result.Normal)).IsEqualTo("TendPatients,HaulGeneral");
    }

    [Test]
    public async Task EmergencyGiversFromLateRolesStillGoToEmergencyList()
    {
        // Doctor is the pawn's LAST role — its emergency job still jumps to the
        // emergency list (role membership decides participation, not position).
        var catalog = new FakeCatalog()
            .WithWorkType("Hauling", "HaulGeneral", "h2", "h3")
            .WithWorkType("Doctor", "TendEmergency")
            .WithEmergency("TendEmergency");
        var roles = Roles(new[] { WT("Hauling") }, new[] { WT("Doctor") });
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(Flat(result.Emergency)).IsEqualTo("TendEmergency");
        await Assert.That(Flat(result.Normal)).IsEqualTo("HaulGeneral,h2,h3");
    }

    [Test]
    public async Task VanillaProjectionBucketsRanksIntoQuartiles()
    {
        // 8 ranked work types -> ranks 1,2 => 1; 3,4 => 2; 5,6 => 3; 7,8 => 4.
        await Assert.That(JobOrderCompiler.ToVanillaPriority(1, 8)).IsEqualTo(1);
        await Assert.That(JobOrderCompiler.ToVanillaPriority(2, 8)).IsEqualTo(1);
        await Assert.That(JobOrderCompiler.ToVanillaPriority(3, 8)).IsEqualTo(2);
        await Assert.That(JobOrderCompiler.ToVanillaPriority(8, 8)).IsEqualTo(4);
        // Fewer than four ranked types spread from 1 without reaching 4.
        await Assert.That(JobOrderCompiler.ToVanillaPriority(1, 2)).IsEqualTo(1);
        await Assert.That(JobOrderCompiler.ToVanillaPriority(2, 2)).IsEqualTo(3);
        // Single ranked type is priority 1; invalid input is 0.
        await Assert.That(JobOrderCompiler.ToVanillaPriority(1, 1)).IsEqualTo(1);
        await Assert.That(JobOrderCompiler.ToVanillaPriority(0, 5)).IsEqualTo(0);
    }

    [Test]
    public async Task EmergencyOnlyRolesYieldEmptyNormalList()
    {
        var catalog = new FakeCatalog()
            .WithWorkType("Firefighter", "FightFires")
            .WithEmergency("FightFires");
        var roles = Roles(new[] { WT("Firefighter") });
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(Flat(result.Emergency)).IsEqualTo("FightFires");
        await Assert.That(result.Normal.Count).IsEqualTo(0);
    }
}
