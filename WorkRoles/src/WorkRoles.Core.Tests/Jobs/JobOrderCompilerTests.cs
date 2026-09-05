using WorkRoles.Core.Tests.Planner;

namespace WorkRoles.Core.Tests.Jobs;

public class JobOrderCompilerTests
{
    internal static JobEntry WT(string defName) => new(JobEntryKind.WorkType, defName);

    internal static JobEntry WG(string defName) => new(JobEntryKind.WorkGiver, defName);

    internal static List<IReadOnlyList<JobEntry>> Roles(params JobEntry[][] roles) => roles.Select(r => (IReadOnlyList<JobEntry>)[.. r]).ToList();

    internal static string Flat(IEnumerable<string> givers) => string.Join(",", givers);

    [Test]
    public async Task WorkTypeEntryExpandsToItsGiversInCatalogOrder()
    {
        var catalog = new FakeCatalog().WithWorkType("Hauling", "HaulCorpses", "HaulGeneral");
        var result = JobOrderCompiler.Compile(Roles([WT("Hauling")]), catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("HaulCorpses,HaulGeneral");
    }

    [Test]
    public async Task WorkTypeJobEntryCodecRoundTrips()
    {
        var wt = WT("Hauling");
        await Assert.That(wt.Encode()).IsEqualTo("WorkType:Hauling");
        bool wtOk = JobEntry.TryDecode(wt.Encode(), out var wt2) && wt2.Kind == JobEntryKind.WorkType && wt2.DefName == "Hauling";

        await Assert.That(wtOk).IsTrue();
    }

    [Test]
    public async Task WorkGiverJobEntryCodecRoundTrips()
    {
        var wg = WG("HaulGeneral");
        await Assert.That(wg.Encode()).IsEqualTo("WorkGiver:HaulGeneral");
        bool wgOk = JobEntry.TryDecode(wg.Encode(), out var wg2) && wg2.Kind == JobEntryKind.WorkGiver && wg2.DefName == "HaulGeneral";

        await Assert.That(wgOk).IsTrue();
    }

    [Test]
    public async Task JobEntryCodecRejectsGarbage()
    {
        await Assert.That(JobEntry.TryDecode("garbage", out _)).IsFalse();
    }

    [Test]
    public async Task MovedSnapshotGiversStayInTheRole()
    {
        // The catalog reflects the CURRENT state: TendAnimals was moved out of
        // Doctor into Veterinary by a mod. The role's snapshot remembers it.
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients").WithWorkType("Veterinary", "TendAnimals");
        List<JobEntry> entries = [WT("Doctor")];
        Dictionary<string, List<string>> snapshot = new() { ["Doctor"] = ["TendPatients", "TendAnimals", "RemovedByMod"] };
        var expanded = JobOrderCompiler.WithMovedSnapshotGivers(entries, snapshot, catalog);
        // Still-member TendPatients expands via the work type (no duplicate entry);
        // moved TendAnimals becomes an explicit giver; missing defs are skipped.
        var result = JobOrderCompiler.Compile(Roles(expanded.ToArray()), catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("TendPatients,TendAnimals");
    }

    [Test]
    public async Task BlockerClaimsJobsFirstAndLaterRolesCannotAddThem()
    {
        var catalog = new FakeCatalog().WithWorkType("Firefighter", "FightFires").WithWorkType("Hauling", "HaulGeneral");
        // Blocker vetoes Firefighter; the later role still provides Hauling.
        List<(IReadOnlyList<JobEntry> entries, bool blocker)> roles = [([WT("Firefighter")], true), ([WT("Firefighter"), WT("Hauling")], false)];
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("HaulGeneral");
        await Assert.That(result.WorkTypePriorities.ContainsKey("Firefighter")).IsFalse();
    }

    [Test]
    public async Task RoleAboveBlockerStillProvidesItsJobs()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients");
        // Earlier roles win: the provider above the blocker keeps the job.
        List<(IReadOnlyList<JobEntry> entries, bool blocker)> roles = [([WT("Doctor")], false), ([WT("Doctor")], true)];
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("TendPatients");
    }

    [Test]
    public async Task BlockerCanVetoSingleJobWhileTypeStaysAvailable()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients", "FeedPatients");
        List<(IReadOnlyList<JobEntry> entries, bool blocker)> roles = [([WG("FeedPatients")], true), ([WT("Doctor")], false)];
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("TendPatients");
    }

    [Test]
    public async Task RolesConcatenateInOrderAndFirstMentionWins()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients", "FeedPatients").WithWorkType("Hauling", "HaulGeneral");
        // Role 1: Doctor (whole type). Role 2: Hauling + TendPatients again (dup, ignored).
        var roles = Roles([WT("Doctor")], [WT("Hauling"), WG("TendPatients")]);
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("TendPatients,FeedPatients,HaulGeneral");
    }

    [Test]
    public async Task LeafBeforeParentKeepsItsEarlierPosition()
    {
        var catalog = new FakeCatalog().WithWorkType("Crafting", "MakeStoneBlocks", "Smelt", "MakeDrugs");
        // "Smelt first, then the rest of Crafting" inside one role.
        var roles = Roles([WG("Smelt"), WT("Crafting")]);
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("Smelt,MakeStoneBlocks,MakeDrugs");
    }

    [Test]
    public async Task ClaimMapAttributesGiversToTheFirstClaimingSlice()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients", "FeedPatients").WithWorkType("Hauling", "HaulGeneral");
        // Role 0 claims all of Doctor; role 1's duplicate TendPatients is inert.
        var roles = Roles([WT("Doctor")], [WT("Hauling"), WG("TendPatients")]);
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(result.ClaimedBySlice["TendPatients"]).IsEqualTo(0);
        await Assert.That(result.ClaimedBySlice["FeedPatients"]).IsEqualTo(0);
        await Assert.That(result.ClaimedBySlice["HaulGeneral"]).IsEqualTo(1);
    }

    [Test]
    public async Task BlockedGiversAreAbsentFromClaimMap()
    {
        var catalog = new FakeCatalog().WithWorkType("Firefighter", "FightFires").WithWorkType("Hauling", "HaulGeneral");
        List<(IReadOnlyList<JobEntry> entries, bool blocker)> roles = [([WT("Firefighter")], true), ([WT("Firefighter"), WT("Hauling")], false)];
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(result.ClaimedBySlice.ContainsKey("FightFires")).IsFalse();
        await Assert.That(result.ClaimedBySlice["HaulGeneral"]).IsEqualTo(1);
    }

    [Test]
    public async Task CapabilityFilteredGiversAreAbsentFromClaimMap()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients", "FeedPatients");
        var roles = Roles([WT("Doctor")]);
        var result = JobOrderCompiler.Compile(roles, catalog, g => g != "TendPatients");
        await Assert.That(result.ClaimedBySlice.ContainsKey("TendPatients")).IsFalse();
        await Assert.That(result.ClaimedBySlice["FeedPatients"]).IsEqualTo(0);
    }

    [Test]
    public async Task MovedSnapshotGiverClaimsToTheRoleCarryingIt()
    {
        // TendAnimals was moved out of Doctor by a mod; the snapshot expansion
        // re-adds it to role 0, whose slice must claim it.
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients").WithWorkType("Veterinary", "TendAnimals");
        Dictionary<string, List<string>> snapshot = new() { ["Doctor"] = ["TendPatients", "TendAnimals"] };
        var expanded = JobOrderCompiler.WithMovedSnapshotGivers([WT("Doctor")], snapshot, catalog);
        var result = JobOrderCompiler.Compile(Roles(expanded.ToArray()), catalog, _ => true);
        await Assert.That(result.ClaimedBySlice["TendPatients"]).IsEqualTo(0);
        await Assert.That(result.ClaimedBySlice["TendAnimals"]).IsEqualTo(0);
    }

    [Test]
    public async Task JobInNoRoleIsAbsent()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients").WithWorkType("Cleaning", "CleanFilth");
        var roles = Roles([WT("Doctor")]);
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(result.AllInOrder.Contains("CleanFilth")).IsFalse();
    }

    [Test]
    public async Task IncapableJobsAreDropped()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients", "FeedPatients");
        var roles = Roles([WT("Doctor")]);
        var result = JobOrderCompiler.Compile(roles, catalog, g => g != "TendPatients");
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("FeedPatients");
    }

    [Test]
    public async Task ContextCapabilityOverloadUsesTheSuppliedContext()
    {
        var catalog = new FakeCatalog().WithWorkType("Doctor", "TendPatients", "FeedPatients");
        var result = JobOrderCompiler.Compile([([WT("Doctor")], false)], catalog, "TendPatients", (blocked, giver) => giver != blocked);

        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("FeedPatients");
    }

    [Test]
    public async Task MissingDefsAreInert()
    {
        var catalog = new FakeCatalog().WithWorkType("Hauling", "HaulGeneral");
        // Unknown work type and unknown workgiver (e.g. a removed mod) contribute nothing.
        var roles = Roles([WT("ModdedType"), WG("ModdedGiver"), WT("Hauling")]);
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(Flat(result.AllInOrder)).IsEqualTo("HaulGeneral");
    }

    [Test]
    public async Task EmptyRolesProduceEmptyOrder()
    {
        var catalog = new FakeCatalog().WithWorkType("Hauling", "HaulGeneral");
        var result = JobOrderCompiler.Compile(Roles(), catalog, _ => true);
        await Assert.That(result.AllInOrder).IsEmpty();
        await Assert.That(result.Normal).IsEmpty();
        await Assert.That(result.Emergency).IsEmpty();
    }

    [Test]
    public async Task RanksAreConsecutivePerWorkTypeNotPositionBased()
    {
        // Doctor's first giver sits at list index 7, but it's only the SECOND work type,
        // so its rank is 2 — ranks count work types, not giver positions.
        var catalog = new FakeCatalog().WithWorkType("Hauling", "HaulGeneral", "h2", "h3", "h4", "h5", "h6", "h7").WithWorkType("Doctor", "TendPatients");
        var roles = Roles([WT("Hauling"), WT("Doctor")]);
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        await Assert.That(result.WorkTypePriorities["Hauling"]).IsEqualTo(1);
        await Assert.That(result.WorkTypePriorities["Doctor"]).IsEqualTo(2);
    }

    [Test]
    public async Task WorkTypeRankComesFromItsFirstGiverEvenIfSplitAcrossRoles()
    {
        var catalog = new FakeCatalog().WithWorkType("Crafting", "Smelt", "MakeDrugs").WithWorkType("Hauling", "HaulGeneral");
        // Smelt is first overall; the rest of Crafting lands later — rank uses Smelt's position.
        var roles = Roles([WG("Smelt")], [WT("Hauling"), WT("Crafting")]);
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
        var roles = Roles([WT("Firefighter"), WT("Doctor")], [WT("Hauling")]);
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);
        // Emergency membership follows the def flag alone; both lists preserve compiled order.
        await Assert.That(Flat(result.Emergency)).IsEqualTo("FightFires,TendEmergency");
        await Assert.That(Flat(result.Normal)).IsEqualTo("TendPatients,HaulGeneral");
    }

    /// Row-position rule: emergency jobs keep the emergency pass only when
    /// one of the colonist's first EmergencyRoleLimit roles claims them.
    [Test]
    public async Task RolePriorityEmergencyRuleDemotesEmergencyJobsOfLateRoles()
    {
        var catalog = new FakeCatalog()
            .WithWorkType("Firefighter", "FightFires")
            .WithWorkType("Patient", "PatientGoToBedEmergencyTreatment", "PatientGoToBedTreatment")
            .WithWorkType("Hauling", "HaulGeneral")
            .WithWorkType("Cooking", "CookMeals")
            .WithWorkType("Growing", "GrowerSow")
            .WithWorkType("Cleaning", "CleanFilth")
            .WithWorkType("Doctor", "TendEmergency", "TendPatients")
            .WithEmergency("FightFires", "PatientGoToBedEmergencyTreatment", "TendEmergency");
        // Core first, four working roles, then Doctor as the sixth role.
        var roles = Roles(
            [WT("Firefighter"), WT("Patient")], [WT("Hauling")], [WT("Cooking")],
            [WT("Growing")], [WT("Cleaning")], [WT("Doctor")]);
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);

        JobOrderCompiler.ApplyRolePriorityEmergencyRule(
            result, catalog, JobOrderCompiler.EmergencyRoleLimit);

        // Core's fires and emergency treatment still interrupt; the sixth
        // role's urgent tending runs as ordinary doctor work at its position.
        await Assert.That(Flat(result.Emergency)).IsEqualTo("FightFires,PatientGoToBedEmergencyTreatment");
        await Assert.That(Flat(result.Normal)).IsEqualTo(
            "PatientGoToBedTreatment,HaulGeneral,CookMeals,GrowerSow,CleanFilth,TendEmergency,TendPatients");
    }

    [Test]
    public async Task RolePriorityEmergencyRuleKeepsTheFifthRoleInTheEmergencyPass()
    {
        var catalog = new FakeCatalog()
            .WithWorkType("Firefighter", "FightFires")
            .WithWorkType("Hauling", "HaulGeneral")
            .WithWorkType("Cooking", "CookMeals")
            .WithWorkType("Growing", "GrowerSow")
            .WithWorkType("Doctor", "TendEmergency", "TendPatients")
            .WithEmergency("FightFires", "TendEmergency");
        var roles = Roles(
            [WT("Firefighter")], [WT("Hauling")], [WT("Cooking")], [WT("Growing")], [WT("Doctor")]);
        var result = JobOrderCompiler.Compile(roles, catalog, _ => true);

        JobOrderCompiler.ApplyRolePriorityEmergencyRule(
            result, catalog, JobOrderCompiler.EmergencyRoleLimit);

        await Assert.That(Flat(result.Emergency)).IsEqualTo("FightFires,TendEmergency");
        await Assert.That(Flat(result.Normal)).IsEqualTo("HaulGeneral,CookMeals,GrowerSow,TendPatients");
    }

    /// Vanilla replays priority numbers ascending, each left-to-right over the
    /// Work-tab columns; replaying the projected numbers must reproduce the
    /// internal rank order.
    private static string Replay(Dictionary<string, int> buckets, Dictionary<string, int> columns) =>
        string.Join(",", buckets.OrderBy(kv => kv.Value).ThenBy(kv => columns[kv.Key]).Select(kv => kv.Key));

    [Test]
    public async Task VanillaProjectionReplaysInternalOrder()
    {
        Dictionary<string, int> columns = new()
        {
            ["A"] = 0,
            ["B"] = 1,
            ["C"] = 2,
            ["D"] = 3,
        };
        Dictionary<string, int> ranks = new()
        {
            ["B"] = 1,
            ["A"] = 2,
            ["D"] = 3,
            ["C"] = 4,
        };
        var buckets = JobOrderCompiler.ToVanillaPriorities(ranks, n => columns[n]);
        await Assert.That(buckets["B"]).IsEqualTo(1);
        await Assert.That(buckets["A"]).IsEqualTo(2); // left of B: needs a later number
        await Assert.That(buckets["D"]).IsEqualTo(2); // right of A: same number keeps order
        await Assert.That(buckets["C"]).IsEqualTo(3); // left of D again
        await Assert.That(Replay(buckets, columns)).IsEqualTo("B,A,D,C");
    }

    [Test]
    public async Task VanillaProjectionInColumnOrderStaysAtOne()
    {
        Dictionary<string, int> columns = new()
        {
            ["A"] = 0,
            ["B"] = 1,
            ["C"] = 2,
            ["D"] = 3,
        };
        Dictionary<string, int> ranks = new()
        {
            ["A"] = 1,
            ["B"] = 2,
            ["C"] = 3,
            ["D"] = 4,
        };
        var buckets = JobOrderCompiler.ToVanillaPriorities(ranks, n => columns[n]);
        await Assert.That(buckets.Values.Distinct().Single()).IsEqualTo(1);
        await Assert.That(Replay(buckets, columns)).IsEqualTo("A,B,C,D");
    }

    [Test]
    public async Task VanillaProjectionSaturatesAtFour()
    {
        // Full reverse of column order needs one number per type; the tail
        // beyond four lumps into 4 (order there falls back to column order).
        Dictionary<string, int> columns = new()
        {
            ["A"] = 0,
            ["B"] = 1,
            ["C"] = 2,
            ["D"] = 3,
            ["E"] = 4,
        };
        Dictionary<string, int> ranks = new()
        {
            ["E"] = 1,
            ["D"] = 2,
            ["C"] = 3,
            ["B"] = 4,
            ["A"] = 5,
        };
        var buckets = JobOrderCompiler.ToVanillaPriorities(ranks, n => columns[n]);
        await Assert.That(buckets["E"]).IsEqualTo(1);
        await Assert.That(buckets["D"]).IsEqualTo(2);
        await Assert.That(buckets["C"]).IsEqualTo(3);
        await Assert.That(buckets["B"]).IsEqualTo(4);
        await Assert.That(buckets["A"]).IsEqualTo(4);
    }

    [Test]
    public async Task DeadEntriesIncludeClaimsAboveAndDuplicatesButNotTypesBelowTheirGivers()
    {
        var catalog = new FakeCatalog().WithWorkType("Cooking", "Cook", "Butcher", "Brew").WithWorkType("Hauling", "HaulGeneral");
        List<JobEntry> entries =
        [
            new(JobEntryKind.WorkGiver, "Cook"), // 0: above its type — alive
            new(JobEntryKind.WorkType, "Cooking"), // 1: claims Butcher+Brew — alive
            new(JobEntryKind.WorkGiver, "Butcher"), // 2: claimed by the type above — dead
            new(JobEntryKind.WorkType, "Cooking"), // 3: duplicate type — dead
            new(JobEntryKind.WorkGiver, "Cook"), // 4: duplicate giver — dead
            new(JobEntryKind.WorkGiver, "HaulGeneral"), // 5: alive
            new(JobEntryKind.WorkType, "ModdedGone"), // 6: unknown — never dead
            new(JobEntryKind.WorkGiver, "ModdedJob"), // 7: unknown — never dead
        ];
        var dead = JobOrderCompiler.DeadEntryIndexes(entries, catalog);
        await Assert.That(dead).IsEquivalentTo([2, 3, 4]);
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
        Dictionary<string, int> columns = new()
        {
            ["P2"] = 0,
            ["P1"] = 1,
            ["W"] = 2,
            ["Z"] = 3,
            ["Y"] = 4,
            ["X"] = 5,
        };
        Dictionary<string, int> ranks = new()
        {
            ["P1"] = 1,
            ["P2"] = 2,
            ["X"] = 3,
            ["Y"] = 4,
            ["Z"] = 5,
            ["W"] = 6,
        };
        var categories = new VanillaProjectionCategories { Basics = ["P1", "P2"] };

        var buckets = JobOrderCompiler.ToVanillaPriorities(ranks, n => columns[n], categories);
        // Pinning the head to 1 frees the numbers the tail needs.
        await Assert.That(buckets["P1"]).IsEqualTo(1);
        await Assert.That(buckets["P2"]).IsEqualTo(1);
        await Assert.That(buckets["X"]).IsEqualTo(1); // right of the pinned block
        await Assert.That(buckets["Y"]).IsEqualTo(2);
        await Assert.That(buckets["Z"]).IsEqualTo(3);
        await Assert.That(buckets["W"]).IsEqualTo(4);
    }

    [Test]
    public async Task VanillaProjectionSpreadsSpareNumbersByCategory()
    {
        // Everything in column order collapses to all-1s; the spread bumps
        // skilled work (and after) to 2, grunt (and after) to 3, research to 4.
        Dictionary<string, int> columns = new()
        {
            ["A"] = 0,
            ["B"] = 1,
            ["C"] = 2,
            ["D"] = 3,
            ["E"] = 4,
        };
        Dictionary<string, int> ranks = new()
        {
            ["A"] = 1,
            ["B"] = 2,
            ["C"] = 3,
            ["D"] = 4,
            ["E"] = 5,
        };
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
        // Skilled and grunt bumps run; the research bump is skipped once the
        // grunt bump lands D on 4 (see the trace below).
        Dictionary<string, int> columns = new()
        {
            ["A"] = 0,
            ["B"] = 1,
            ["C"] = 2,
            ["D"] = 3,
        };
        Dictionary<string, int> ranks = new()
        {
            ["A"] = 1,
            ["C"] = 2,
            ["B"] = 3,
            ["D"] = 4,
        }; // one inversion: B left of C
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
    public async Task MetadataProjectionOverloadMatchesLegacyDelegateAndCategories()
    {
        VanillaProjectionWorkTypeSource[] sources =
        [
            new VanillaProjectionWorkTypeSource("A", skilled: false, research: false),
            new VanillaProjectionWorkTypeSource("B", skilled: false, research: false),
            new VanillaProjectionWorkTypeSource("C", skilled: true, research: false),
            new VanillaProjectionWorkTypeSource("D", skilled: false, research: false),
            new VanillaProjectionWorkTypeSource("E", skilled: true, research: true),
        ];
        var metadata = new VanillaProjectionDefinitionMetadata(sources).WithBasics(["A", "B"]);
        Dictionary<string, int> ranks = new()
        {
            ["A"] = 1,
            ["B"] = 2,
            ["C"] = 3,
            ["D"] = 4,
            ["E"] = 5,
        };
        var categories = new VanillaProjectionCategories
        {
            Basics = ["A", "B"],
            Skilled = ["C", "E"],
            Grunt = ["D"],
            Research = ["E"],
        };

        var legacy = JobOrderCompiler.ToVanillaPriorities(ranks, metadata.ColumnOf, categories);
        var cached = JobOrderCompiler.ToVanillaPriorities(ranks, metadata);

        await Assert.That(cached).IsEquivalentTo(legacy);
        await Assert.That(string.Join(",", cached.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}"))).IsEqualTo("A:1,B:1,C:2,D:3,E:4");
    }
}
