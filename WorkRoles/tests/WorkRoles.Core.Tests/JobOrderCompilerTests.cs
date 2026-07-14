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
    public async Task MovedSnapshotGiversStayInTheRole()
    {
        // The catalog reflects the CURRENT state: TendAnimals was moved out of
        // Doctor into Veterinary by a mod. The role's snapshot remembers it.
        var catalog = new FakeCatalog()
            .WithWorkType("Doctor", "TendPatients")
            .WithWorkType("Veterinary", "TendAnimals");
        var entries = new List<JobEntry> { WT("Doctor") };
        var snapshot = new Dictionary<string, List<string>>
        {
            ["Doctor"] = new List<string> { "TendPatients", "TendAnimals", "RemovedByMod" },
        };
        var expanded = JobOrderCompiler.WithMovedSnapshotGivers(entries, snapshot, catalog);
        // Still-member TendPatients expands via the work type (no duplicate entry);
        // moved TendAnimals becomes an explicit giver; missing defs are skipped.
        var result = JobOrderCompiler.Compile(Roles(expanded.ToArray()), catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("TendPatients,TendAnimals");
    }

    [Test]
    public async Task BlockerClaimsJobsFirst_LaterRolesCannotAddThem()
    {
        var catalog = new FakeCatalog()
            .WithWorkType("Firefighter", "FightFires")
            .WithWorkType("Hauling", "HaulGeneral");
        // Blocker vetoes Firefighter; the later role still provides Hauling.
        var roles = new List<(IReadOnlyList<JobEntry> entries, bool blocker)>
        {
            (new List<JobEntry> { WT("Firefighter") }, true),
            (new List<JobEntry> { WT("Firefighter"), WT("Hauling") }, false),
        };
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("HaulGeneral");
        await Assert.That(result.WorkTypePriorities.ContainsKey("Firefighter")).IsFalse();
    }

    [Test]
    public async Task RoleAboveBlockerStillProvidesItsJobs()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients");
        // Earlier roles win: the provider above the blocker keeps the job.
        var roles = new List<(IReadOnlyList<JobEntry> entries, bool blocker)>
        {
            (new List<JobEntry> { WT("Doctor") }, false),
            (new List<JobEntry> { WT("Doctor") }, true),
        };
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("TendPatients");
    }

    [Test]
    public async Task BlockerCanVetoSingleJobWhileTypeStaysAvailable()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients", "FeedPatients");
        var roles = new List<(IReadOnlyList<JobEntry> entries, bool blocker)>
        {
            (new List<JobEntry> { WG("FeedPatients") }, true),
            (new List<JobEntry> { WT("Doctor") }, false),
        };
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("TendPatients");
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

    /// Vanilla replays priority numbers ascending, each left-to-right over the
    /// Work-tab columns; replaying the projected numbers must reproduce the
    /// internal rank order.
    private static string Replay(Dictionary<string, int> buckets, Dictionary<string, int> columns)
        => string.Join(",", buckets.OrderBy(kv => kv.Value).ThenBy(kv => columns[kv.Key]).Select(kv => kv.Key));

    [Test]
    public async Task VanillaProjectionReplaysInternalOrder()
    {
        var columns = new Dictionary<string, int> { ["A"] = 0, ["B"] = 1, ["C"] = 2, ["D"] = 3 };
        var ranks = new Dictionary<string, int> { ["B"] = 1, ["A"] = 2, ["D"] = 3, ["C"] = 4 };
        var buckets = JobOrderCompiler.ToVanillaPriorities(ranks, n => columns[n]);
        await Assert.That(buckets["B"]).IsEqualTo(1);
        await Assert.That(buckets["A"]).IsEqualTo(2);  // left of B: needs a later number
        await Assert.That(buckets["D"]).IsEqualTo(2);  // right of A: same number keeps order
        await Assert.That(buckets["C"]).IsEqualTo(3);  // left of D again
        await Assert.That(Replay(buckets, columns)).IsEqualTo("B,A,D,C");
    }

    [Test]
    public async Task VanillaProjectionInColumnOrderStaysAtOne()
    {
        var columns = new Dictionary<string, int> { ["A"] = 0, ["B"] = 1, ["C"] = 2, ["D"] = 3 };
        var ranks = new Dictionary<string, int> { ["A"] = 1, ["B"] = 2, ["C"] = 3, ["D"] = 4 };
        var buckets = JobOrderCompiler.ToVanillaPriorities(ranks, n => columns[n]);
        await Assert.That(buckets.Values.Distinct().Single()).IsEqualTo(1);
        await Assert.That(Replay(buckets, columns)).IsEqualTo("A,B,C,D");
    }

    [Test]
    public async Task VanillaProjectionSaturatesAtFour()
    {
        // Full reverse of column order needs one number per type; the tail
        // beyond four lumps into 4 (order there falls back to column order).
        var columns = new Dictionary<string, int>
            { ["A"] = 0, ["B"] = 1, ["C"] = 2, ["D"] = 3, ["E"] = 4 };
        var ranks = new Dictionary<string, int>
            { ["E"] = 1, ["D"] = 2, ["C"] = 3, ["B"] = 4, ["A"] = 5 };
        var buckets = JobOrderCompiler.ToVanillaPriorities(ranks, n => columns[n]);
        await Assert.That(buckets["E"]).IsEqualTo(1);
        await Assert.That(buckets["D"]).IsEqualTo(2);
        await Assert.That(buckets["C"]).IsEqualTo(3);
        await Assert.That(buckets["B"]).IsEqualTo(4);
        await Assert.That(buckets["A"]).IsEqualTo(4);
    }

    [Test]
    public async Task VanillaProjectionOfNothingIsEmpty()
    {
        var buckets = JobOrderCompiler.ToVanillaPriorities(new Dictionary<string, int>(), _ => 0);
        await Assert.That(buckets).IsEmpty();
    }

    [Test]
    public async Task VanillaProjectionPinsBasicsToOneWhenLumping()
    {
        // P2,P1 head then a fully reversed tail: five direction changes, so the
        // plain pass lumps W and Z together at 4 in the wrong replay order.
        var columns = new Dictionary<string, int>
            { ["P2"] = 0, ["P1"] = 1, ["W"] = 2, ["Z"] = 3, ["Y"] = 4, ["X"] = 5 };
        var ranks = new Dictionary<string, int>
            { ["P1"] = 1, ["P2"] = 2, ["X"] = 3, ["Y"] = 4, ["Z"] = 5, ["W"] = 6 };
        var categories = new VanillaProjectionCategories { Basics = ["P1", "P2"] };

        var buckets = JobOrderCompiler.ToVanillaPriorities(ranks, n => columns[n], categories);
        // Pinning the head to 1 frees the numbers the tail needs.
        await Assert.That(buckets["P1"]).IsEqualTo(1);
        await Assert.That(buckets["P2"]).IsEqualTo(1);
        await Assert.That(buckets["X"]).IsEqualTo(1);  // right of the pinned block
        await Assert.That(buckets["Y"]).IsEqualTo(2);
        await Assert.That(buckets["Z"]).IsEqualTo(3);
        await Assert.That(buckets["W"]).IsEqualTo(4);
    }

    [Test]
    public async Task VanillaProjectionSpreadsSpareNumbersByCategory()
    {
        // Everything in column order collapses to all-1s; the spread bumps
        // skilled work (and after) to 2, grunt (and after) to 3, research to 4.
        var columns = new Dictionary<string, int>
            { ["A"] = 0, ["B"] = 1, ["C"] = 2, ["D"] = 3, ["E"] = 4 };
        var ranks = new Dictionary<string, int>
            { ["A"] = 1, ["B"] = 2, ["C"] = 3, ["D"] = 4, ["E"] = 5 };
        var categories = new VanillaProjectionCategories
        {
            Basics = ["A", "B"],
            Skilled = ["C", "E"],
            Grunt = ["D"],
            Research = ["E"],
        };

        var buckets = JobOrderCompiler.ToVanillaPriorities(ranks, n => columns[n], categories);
        await Assert.That(buckets["A"]).IsEqualTo(1);
        await Assert.That(buckets["B"]).IsEqualTo(1);
        await Assert.That(buckets["C"]).IsEqualTo(2);
        await Assert.That(buckets["D"]).IsEqualTo(3);
        await Assert.That(buckets["E"]).IsEqualTo(4);
    }

    [Test]
    public async Task VanillaProjectionSpreadStopsWhenFourIsReached()
    {
        // The skilled bump alone reaches 4: grunt and research stay untouched.
        var columns = new Dictionary<string, int>
            { ["A"] = 0, ["B"] = 1, ["C"] = 2, ["D"] = 3 };
        var ranks = new Dictionary<string, int>
            { ["A"] = 1, ["C"] = 2, ["B"] = 3, ["D"] = 4 };  // one inversion: B left of C
        var categories = new VanillaProjectionCategories
        {
            Skilled = ["A"],
            Grunt = ["D"],
            Research = ["D"],
        };

        // Base pass: A=1, C=1, B=2, D=2. Skilled bump from A shifts everything: 2,2,3,3.
        // Still no 4 -> grunt bump from D: D=4. Research untouched after that.
        var buckets = JobOrderCompiler.ToVanillaPriorities(ranks, n => columns[n], categories);
        await Assert.That(buckets["A"]).IsEqualTo(2);
        await Assert.That(buckets["C"]).IsEqualTo(2);
        await Assert.That(buckets["B"]).IsEqualTo(3);
        await Assert.That(buckets["D"]).IsEqualTo(4);
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
