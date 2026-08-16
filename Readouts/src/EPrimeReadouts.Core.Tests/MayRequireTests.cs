using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Tests for MayRequire/MayRequireAnyOf conditionals in the Readouts XML
/// format: import-side filtering (TryImport with an isModActive predicate)
/// and export-side derivation (Export with a packageIdOf resolver).
public class MayRequireTests
{
    private const string Biotech = "ludeon.rimworld.biotech";
    private const string Anomaly = "ludeon.rimworld.anomaly";

    private static Func<int> Counter(int start = 1)
    {
        int n = start;
        return () => n++;
    }

    /// Predicate treating exactly the given packageIds as active (case-insensitive).
    private static Func<string, bool> Active(params string[] ids)
    {
        var set = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        return id => set.Contains(id);
    }

    /// Resolver mapping member keys (defName or "@Category") to packageIds;
    /// unmapped keys are vanilla (null).
    private static Func<string, string> Packages(params (string key, string id)[] map)
    {
        var d = new Dictionary<string, string>();
        foreach (var (key, id) in map) d[key] = id;
        return key => d.TryGetValue(key, out var v) ? v : null;
    }

    // ── Import: member filtering ──────────────────────────────────────────

    private const string MemberXml = @"<Readouts>
  <Pools>
    <Pool Name=""Sera"">
      <Member>MechSerumHealer</Member>
      <Member MayRequire=""ludeon.rimworld.anomaly"">VoidsightSerum</Member>
    </Pool>
  </Pools>
</Readouts>";

    [Test]
    public async Task TryImport_GatedMember_InactiveMod_Dropped()
    {
        ReadoutsXml.TryImport(MemberXml, out var pools, out _, out _, Active());
        await Assert.That(string.Join(",", pools[0].Members)).IsEqualTo("MechSerumHealer");
    }

    [Test]
    public async Task TryImport_GatedMember_ActiveMod_Kept()
    {
        ReadoutsXml.TryImport(MemberXml, out var pools, out _, out _, Active(Anomaly));
        await Assert.That(string.Join(",", pools[0].Members))
            .IsEqualTo("MechSerumHealer,VoidsightSerum");
    }

    [Test]
    public async Task TryImport_NullPredicate_KeepsGatedEntries()
    {
        ReadoutsXml.TryImport(MemberXml, out var pools, out _, out _);
        await Assert.That(string.Join(",", pools[0].Members))
            .IsEqualTo("MechSerumHealer,VoidsightSerum");
    }

    // ── Import: pool filtering cascades to referencing slots ──────────────

    [Test]
    public async Task TryImport_GatedPool_Inactive_PoolAndReferencingSlotDropped()
    {
        string xml = @"<Readouts>
  <Pools>
    <Pool Name=""Mechs"" MayRequire=""ludeon.rimworld.biotech"">
      <Member>SignalChip</Member>
    </Pool>
  </Pools>
  <Groups>
    <Group Name=""Wealth"">
      <Tier>
        <Slot>Silver</Slot>
        <Slot>pool:Mechs</Slot>
      </Tier>
    </Group>
  </Groups>
</Readouts>";

        ReadoutsXml.TryImport(xml, out var pools, out var groups, out _, Active());
        await Assert.That(pools.Count).IsEqualTo(0);

        var fresh = new ReadoutModel();
        fresh.ApplyImport(pools, groups, Counter(), Counter());
        await Assert.That(string.Join(",", fresh.InDisplayOrder()[0].Tiers[0]))
            .IsEqualTo("Silver");
    }

    // ── Import: group, tier, and slot filtering ───────────────────────────

    private const string GroupTierSlotXml = @"<Readouts>
  <Groups>
    <Group Name=""Anomaly"" MayRequire=""ludeon.rimworld.anomaly"">
      <Tier><Slot>Bioferrite</Slot></Tier>
    </Group>
    <Group Name=""Mixed"">
      <Tier>
        <Slot>Steel</Slot>
        <Slot MayRequire=""ludeon.rimworld.biotech"">~HemogenPack</Slot>
      </Tier>
      <Tier MayRequire=""ludeon.rimworld.biotech"">
        <Slot>SignalChip</Slot>
      </Tier>
    </Group>
  </Groups>
</Readouts>";

    [Test]
    public async Task TryImport_GatedGroupTierSlot_Inactive_AllDropped()
    {
        ReadoutsXml.TryImport(GroupTierSlotXml, out _, out var groups, out _, Active());
        await Assert.That(groups.Count).IsEqualTo(1);
        await Assert.That(groups[0].Name).IsEqualTo("Mixed");
        await Assert.That(groups[0].TierCount).IsEqualTo(1);
        await Assert.That(string.Join(",", groups[0].Tiers[0])).IsEqualTo("Steel");
    }

    [Test]
    public async Task TryImport_GatedGroupTierSlot_Active_AllKept()
    {
        ReadoutsXml.TryImport(GroupTierSlotXml, out _, out var groups, out _,
            Active(Anomaly, Biotech));
        await Assert.That(groups.Count).IsEqualTo(2);
        await Assert.That(groups[1].TierCount).IsEqualTo(2);
        await Assert.That(string.Join(",", groups[1].Tiers[0])).IsEqualTo("Steel,~HemogenPack");
        await Assert.That(string.Join(",", groups[1].Tiers[1])).IsEqualTo("SignalChip");
    }

    // ── Import: comma list = all required; MayRequireAnyOf = any ──────────

    private const string CommaListXml = @"<Readouts>
  <Pools>
    <Pool Name=""P"">
      <Member MayRequire=""ludeon.rimworld.biotech, ludeon.rimworld.anomaly"">Both</Member>
      <Member MayRequireAnyOf=""ludeon.rimworld.biotech, ludeon.rimworld.anomaly"">Either</Member>
    </Pool>
  </Pools>
</Readouts>";

    [Test]
    public async Task TryImport_MayRequireList_RequiresAll()
    {
        ReadoutsXml.TryImport(CommaListXml, out var pools, out _, out _, Active(Biotech));
        await Assert.That(string.Join(",", pools[0].Members)).IsEqualTo("Either");

        ReadoutsXml.TryImport(CommaListXml, out pools, out _, out _, Active(Biotech, Anomaly));
        await Assert.That(string.Join(",", pools[0].Members)).IsEqualTo("Both,Either");
    }

    [Test]
    public async Task TryImport_MayRequireAnyOf_RequiresAny()
    {
        ReadoutsXml.TryImport(CommaListXml, out var pools, out _, out _, Active());
        await Assert.That(pools[0].Members.Count).IsEqualTo(0);

        ReadoutsXml.TryImport(CommaListXml, out pools, out _, out _, Active(Anomaly));
        await Assert.That(string.Join(",", pools[0].Members)).IsEqualTo("Either");
    }

    // ── Export: member and slot derivation ────────────────────────────────

    [Test]
    public async Task Export_RestrictedMemberAndSlot_EmitMayRequire()
    {
        var model = new ReadoutModel();
        var pool = model.CreatePool(1, "Sera");
        pool.Members.Add("MechSerumHealer");
        pool.Members.Add("VoidsightSerum");
        model.CreateGroup(10, "G");
        model.SetTiers(10, new List<List<string>> { new() { "Steel", "~HemogenPack" } });

        string xml = ReadoutsXml.Export(model.Pools, model.InDisplayOrder(),
            Packages(("VoidsightSerum", Anomaly), ("HemogenPack", Biotech)));

        // Without the DLCs, the derived attributes filter out the tagged entries
        ReadoutsXml.TryImport(xml, out var pools, out var groups, out _, Active());
        await Assert.That(string.Join(",", pools[0].Members)).IsEqualTo("MechSerumHealer");
        await Assert.That(string.Join(",", groups[0].Tiers[0])).IsEqualTo("Steel");

        // With the DLCs everything survives, flag preserved
        ReadoutsXml.TryImport(xml, out pools, out groups, out _, Active(Anomaly, Biotech));
        await Assert.That(string.Join(",", pools[0].Members))
            .IsEqualTo("MechSerumHealer,VoidsightSerum");
        await Assert.That(string.Join(",", groups[0].Tiers[0])).IsEqualTo("Steel,~HemogenPack");
    }

    // ── Export: pool-level MayRequireAnyOf when ALL members restricted ────

    [Test]
    public async Task Export_AllRestrictedPool_EmitsMayRequireAnyOf_PoolDroppedWithoutDlc()
    {
        var model = new ReadoutModel();
        var pool = model.CreatePool(1, "Mechs");
        pool.Members.Add("SignalChip");
        pool.Members.Add("VoidsightSerum");
        model.CreateGroup(10, "Wealth");
        model.SetTiers(10, new List<List<string>> { new() { "Silver", "#1" } });

        string xml = ReadoutsXml.Export(model.Pools, model.InDisplayOrder(),
            Packages(("SignalChip", Biotech), ("VoidsightSerum", Anomaly)));

        await Assert.That(xml.Contains(
            "MayRequireAnyOf=\"ludeon.rimworld.biotech,ludeon.rimworld.anomaly\"")).IsTrue();

        // Neither DLC active → pool gone, referencing slot gone
        ReadoutsXml.TryImport(xml, out var pools, out var groups, out _, Active());
        await Assert.That(pools.Count).IsEqualTo(0);
        var fresh = new ReadoutModel();
        fresh.ApplyImport(pools, groups, Counter(), Counter());
        await Assert.That(string.Join(",", fresh.InDisplayOrder()[0].Tiers[0]))
            .IsEqualTo("Silver");

        // One of them active → pool survives with the surviving member
        ReadoutsXml.TryImport(xml, out pools, out groups, out _, Active(Biotech));
        await Assert.That(pools.Count).IsEqualTo(1);
        await Assert.That(string.Join(",", pools[0].Members)).IsEqualTo("SignalChip");
    }

    [Test]
    public async Task Export_MixedPool_NoPoolLevelAttribute()
    {
        var model = new ReadoutModel();
        var pool = model.CreatePool(1, "Sera");
        pool.Members.Add("MechSerumHealer");
        pool.Members.Add("VoidsightSerum");

        string xml = ReadoutsXml.Export(model.Pools, model.InDisplayOrder(),
            Packages(("VoidsightSerum", Anomaly)));

        ReadoutsXml.TryImport(xml, out var pools, out _, out _, Active());
        await Assert.That(pools.Count).IsEqualTo(1);
        await Assert.That(string.Join(",", pools[0].Members)).IsEqualTo("MechSerumHealer");
    }

    // ── Export: group-level MayRequireAnyOf when ALL slots restricted ─────

    [Test]
    public async Task Export_AllRestrictedGroup_EmitsMayRequireAnyOf_GroupDroppedWithoutDlc()
    {
        var model = new ReadoutModel();
        var pool = model.CreatePool(1, "Mechs");
        pool.Members.Add("SignalChip");
        model.CreateGroup(10, "DlcOnly");
        model.SetTiers(10, new List<List<string>> { new() { "#1", "Bioferrite" } });

        string xml = ReadoutsXml.Export(model.Pools, model.InDisplayOrder(),
            Packages(("SignalChip", Biotech), ("Bioferrite", Anomaly)));

        ReadoutsXml.TryImport(xml, out _, out var groups, out _, Active());
        await Assert.That(groups.Count).IsEqualTo(0);

        // Anomaly only: group survives; the slot into the Biotech-gated pool is
        // dropped by ApplyImport's unknown-pool resolution.
        ReadoutsXml.TryImport(xml, out var pools, out groups, out _, Active(Anomaly));
        await Assert.That(groups.Count).IsEqualTo(1);
        var fresh = new ReadoutModel();
        fresh.ApplyImport(pools, groups, Counter(), Counter());
        await Assert.That(string.Join(",", fresh.InDisplayOrder()[0].Tiers[0]))
            .IsEqualTo("Bioferrite");
    }

    [Test]
    public async Task Export_GroupWithSlotIntoMixedPool_NotGated()
    {
        var model = new ReadoutModel();
        var pool = model.CreatePool(1, "Sera");
        pool.Members.Add("MechSerumHealer");
        pool.Members.Add("VoidsightSerum");
        model.CreateGroup(10, "G");
        model.SetTiers(10, new List<List<string>> { new() { "#1" } });

        string xml = ReadoutsXml.Export(model.Pools, model.InDisplayOrder(),
            Packages(("VoidsightSerum", Anomaly)));

        // Mixed pool is unrestricted → group referencing it is unrestricted
        ReadoutsXml.TryImport(xml, out var pools, out var groups, out _, Active());
        await Assert.That(groups.Count).IsEqualTo(1);
        var fresh = new ReadoutModel();
        fresh.ApplyImport(pools, groups, Counter(), Counter());
        await Assert.That(string.Join(",", fresh.InDisplayOrder()[0].Tiers[0])).IsEqualTo("#1");
    }

    // ── Export: category members resolve via "@Category" key ──────────────

    [Test]
    public async Task Export_RestrictedCategoryMember_EmitsMayRequire()
    {
        var model = new ReadoutModel();
        var pool = model.CreatePool(1, "Hard Drugs");
        pool.Members.Add("@DavaiHardDrugs");
        pool.Members.Add("Yayo");

        string xml = ReadoutsXml.Export(model.Pools, model.InDisplayOrder(),
            Packages(("@DavaiHardDrugs", "davai.drugs")));

        ReadoutsXml.TryImport(xml, out var pools, out _, out _, Active());
        await Assert.That(string.Join(",", pools[0].Members)).IsEqualTo("Yayo");
    }

    // ── Shipped seed file honors its own annotations ──────────────────────

    private static string SeedXml()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null &&
               !File.Exists(Path.Combine(directory.FullName, "src", "EPrimeReadouts.sln")))
            directory = directory.Parent;

        if (directory == null)
            throw new InvalidOperationException("repository root not found");

        return File.ReadAllText(Path.Combine(
            directory.FullName, "mod", "Seed", "Readouts.xml"));
    }

    [Test]
    public async Task SeedFile_CoreOnly_DropsAllDlcAndModContent()
    {
        bool ok = ReadoutsXml.TryImport(SeedXml(), out var pools, out var groups,
            out string error, Active());
        await Assert.That(ok).IsTrue();
        await Assert.That(error).IsNull();

        // The all-Biotech Mechs pool is gone; the other 13 pools remain
        await Assert.That(pools.Any(p => p.Name == "Mechs")).IsFalse();
        await Assert.That(pools.Count).IsEqualTo(13);

        // No surviving member or slot mentions DLC/mod-only content
        var members = pools.SelectMany(p => p.Members).ToList();
        await Assert.That(members.Any(m => m.StartsWith("@Davai"))).IsFalse();
        await Assert.That(members.Any(m => m.StartsWith("Fish_"))).IsFalse();
        await Assert.That(members.Contains("Meat_WasteRat")).IsFalse();
        await Assert.That(members.Contains("Meat_Dryad_Basic")).IsFalse();
        await Assert.That(members.Contains("Shell_Deadlife")).IsFalse();
        await Assert.That(members.Contains("VoidsightSerum")).IsFalse();

        var slots = groups.SelectMany(g => g.Tiers).SelectMany(t => t).ToList();
        await Assert.That(slots.Contains("BabyFood")).IsFalse();
        await Assert.That(slots.Contains("~HemogenPack")).IsFalse();
        await Assert.That(slots.Contains("~Bioferrite")).IsFalse();
        await Assert.That(slots.Contains("~GravlitePanel")).IsFalse();

        // Core content is untouched: all 7 groups, core members intact
        await Assert.That(groups.Count).IsEqualTo(7);
        await Assert.That(members.Contains("Meat_Cow")).IsTrue();
        await Assert.That(members.Contains("Shell_EMP")).IsTrue();
        await Assert.That(slots.Contains("Steel")).IsTrue();
    }

    [Test]
    public async Task SeedFile_AllActive_KeepsEverything()
    {
        bool ok = ReadoutsXml.TryImport(SeedXml(), out var pools, out var groups,
            out _, Active("Ludeon.RimWorld.Ideology", "Ludeon.RimWorld.Biotech",
                "Ludeon.RimWorld.Anomaly", "Ludeon.RimWorld.Odyssey",
                "davai.sortedcategories"));
        await Assert.That(ok).IsTrue();

        await Assert.That(pools.Count).IsEqualTo(14);
        var members = pools.SelectMany(p => p.Members).ToList();
        await Assert.That(members.Contains("Meat_WasteRat")).IsTrue();
        await Assert.That(members.Contains("Fish_Toxfish")).IsTrue();
        await Assert.That(members.Contains("@DavaiSera")).IsTrue();

        var slots = groups.SelectMany(g => g.Tiers).SelectMany(t => t).ToList();
        await Assert.That(slots.Contains("BabyFood")).IsTrue();
        await Assert.That(slots.Contains("~Shard")).IsTrue();
    }

    // ── Export: no resolver → no attributes (existing behavior) ───────────

    [Test]
    public async Task Export_NullResolver_EmitsNoAttributes()
    {
        var model = new ReadoutModel();
        var pool = model.CreatePool(1, "Mechs");
        pool.Members.Add("SignalChip");

        string xml = ReadoutsXml.Export(model.Pools, model.InDisplayOrder());
        await Assert.That(xml.Contains("MayRequire")).IsFalse();
    }
}
