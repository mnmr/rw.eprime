using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Tests for ResourcePool, PoolMembersCodec, PoolSnapshot, and ReadoutModel
/// pool operations (CreatePool, DeletePool, MigrateCategoryTokens, etc.).
public class PoolTests
{
    // ── SlotToken pool-ref shapes ──────────────────────────────────────────

    [Test]
    public async Task IsPoolRef_Hash_True()
    {
        await Assert.That(SlotToken.IsPoolRef("#12")).IsTrue();
    }

    [Test]
    public async Task IsPoolRef_TildeHash_True()
    {
        await Assert.That(SlotToken.IsPoolRef("~#12")).IsTrue();
    }

    [Test]
    public async Task IsPoolRef_PlainDef_False()
    {
        await Assert.That(SlotToken.IsPoolRef("Steel")).IsFalse();
    }

    [Test]
    public async Task IsPoolRef_AtCategory_False()
    {
        await Assert.That(SlotToken.IsPoolRef("@MeatRaw")).IsFalse();
    }

    [Test]
    public async Task PoolId_ValidToken_ReturnsId()
    {
        await Assert.That(SlotToken.PoolId("#12")).IsEqualTo(12);
    }

    [Test]
    public async Task PoolId_TildePrefixed_ReturnsId()
    {
        await Assert.That(SlotToken.PoolId("~#12")).IsEqualTo(12);
    }

    [Test]
    public async Task PoolId_PlainDef_ReturnsMinusOne()
    {
        await Assert.That(SlotToken.PoolId("Steel")).IsEqualTo(-1);
    }

    [Test]
    public async Task PoolId_AtCategory_ReturnsMinusOne()
    {
        await Assert.That(SlotToken.PoolId("@MeatRaw")).IsEqualTo(-1);
    }

    [Test]
    public async Task PoolId_NonNumericAfterHash_ReturnsMinusOne()
    {
        await Assert.That(SlotToken.PoolId("#abc")).IsEqualTo(-1);
    }

    [Test]
    public async Task PoolId_EmptyAfterHash_ReturnsMinusOne()
    {
        await Assert.That(SlotToken.PoolId("#")).IsEqualTo(-1);
    }

    [Test]
    public async Task PoolToken_BuildsHashIdString()
    {
        await Assert.That(SlotToken.PoolToken(7)).IsEqualTo("#7");
    }

    [Test]
    public async Task IsPool_AtRef_StillTrue()
    {
        // IsPool (the @ predicate) must remain unchanged — game code uses it
        await Assert.That(SlotToken.IsPool("@MeatRaw")).IsTrue();
        await Assert.That(SlotToken.IsPool("~@MeatRaw")).IsTrue();
        await Assert.That(SlotToken.IsPool("#5")).IsFalse();
    }

    [Test]
    public async Task Canonical_HashToken_StripsTilde()
    {
        await Assert.That(SlotToken.Canonical("~#12")).IsEqualTo("#12");
        await Assert.That(SlotToken.Canonical("#12")).IsEqualTo("#12");
    }

    [Test]
    public async Task WithShowWhenZero_HashToken_AddsOrRemovesTilde()
    {
        await Assert.That(SlotToken.WithShowWhenZero("#5", false)).IsEqualTo("~#5");
        await Assert.That(SlotToken.WithShowWhenZero("~#5", true)).IsEqualTo("#5");
    }

    // ── PoolMembersCodec ───────────────────────────────────────────────────

    [Test]
    public async Task PoolMembersCodec_RoundTrip_PlainDefs()
    {
        var members = new List<string> { "Steel", "WoodLog", "Plasteel" };
        var blob = PoolMembersCodec.Encode(members);
        var decoded = PoolMembersCodec.Decode(blob);
        await Assert.That(string.Join(",", decoded)).IsEqualTo("Steel,WoodLog,Plasteel");
    }

    [Test]
    public async Task PoolMembersCodec_RoundTrip_AtCategory()
    {
        var members = new List<string> { "@MeatRaw", "Steel" };
        var blob = PoolMembersCodec.Encode(members);
        var decoded = PoolMembersCodec.Decode(blob);
        await Assert.That(string.Join(",", decoded)).IsEqualTo("@MeatRaw,Steel");
    }

    [Test]
    public async Task PoolMembersCodec_EmptyList_EmptyString()
    {
        await Assert.That(PoolMembersCodec.Encode(new List<string>())).IsEqualTo("");
    }

    [Test]
    public async Task PoolMembersCodec_NullList_EmptyString()
    {
        await Assert.That(PoolMembersCodec.Encode(null)).IsEqualTo("");
    }

    [Test]
    public async Task PoolMembersCodec_EmptyBlob_EmptyList()
    {
        var decoded = PoolMembersCodec.Decode("");
        await Assert.That(decoded.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PoolMembersCodec_NullBlob_EmptyList()
    {
        var decoded = PoolMembersCodec.Decode(null);
        await Assert.That(decoded.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PoolMembersCodec_DropsEmptyEntries()
    {
        // Blob with consecutive commas (shouldn't normally happen, but be safe)
        var decoded = PoolMembersCodec.Decode("Steel,,WoodLog");
        await Assert.That(string.Join(",", decoded)).IsEqualTo("Steel,WoodLog");
    }

    // ── ReadoutModel pool ops ──────────────────────────────────────────────

    [Test]
    public async Task CreatePool_AppendsPool()
    {
        var model = new ReadoutModel();
        var pool = model.CreatePool(1, "Meats");
        await Assert.That(model.Pools.Count).IsEqualTo(1);
        await Assert.That(pool.Id).IsEqualTo(1);
        await Assert.That(pool.Name).IsEqualTo("Meats");
    }

    [Test]
    public async Task CreatePool_TrimsName()
    {
        var model = new ReadoutModel();

        var pool = model.CreatePool(1, "  Concrete  ");

        await Assert.That(pool.Name).IsEqualTo("Concrete");
    }

    [Test]
    public async Task CreatePool_RejectsCaseInsensitiveTrimmedDuplicate()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Concrete");

        await Assert.That(() => model.CreatePool(2, "  concrete  "))
            .Throws<InvalidOperationException>();
        await Assert.That(model.Pools.Count).IsEqualTo(1);
        await Assert.That(model.PoolById(2)).IsNull();
    }

    [Test]
    public async Task PoolsStayNameSortedThroughCreateAndRename()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Wools");
        model.CreatePool(2, "Meats");
        model.CreatePool(3, "eggs"); // case-insensitive ordering
        await Assert.That(string.Join(",", model.Pools.Select(p => p.Name)))
            .IsEqualTo("eggs,Meats,Wools");
        model.RenamePool(1, "Aardvark hides");
        await Assert.That(string.Join(",", model.Pools.Select(p => p.Name)))
            .IsEqualTo("Aardvark hides,eggs,Meats");
    }

    [Test]
    public async Task PoolById_Found_ReturnsPool()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Meats");
        model.CreatePool(2, "Metals");
        await Assert.That(model.PoolById(2)?.Name).IsEqualTo("Metals");
    }

    [Test]
    public async Task PoolById_Missing_ReturnsNull()
    {
        var model = new ReadoutModel();
        await Assert.That(model.PoolById(99)).IsNull();
    }

    [Test]
    public async Task RenamePool_ChangesName()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Old");
        await Assert.That(model.RenamePool(1, "New")).IsTrue();
        await Assert.That(model.PoolById(1)!.Name).IsEqualTo("New");
    }

    [Test]
    public async Task RenamePool_MissingId_ReturnsFalse()
    {
        var model = new ReadoutModel();
        await Assert.That(model.RenamePool(99, "X")).IsFalse();
    }

    [Test]
    public async Task RenamePool_RejectsCaseInsensitiveTrimmedDuplicate()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Concrete");
        model.CreatePool(2, "Mortar");

        await Assert.That(model.RenamePool(2, " concrete ")).IsFalse();
        await Assert.That(model.PoolById(2)!.Name).IsEqualTo("Mortar");
    }

    [Test]
    public async Task SetPoolMembers_ClonesMembers()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Meats");
        var source = new List<string> { "Meat_Cow", "@MeatRaw" };
        model.SetPoolMembers(1, source);
        source.Clear(); // mutate original — pool should be unaffected
        await Assert.That(string.Join(",", model.PoolById(1)!.Members))
            .IsEqualTo("Meat_Cow,@MeatRaw");
    }

    [Test]
    public async Task SetPoolIcon_SetsIcon()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Meats");
        await Assert.That(model.SetPoolIcon(1, "Meat_Cow")).IsTrue();
        await Assert.That(model.PoolById(1)!.IconDefName).IsEqualTo("Meat_Cow");
    }

    // ── DeletePool purges tokens and thresholds ────────────────────────────

    [Test]
    public async Task DeletePool_PurgesPlainPoolTokens()
    {
        var model = new ReadoutModel();
        model.CreatePool(5, "Meats");
        model.CreateGroup(1, "G1");
        model.SetTiers(1, new List<List<string>> { new() { "#5", "Steel" } });

        model.DeletePool(5);

        await Assert.That(model.PoolById(5)).IsNull();
        // "#5" removed, "Steel" stays
        await Assert.That(string.Join(",", model.GroupById(1)!.Tiers[0])).IsEqualTo("Steel");
    }

    [Test]
    public async Task DeletePool_PurgesTildePrefixedPoolTokens()
    {
        var model = new ReadoutModel();
        model.CreatePool(5, "Meats");
        model.CreateGroup(1, "G1");
        model.SetTiers(1, new List<List<string>> { new() { "~#5", "Steel" } });

        model.DeletePool(5);

        await Assert.That(string.Join(",", model.GroupById(1)!.Tiers[0])).IsEqualTo("Steel");
    }

    [Test]
    public async Task DeletePool_CompactsTiersAfterPurge()
    {
        var model = new ReadoutModel();
        model.CreatePool(3, "X");
        model.CreateGroup(1, "G1");
        // Two tiers: tier 0 has only "#3", tier 1 has "Steel"
        model.SetTiers(1, new List<List<string>>
        {
            new() { "#3" },
            new() { "Steel" }
        });

        model.DeletePool(3);

        // Tier 0 should be gone; "Steel" remains as tier 0
        await Assert.That(model.GroupById(1)!.TierCount).IsEqualTo(1);
        await Assert.That(string.Join(",", model.GroupById(1)!.Tiers[0])).IsEqualTo("Steel");
    }

    [Test]
    public async Task DeletePool_RemovesThresholdEntry()
    {
        var model = new ReadoutModel();
        model.CreatePool(7, "Meats");
        model.SetThreshold("#7", 100, 20);
        model.SetThreshold("Steel", 50, 10);

        model.DeletePool(7);

        await Assert.That(model.Thresholds.ContainsKey("#7")).IsFalse();
        await Assert.That(model.Thresholds.ContainsKey("Steel")).IsTrue();
    }

    [Test]
    public async Task DeletePool_MissingId_ReturnsFalse()
    {
        var model = new ReadoutModel();
        await Assert.That(model.DeletePool(99)).IsFalse();
    }

    // ── CleanupMissing (new two-arg signature) ────────────────────────────

    [Test]
    public async Task CleanupMissing_PurgesPoolMembersViaSecondPredicate()
    {
        var model = new ReadoutModel();
        var pool = model.CreatePool(1, "Mixed");
        pool.Members.Add("Steel");      // valid
        pool.Members.Add("Gone");       // invalid
        pool.Members.Add("@MeatRaw");   // valid category

        model.CleanupMissing(
            tokenValid: t => true,
            memberValid: m => m == "Steel" || m == "@MeatRaw");

        await Assert.That(string.Join(",", pool.Members)).IsEqualTo("Steel,@MeatRaw");
    }

    [Test]
    public async Task CleanupMissing_KeepsEmptyPool()
    {
        var model = new ReadoutModel();
        var pool = model.CreatePool(1, "Empty");
        pool.Members.Add("Gone");

        model.CleanupMissing(tokenValid: t => true, memberValid: m => false);

        // Pool itself is preserved even though all members were invalid
        await Assert.That(model.Pools.Count).IsEqualTo(1);
        await Assert.That(pool.Members.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CleanupMissing_NormalizesLegacyPoolNamesDeterministically()
    {
        var model = new ReadoutModel();
        model.Pools.Add(new ResourcePool { Id = 1, Name = "  Concrete  " });
        model.Pools.Add(new ResourcePool { Id = 2, Name = "concrete" });
        model.Pools.Add(new ResourcePool { Id = 3, Name = "   " });

        model.CleanupMissing(tokenValid: _ => true, memberValid: _ => true);

        await Assert.That(model.PoolById(1)!.Name).IsEqualTo("Concrete");
        await Assert.That(model.PoolById(2)!.Name).IsEqualTo("concrete (2)");
        await Assert.That(model.PoolById(3)!.Name).IsEqualTo("Pool");
    }

    [Test]
    public async Task CleanupMissing_DuplicateDoesNotClaimExistingSuffixedName()
    {
        var model = new ReadoutModel();
        model.Pools.Add(new ResourcePool { Id = 1, Name = "Concrete" });
        model.Pools.Add(new ResourcePool { Id = 2, Name = "concrete" });
        model.Pools.Add(new ResourcePool { Id = 3, Name = "Concrete (2)" });

        model.CleanupMissing(tokenValid: _ => true, memberValid: _ => true);

        await Assert.That(model.PoolById(1)!.Name).IsEqualTo("Concrete");
        await Assert.That(model.PoolById(2)!.Name).IsEqualTo("concrete (3)");
        await Assert.That(model.PoolById(3)!.Name).IsEqualTo("Concrete (2)");
    }

    [Test]
    public async Task CleanupMissing_BlankDoesNotClaimExistingFallbackName()
    {
        var model = new ReadoutModel();
        model.Pools.Add(new ResourcePool { Id = 1, Name = "   " });
        model.Pools.Add(new ResourcePool { Id = 2, Name = "Pool" });

        model.CleanupMissing(tokenValid: _ => true, memberValid: _ => true);

        await Assert.That(model.PoolById(1)!.Name).IsEqualTo("Pool (2)");
        await Assert.That(model.PoolById(2)!.Name).IsEqualTo("Pool");
    }

    [Test]
    public async Task CleanupMissing_StillPurgesStaleThresholds()
    {
        var model = new ReadoutModel();
        model.SetThreshold("Steel", 100, 20);
        model.SetThreshold("Gone", 5, 1);

        model.CleanupMissing(
            tokenValid: t => t == "Steel",
            memberValid: m => true);

        await Assert.That(model.Thresholds.ContainsKey("Gone")).IsFalse();
        await Assert.That(model.Thresholds.ContainsKey("Steel")).IsTrue();
    }

    // ── MigrateCategoryTokens ─────────────────────────────────────────────

    [Test]
    public async Task MigrateCategoryTokens_PlainAtRef_CreatesPoolAndReplacesToken()
    {
        var model = new ReadoutModel();
        model.CreateGroup(1, "Food");
        model.SetTiers(1, new List<List<string>> { new() { "@MeatRaw", "Steel" } });

        int nextId = 10;
        bool changed = model.MigrateCategoryTokens(
            takeId: () => nextId++,
            nameForCategory: cat => "Pool:" + cat);

        await Assert.That(changed).IsTrue();
        // Pool created with the @ref as its single member
        var pool = model.Pools[0];
        await Assert.That(pool.Members.Count).IsEqualTo(1);
        await Assert.That(pool.Members[0]).IsEqualTo("@MeatRaw");
        // Token replaced with "#10"
        await Assert.That(model.GroupById(1)!.Tiers[0][0]).IsEqualTo("#10");
        await Assert.That(model.GroupById(1)!.Tiers[0][1]).IsEqualTo("Steel");
    }

    [Test]
    public async Task MigrateCategoryTokens_TildeFlagPreserved()
    {
        var model = new ReadoutModel();
        model.CreateGroup(1, "Food");
        model.SetTiers(1, new List<List<string>> { new() { "~@MeatRaw" } });

        int nextId = 1;
        model.MigrateCategoryTokens(() => nextId++, cat => cat);

        string token = model.GroupById(1)!.Tiers[0][0];
        await Assert.That(token).IsEqualTo("~#1"); // flag preserved
    }

    [Test]
    public async Task MigrateCategoryTokens_TwoGroupsSameRef_SharesOnePool()
    {
        var model = new ReadoutModel();
        model.CreateGroup(1, "G1");
        model.CreateGroup(2, "G2");
        model.SetTiers(1, new List<List<string>> { new() { "@MeatRaw" } });
        model.SetTiers(2, new List<List<string>> { new() { "@MeatRaw", "Steel" } });

        int nextId = 100;
        model.MigrateCategoryTokens(() => nextId++, cat => cat);

        // Only ONE pool created
        await Assert.That(model.Pools.Count).IsEqualTo(1);
        int pid = model.Pools[0].Id;
        // Both groups reference the same pool
        await Assert.That(model.GroupById(1)!.Tiers[0][0]).IsEqualTo("#" + pid);
        await Assert.That(model.GroupById(2)!.Tiers[0][0]).IsEqualTo("#" + pid);
    }

    [Test]
    public async Task MigrateCategoryTokens_DisambiguatesCollidingPoolName()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Concrete").Members.Add("Steel");
        model.CreateGroup(1, "Materials");
        model.SetTiers(1, new List<List<string>> { new() { "@Concrete" } });

        int nextId = 2;
        model.MigrateCategoryTokens(() => nextId++, _ => " concrete ");

        await Assert.That(model.PoolById(2)!.Name).IsEqualTo("concrete (2)");
        await Assert.That(model.GroupById(1)!.Tiers[0][0]).IsEqualTo("#2");
    }

    [Test]
    public async Task MigrateCategoryTokens_NoAtTokens_ReturnsFalse()
    {
        var model = new ReadoutModel();
        model.CreateGroup(1, "G1");
        model.SetTiers(1, new List<List<string>> { new() { "Steel", "WoodLog" } });

        int nextId = 1;
        bool changed = model.MigrateCategoryTokens(() => nextId++, cat => cat);

        await Assert.That(changed).IsFalse();
        await Assert.That(model.Pools.Count).IsEqualTo(0);
    }

    [Test]
    public async Task MigrateCategoryTokens_SecondCall_NoOp()
    {
        var model = new ReadoutModel();
        model.CreateGroup(1, "G1");
        model.SetTiers(1, new List<List<string>> { new() { "@MeatRaw" } });

        int nextId = 1;
        model.MigrateCategoryTokens(() => nextId++, cat => cat);
        // Second call: tokens are now #ids, no @refs remain
        bool secondChange = model.MigrateCategoryTokens(() => nextId++, cat => cat);

        await Assert.That(secondChange).IsFalse();
        await Assert.That(model.Pools.Count).IsEqualTo(1); // no duplicate pools
    }

    // ── PoolSnapshot ───────────────────────────────────────────────────────

    [Test]
    public async Task PoolSnapshot_PlainDefMembers_Expanded()
    {
        var catalog = StaticResources.Catalog();
        var pool = new ResourcePool { Id = 1, Name = "Metals" };
        pool.Members.Add("Steel");
        pool.Members.Add("Plasteel");

        var snap = PoolSnapshot.Build(new List<ResourcePool> { pool }, catalog);
        bool found = snap.TryGet(1, out var members, out _, out _);

        await Assert.That(found).IsTrue();
        await Assert.That(string.Join(",", members!)).IsEqualTo("Steel,Plasteel");
    }

    [Test]
    public async Task PoolSnapshot_CategoryRefExpanded()
    {
        var catalog = StaticResources.Catalog();
        var pool = new ResourcePool { Id = 1, Name = "AllMeat" };
        pool.Members.Add("@MeatRaw");

        var snap = PoolSnapshot.Build(new List<ResourcePool> { pool }, catalog);
        snap.TryGet(1, out var members, out _, out _);

        await Assert.That(string.Join(",", members!)).IsEqualTo("Meat_Cow,Meat_Chicken");
    }

    [Test]
    public async Task PoolSnapshot_CategoryAliasesCollapseToCanonicalDef()
    {
        var catalog = new FakeResourceCatalog()
            .With("Meat_Cow", "raw meat")
            .WithAlias("Meat_Chicken", "Meat_Cow")
            .WithCategory("MeatRaw", "raw meat", "Meat_Chicken", "Meat_Cow");
        var pool = new ResourcePool { Id = 1, Name = "Meats" };
        pool.Members.Add("@MeatRaw");

        var snapshot = PoolSnapshot.Build(
            new List<ResourcePool> { pool }, catalog);
        snapshot.TryGet(1, out var members, out _, out _);

        await Assert.That(string.Join(",", members!)).IsEqualTo("Meat_Cow");
    }

    [Test]
    public async Task PoolSnapshot_DedupeAcrossOverlappingMembers()
    {
        var catalog = StaticResources.Catalog();
        // "@MeatRaw" expands to Meat_Cow, Meat_Chicken; then "Meat_Cow" is duplicate
        var pool = new ResourcePool { Id = 1, Name = "Meats" };
        pool.Members.Add("@MeatRaw");
        pool.Members.Add("Meat_Cow"); // duplicate — already covered by @MeatRaw

        var snap = PoolSnapshot.Build(new List<ResourcePool> { pool }, catalog);
        snap.TryGet(1, out var members, out _, out _);

        // Meat_Cow should appear only once
        await Assert.That(string.Join(",", members!)).IsEqualTo("Meat_Cow,Meat_Chicken");
    }

    [Test]
    public async Task PoolSnapshot_AliasedMembersCollapseToCanonicalDef()
    {
        var catalog = new FakeResourceCatalog()
            .With("Meat_Cow", "raw meat")
            .WithAlias("Meat_Chicken", "Meat_Cow");
        var pool = new ResourcePool { Id = 1, Name = "Meats" };
        pool.Members.Add("Meat_Chicken");

        var snapshot = PoolSnapshot.Build(
            new List<ResourcePool> { pool }, catalog);
        snapshot.TryGet(1, out var members, out _, out _);
        var group = new ReadoutGroup { Id = 1, Name = "Raw" };
        group.Tiers.Add(new List<string> { SlotToken.PoolToken(1) });
        RenderModel render = ReadoutLayoutEngine.Build(new LayoutInput
        {
            Groups = new List<ReadoutGroup> { group },
            Counts = new Dictionary<string, int> { ["Meat_Cow"] = 37 },
            Thresholds = new Dictionary<string, ThresholdSpec>(),
            Catalog = catalog,
            Pools = snapshot,
            Width = 140f,
        });

        await Assert.That(string.Join(",", members!)).IsEqualTo("Meat_Cow");
        await Assert.That(render.Cells.Single(cell => cell.Kind == CellKind.Counter).Text)
            .IsEqualTo("37");
    }

    [Test]
    public async Task PoolSnapshot_DistinctDefsWithSameLabelRemainSeparate()
    {
        var catalog = new FakeResourceCatalog()
            .With("Meat_Cow", "raw meat")
            .With("Meat_Yak", "raw meat");
        var pool = new ResourcePool { Id = 1, Name = "Meats" };
        pool.Members.Add("Meat_Cow");
        pool.Members.Add("Meat_Yak");

        var snapshot = PoolSnapshot.Build(
            new List<ResourcePool> { pool }, catalog);
        snapshot.TryGet(1, out var members, out _, out _);

        await Assert.That(string.Join(",", members!))
            .IsEqualTo("Meat_Cow,Meat_Yak");
    }

    [Test]
    public async Task PoolSnapshot_ExplicitIcon_UsedWhenResolvable()
    {
        var catalog = StaticResources.Catalog();
        var pool = new ResourcePool { Id = 1, Name = "Food", IconDefName = "Plasteel" };
        pool.Members.Add("Steel");

        var snap = PoolSnapshot.Build(new List<ResourcePool> { pool }, catalog);
        snap.TryGet(1, out _, out string? icon, out _);

        await Assert.That(icon).IsEqualTo("Plasteel");
    }

    [Test]
    public async Task PoolSnapshot_AliasedExplicitIconUsesCanonicalDef()
    {
        var catalog = new FakeResourceCatalog()
            .With("Meat_Cow", "raw meat")
            .WithAlias("Meat_Chicken", "Meat_Cow");
        var pool = new ResourcePool
        {
            Id = 1,
            Name = "Meats",
            IconDefName = "Meat_Chicken",
        };
        pool.Members.Add("Meat_Cow");

        var snapshot = PoolSnapshot.Build(
            new List<ResourcePool> { pool }, catalog);
        snapshot.TryGet(1, out _, out string? icon, out _);

        await Assert.That(icon).IsEqualTo("Meat_Cow");
    }

    [Test]
    public async Task PoolSnapshot_UnresolvableExplicitIcon_FallsBackToFirstMember()
    {
        var catalog = StaticResources.Catalog();
        var pool = new ResourcePool { Id = 1, Name = "Food", IconDefName = "Nonexistent" };
        pool.Members.Add("Steel");
        pool.Members.Add("WoodLog");

        var snap = PoolSnapshot.Build(new List<ResourcePool> { pool }, catalog);
        snap.TryGet(1, out _, out string? icon, out _);

        await Assert.That(icon).IsEqualTo("Steel");
    }

    [Test]
    public async Task PoolSnapshot_NoExplicitIcon_FallsBackToFirstMember()
    {
        var catalog = StaticResources.Catalog();
        var pool = new ResourcePool { Id = 1, Name = "Food" };
        pool.Members.Add("Cloth");

        var snap = PoolSnapshot.Build(new List<ResourcePool> { pool }, catalog);
        snap.TryGet(1, out _, out string? icon, out _);

        await Assert.That(icon).IsEqualTo("Cloth");
    }

    [Test]
    public async Task PoolSnapshot_NoMembersAndNoIcon_IconIsNull()
    {
        var catalog = StaticResources.Catalog();
        var pool = new ResourcePool { Id = 1, Name = "Empty" };

        var snap = PoolSnapshot.Build(new List<ResourcePool> { pool }, catalog);
        snap.TryGet(1, out _, out string? icon, out _);

        await Assert.That(icon).IsNull();
    }

    [Test]
    public async Task PoolSnapshot_UnknownPoolId_ReturnsFalse()
    {
        var catalog = StaticResources.Catalog();
        var snap = PoolSnapshot.Build(new List<ResourcePool>(), catalog);
        bool found = snap.TryGet(99, out _, out _, out _);
        await Assert.That(found).IsFalse();
    }

    [Test]
    public async Task PoolSnapshot_ExplicitIconDoesNotNeedToBeAMember()
    {
        // Icon may be any resolvable def, not necessarily a member
        var catalog = StaticResources.Catalog();
        var pool = new ResourcePool { Id = 1, Name = "Meats", IconDefName = "Gold" };
        pool.Members.Add("Steel");

        var snap = PoolSnapshot.Build(new List<ResourcePool> { pool }, catalog);
        snap.TryGet(1, out _, out string? icon, out _);

        await Assert.That(icon).IsEqualTo("Gold");
    }

    [Test]
    public async Task PoolSnapshot_PublishedMembersRejectConsumerMutation()
    {
        var catalog = StaticResources.Catalog();
        var pool = new ResourcePool { Id = 1, Name = "Metals" };
        pool.Members.Add("Steel");
        var snapshot = PoolSnapshot.Build(new List<ResourcePool> { pool }, catalog);
        snapshot.TryGet(1, out var members, out _, out _);

        await Assert.That(() => ((IList<string>)members!)[0] = "Gold")
            .Throws<NotSupportedException>();
    }

    [Test]
    public async Task PoolSnapshot_PublishesInputOrderWithStableEntryData()
    {
        var catalog = StaticResources.Catalog();
        var alpha = new ResourcePool
        {
            Id = 2,
            Name = "Alpha",
            IconDefName = "Steel",
            Members = new List<string> { "Steel" },
        };
        var zinc = new ResourcePool
        {
            Id = 9,
            Name = "Zinc",
            Members = new List<string> { "Plasteel" },
        };

        var snapshot = PoolSnapshot.Build(
            new List<ResourcePool> { alpha, zinc }, catalog);

        await Assert.That(snapshot.Count).IsEqualTo(2);
        await Assert.That(snapshot.EntryAt(0).Id).IsEqualTo(2);
        await Assert.That(snapshot.EntryAt(0).Name).IsEqualTo("Alpha");
        await Assert.That(snapshot.EntryAt(0).IconDefName).IsEqualTo("Steel");
        await Assert.That(string.Join(",", snapshot.EntryAt(0).Members))
            .IsEqualTo("Steel");
        await Assert.That(snapshot.EntryAt(1).Id).IsEqualTo(9);
        await Assert.That(snapshot.EntryAt(1).Name).IsEqualTo("Zinc");
    }
}
