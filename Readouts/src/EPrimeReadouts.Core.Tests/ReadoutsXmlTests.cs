using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Tests for ReadoutsXml.Export, ReadoutsXml.TryImport, and ReadoutModel.ApplyImport.
public class ReadoutsXmlTests
{
    // ── Sequential id allocators ──────────────────────────────────────────

    private static Func<int> Counter(int start = 1)
    {
        int n = start;
        return () => n++;
    }

    // ── Full round-trip ───────────────────────────────────────────────────

    [Test]
    public async Task RoundTrip_FullModel_PoolsAndGroupsRoundTrip()
    {
        // Build source model
        var model = new ReadoutModel();

        // Pool 1: Meats with icon, @ref + defName members
        var pool1 = model.CreatePool(10, "Meats");
        pool1.IconDefName = "Meat_Cow";
        pool1.Members.Add("@MeatRaw");
        pool1.Members.Add("Meat_Cow");

        // Pool 2: Metals, no icon
        var pool2 = model.CreatePool(11, "Metals");
        pool2.Members.Add("Steel");
        pool2.Members.Add("Plasteel");

        // Group 1: multi-tier, DefaultEnabled=true, pool ref + ~ flag
        var g1 = model.CreateGroup(100, "Food");
        model.SetTiers(100, new List<List<string>>
        {
            new() { "#10", "~Milk" },   // pool ref + flagged plain token
            new() { "RawRice" },
        });

        // Group 2: DefaultEnabled=false, ~pool ref
        var g2 = model.CreateGroup(101, "Metals");
        g2.DefaultEnabled = false;
        model.SetTiers(101, new List<List<string>>
        {
            new() { "~#11", "Silver" },
        });

        // Add threshold (should be cleared after import)
        model.SetThreshold("Steel", 100, 20);

        // Export
        string xml = ReadoutsXml.Export(model.Pools, model.InDisplayOrder());

        // Import
        bool ok = ReadoutsXml.TryImport(xml, out var iPools, out var iGroups, out string err);
        await Assert.That(ok).IsTrue();
        await Assert.That(err).IsNull();

        // Apply to a fresh model
        var fresh = new ReadoutModel();
        fresh.ApplyImport(iPools, iGroups, Counter(1), Counter(1));

        // ── Pools ──────────────────────────────────────────────────────
        await Assert.That(fresh.Pools.Count).IsEqualTo(2);

        var fp1 = fresh.Pools[0];
        await Assert.That(fp1.Name).IsEqualTo("Meats");
        await Assert.That(fp1.IconDefName).IsEqualTo("Meat_Cow");
        await Assert.That(string.Join(",", fp1.Members)).IsEqualTo("@MeatRaw,Meat_Cow");

        var fp2 = fresh.Pools[1];
        await Assert.That(fp2.Name).IsEqualTo("Metals");
        await Assert.That(fp2.IconDefName).IsNull();
        await Assert.That(string.Join(",", fp2.Members)).IsEqualTo("Steel,Plasteel");

        // ── Groups — display order ─────────────────────────────────────
        var ordered = fresh.InDisplayOrder();
        await Assert.That(string.Join(",", ordered.Select(g => g.Name))).IsEqualTo("Food,Metals");

        // Group Food (gets id=1 from allocator)
        var fg1 = ordered[0];
        await Assert.That(fg1.Name).IsEqualTo("Food");
        await Assert.That(fg1.DefaultEnabled).IsTrue();
        await Assert.That(fg1.TierCount).IsEqualTo(2);

        // Pool "Meats" was assigned fresh id=1; "pool:Meats" → "#1"
        await Assert.That(string.Join(",", fg1.Tiers[0])).IsEqualTo("#1,~Milk");
        await Assert.That(string.Join(",", fg1.Tiers[1])).IsEqualTo("RawRice");

        // Group Metals (gets id=2 from allocator)
        var fg2 = ordered[1];
        await Assert.That(fg2.Name).IsEqualTo("Metals");
        await Assert.That(fg2.DefaultEnabled).IsFalse();
        await Assert.That(fg2.TierCount).IsEqualTo(1);

        // Pool "Metals" was assigned fresh id=2; "~pool:Metals" → "~#2"
        await Assert.That(string.Join(",", fg2.Tiers[0])).IsEqualTo("~#2,Silver");

        // ── Thresholds cleared ────────────────────────────────────────
        await Assert.That(fresh.Thresholds.Count).IsEqualTo(0);
    }

    // ── Export drops unknown-pool tokens ─────────────────────────────────

    [Test]
    public async Task Export_UnknownPoolIdToken_Dropped()
    {
        var model = new ReadoutModel();
        model.CreatePool(1, "Meats");
        var g = model.CreateGroup(10, "Food");
        // "#99" references a pool that does not exist
        model.SetTiers(10, new List<List<string>>
        {
            new() { "#1", "#99", "Steel" },
        });

        string xml = ReadoutsXml.Export(model.Pools, model.InDisplayOrder());

        bool ok = ReadoutsXml.TryImport(xml, out _, out var groups, out _);
        await Assert.That(ok).IsTrue();
        // "#99" was dropped; "pool:Meats" and "Steel" survive
        var tier = groups[0].Tiers[0];
        await Assert.That(string.Join(",", tier)).IsEqualTo("pool:Meats,Steel");
    }

    // ── Import drops unknown pool:Name and compacts ───────────────────────

    [Test]
    public async Task Import_UnknownPoolName_DroppedAndCompacted()
    {
        // Hand-authored XML referencing a pool that does not appear in <Pools>
        string xml = @"<Readouts>
  <Pools>
    <Pool Name=""Metals"">
      <Member>Steel</Member>
    </Pool>
  </Pools>
  <Groups>
    <Group Name=""Test"">
      <Tier>
        <Slot>pool:Metals</Slot>
        <Slot>pool:Unknown</Slot>
        <Slot>Silver</Slot>
      </Tier>
      <Tier>
        <Slot>pool:AlsoUnknown</Slot>
      </Tier>
    </Group>
  </Groups>
</Readouts>";

        bool ok = ReadoutsXml.TryImport(xml, out var pools, out var groups, out _);
        await Assert.That(ok).IsTrue();

        var fresh = new ReadoutModel();
        fresh.ApplyImport(pools, groups, Counter(1), Counter(1));

        var fg = fresh.InDisplayOrder()[0];
        // Tier 0: "pool:Unknown" dropped, "pool:Metals" resolves to "#1", "Silver" stays
        await Assert.That(fg.TierCount).IsEqualTo(1); // tier 1 had only unknowns → compacted away
        await Assert.That(string.Join(",", fg.Tiers[0])).IsEqualTo("#1,Silver");
    }

    // ── TryImport malformed XML → false + non-empty error ────────────────

    [Test]
    public async Task TryImport_MalformedXml_ReturnsFalse()
    {
        bool ok = ReadoutsXml.TryImport("<NotClosed>", out _, out _, out string error);
        await Assert.That(ok).IsFalse();
        await Assert.That(error).IsNotNull();
        await Assert.That(error.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task TryImport_WrongRoot_ReturnsFalse()
    {
        bool ok = ReadoutsXml.TryImport("<WrongRoot/>", out _, out _, out string error);
        await Assert.That(ok).IsFalse();
        await Assert.That(error).IsNotNull();
        await Assert.That(error.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task TryImport_NullInput_ReturnsFalse()
    {
        bool ok = ReadoutsXml.TryImport(null, out _, out _, out string error);
        await Assert.That(ok).IsFalse();
        await Assert.That(error).IsNotNull();
    }

    // ── Unknown elements/attributes ignored ───────────────────────────────

    [Test]
    public async Task TryImport_UnknownElements_Ignored()
    {
        string xml = @"<Readouts>
  <UnknownSection>
    <Whatever Foo=""bar""/>
  </UnknownSection>
  <Groups>
    <Group Name=""G1"" UnknownAttr=""xyz"">
      <Tier>
        <Slot>Steel</Slot>
        <ExtraElement>ignored</ExtraElement>
      </Tier>
    </Group>
  </Groups>
</Readouts>";

        bool ok = ReadoutsXml.TryImport(xml, out var pools, out var groups, out string error);
        await Assert.That(ok).IsTrue();
        await Assert.That(pools.Count).IsEqualTo(0);
        await Assert.That(groups.Count).IsEqualTo(1);
        await Assert.That(groups[0].Name).IsEqualTo("G1");
        await Assert.That(string.Join(",", groups[0].Tiers[0])).IsEqualTo("Steel");
    }

    // ── DefaultEnabled absent → true ─────────────────────────────────────

    [Test]
    public async Task TryImport_DefaultEnabledAbsent_IsTrue()
    {
        string xml = @"<Readouts>
  <Groups>
    <Group Name=""G1"">
      <Tier><Slot>Steel</Slot></Tier>
    </Group>
  </Groups>
</Readouts>";

        ReadoutsXml.TryImport(xml, out _, out var groups, out _);
        await Assert.That(groups[0].DefaultEnabled).IsTrue();
    }

    [Test]
    public async Task TryImport_DefaultEnabledFalse_IsFalse()
    {
        string xml = @"<Readouts>
  <Groups>
    <Group Name=""G1"" DefaultEnabled=""False"">
      <Tier><Slot>Steel</Slot></Tier>
    </Group>
  </Groups>
</Readouts>";

        ReadoutsXml.TryImport(xml, out _, out var groups, out _);
        await Assert.That(groups[0].DefaultEnabled).IsFalse();
    }

    // ── Excess tiers dropped ──────────────────────────────────────────────

    [Test]
    public async Task TryImport_MoreThanMaxTiers_ExtraDropped()
    {
        // MaxTiers == 3; provide 4
        string xml = @"<Readouts>
  <Groups>
    <Group Name=""G1"">
      <Tier><Slot>A</Slot></Tier>
      <Tier><Slot>B</Slot></Tier>
      <Tier><Slot>C</Slot></Tier>
      <Tier><Slot>D</Slot></Tier>
    </Group>
  </Groups>
</Readouts>";

        ReadoutsXml.TryImport(xml, out _, out var groups, out _);
        await Assert.That(groups[0].TierCount).IsEqualTo(TierOps.MaxTiers);
        // Verify first three tiers, fourth dropped
        await Assert.That(string.Join(",", groups[0].Tiers[0])).IsEqualTo("A");
        await Assert.That(string.Join(",", groups[0].Tiers[1])).IsEqualTo("B");
        await Assert.That(string.Join(",", groups[0].Tiers[2])).IsEqualTo("C");
    }

    // ── Human-editability: minimal doc, no Pools section ─────────────────

    [Test]
    public async Task TryImport_NoPoolsSection_EmptyPools()
    {
        string xml = @"<Readouts>
  <Groups>
    <Group Name=""Raw"">
      <Tier>
        <Slot>Steel</Slot>
        <Slot>~Milk</Slot>
      </Tier>
    </Group>
  </Groups>
</Readouts>";

        bool ok = ReadoutsXml.TryImport(xml, out var pools, out var groups, out string error);
        await Assert.That(ok).IsTrue();
        await Assert.That(pools.Count).IsEqualTo(0);
        await Assert.That(groups.Count).IsEqualTo(1);
        await Assert.That(groups[0].Name).IsEqualTo("Raw");
        await Assert.That(string.Join(",", groups[0].Tiers[0])).IsEqualTo("Steel,~Milk");
    }

    [Test]
    public async Task TryImport_EmptyPoolsSection_EmptyPools()
    {
        string xml = @"<Readouts>
  <Pools/>
  <Groups>
    <Group Name=""G1"">
      <Tier><Slot>Steel</Slot></Tier>
    </Group>
  </Groups>
</Readouts>";

        bool ok = ReadoutsXml.TryImport(xml, out var pools, out _, out _);
        await Assert.That(ok).IsTrue();
        await Assert.That(pools.Count).IsEqualTo(0);
    }

    // ── Export: empty members/tiers not written ───────────────────────────

    [Test]
    public async Task Export_EmptySlots_NotIncluded()
    {
        var model = new ReadoutModel();
        model.CreateGroup(1, "G1");
        // No tiers set → empty group still exported but without Tier children
        string xml = ReadoutsXml.Export(model.Pools, model.InDisplayOrder());

        bool ok = ReadoutsXml.TryImport(xml, out _, out var groups, out _);
        await Assert.That(ok).IsTrue();
        await Assert.That(groups.Count).IsEqualTo(1);
        await Assert.That(groups[0].TierCount).IsEqualTo(0);
    }

    // ── Pool name with special XML chars round-trips ──────────────────────

    [Test]
    public async Task RoundTrip_PoolNameWithSpecialChars_VerbatimPreserved()
    {
        var model = new ReadoutModel();
        var pool = model.CreatePool(1, "Meat & Fish <Raw>");
        pool.Members.Add("Meat_Cow");
        var g = model.CreateGroup(10, "Food");
        model.SetTiers(10, new List<List<string>> { new() { "#1" } });

        string xml = ReadoutsXml.Export(model.Pools, model.InDisplayOrder());

        bool ok = ReadoutsXml.TryImport(xml, out var pools, out var groups, out _);
        await Assert.That(ok).IsTrue();
        await Assert.That(pools[0].Name).IsEqualTo("Meat & Fish <Raw>");

        var fresh = new ReadoutModel();
        fresh.ApplyImport(pools, groups, Counter(1), Counter(1));
        await Assert.That(fresh.Pools[0].Name).IsEqualTo("Meat & Fish <Raw>");
        // Slot should resolve to the new pool id
        await Assert.That(string.Join(",", fresh.InDisplayOrder()[0].Tiers[0])).IsEqualTo("#1");
    }

    // ── ApplyImport: clears existing state first ──────────────────────────

    [Test]
    public async Task ApplyImport_ClearsExistingPoolsGroupsThresholds()
    {
        var model = new ReadoutModel();
        model.CreatePool(99, "OldPool");
        model.CreateGroup(88, "OldGroup");
        model.SetThreshold("Steel", 100, 20);

        string xml = @"<Readouts>
  <Groups>
    <Group Name=""NewGroup"">
      <Tier><Slot>Plasteel</Slot></Tier>
    </Group>
  </Groups>
</Readouts>";

        ReadoutsXml.TryImport(xml, out var pools, out var groups, out _);
        model.ApplyImport(pools, groups, Counter(1), Counter(1));

        await Assert.That(model.Pools.Count).IsEqualTo(0);
        await Assert.That(model.Thresholds.Count).IsEqualTo(0);
        await Assert.That(model.Groups.Count).IsEqualTo(1);
        await Assert.That(model.Groups[0].Name).IsEqualTo("NewGroup");
    }

    // ── ApplyImport: duplicate pool names — last wins ─────────────────────

    [Test]
    public async Task ApplyImport_DuplicatePoolName_LastWins()
    {
        // Two pools with the same name; the slot should resolve to the second one's id
        string xml = @"<Readouts>
  <Pools>
    <Pool Name=""Dup"">
      <Member>Steel</Member>
    </Pool>
    <Pool Name=""Dup"">
      <Member>Gold</Member>
    </Pool>
  </Pools>
  <Groups>
    <Group Name=""G1"">
      <Tier><Slot>pool:Dup</Slot></Tier>
    </Group>
  </Groups>
</Readouts>";

        ReadoutsXml.TryImport(xml, out var pools, out var groups, out _);

        var fresh = new ReadoutModel();
        fresh.ApplyImport(pools, groups, Counter(1), Counter(1));

        // Two pools created: ids 1 and 2; last "Dup" → id 2
        await Assert.That(fresh.Pools.Count).IsEqualTo(2);
        var tier = fresh.InDisplayOrder()[0].Tiers[0];
        // "pool:Dup" should resolve to id 2 (last pool named "Dup")
        await Assert.That(string.Join(",", tier)).IsEqualTo("#2");
    }

    // ── ApplyImport: group display order matches file order ───────────────

    [Test]
    public async Task ApplyImport_GroupOrder_MatchesFileOrder()
    {
        string xml = @"<Readouts>
  <Groups>
    <Group Name=""Alpha""><Tier><Slot>A</Slot></Tier></Group>
    <Group Name=""Beta""><Tier><Slot>B</Slot></Tier></Group>
    <Group Name=""Gamma""><Tier><Slot>C</Slot></Tier></Group>
  </Groups>
</Readouts>";

        ReadoutsXml.TryImport(xml, out var pools, out var groups, out _);
        var fresh = new ReadoutModel();
        fresh.ApplyImport(pools, groups, Counter(1), Counter(1));

        var order = fresh.InDisplayOrder();
        await Assert.That(string.Join(",", order.Select(g => g.Name))).IsEqualTo("Alpha,Beta,Gamma");
    }

    // ── Icon attribute: present vs absent ────────────────────────────────

    [Test]
    public async Task Export_PoolWithIcon_IconAttributePresent()
    {
        var model = new ReadoutModel();
        var pool = model.CreatePool(1, "Meats");
        pool.IconDefName = "Meat_Cow";
        pool.Members.Add("Steel");

        string xml = ReadoutsXml.Export(model.Pools, model.InDisplayOrder());

        ReadoutsXml.TryImport(xml, out var pools, out _, out _);
        await Assert.That(pools[0].IconDefName).IsEqualTo("Meat_Cow");
    }

    [Test]
    public async Task Export_PoolWithoutIcon_IconAttributeAbsent()
    {
        var model = new ReadoutModel();
        var pool = model.CreatePool(1, "Metals");
        pool.Members.Add("Steel");

        string xml = ReadoutsXml.Export(model.Pools, model.InDisplayOrder());

        // No Icon attribute → parsed as null
        ReadoutsXml.TryImport(xml, out var pools, out _, out _);
        await Assert.That(pools[0].IconDefName).IsNull();
        // Also verify the raw XML does not contain Icon=
        await Assert.That(xml.Contains("Icon=")).IsFalse();
    }

    [Test]
    public async Task ApplyImport_EmptyToEmptyReportsNoChangedDomain()
    {
        var model = new ReadoutModel();

        ReadoutChange change = model.ApplyImport(
            new List<ResourcePool>(), new List<ReadoutGroup>(), Counter(), Counter());

        await Assert.That(change).IsEqualTo(ReadoutChange.None);
    }

    [Test]
    public async Task ApplyImport_ReportsOnlyDomainsWhoseContentsAreReplaced()
    {
        var model = new ReadoutModel();
        model.CreateGroup(7, "Old");
        var groups = new List<ReadoutGroup>
        {
            new ReadoutGroup { Name = "New" },
        };

        ReadoutChange change = model.ApplyImport(
            new List<ResourcePool>(), groups, Counter(), Counter());

        await Assert.That(change).IsEqualTo(ReadoutChange.Groups);
    }
}
