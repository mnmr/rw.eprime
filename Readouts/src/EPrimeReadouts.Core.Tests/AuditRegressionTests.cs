using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class AuditRegressionTests
{
    [Test]
    public async Task UnfinishedBillCreditsOnePaidAttemptWhileReservingLaterCraftsAndQualityRetries()
    {
        // A duster consumes 80 cloth before its unfinished item exists. With
        // two accepted dusters still owed, only the current attempt is paid.
        await Assert.That(PlannedWorkMath.BillDebt(80, 1, 1f, firstAttemptPaid: true)).IsEqualTo(0);
        await Assert.That(PlannedWorkMath.BillDebt(80, 2, 1f, firstAttemptPaid: true)).IsEqualTo(80);
        await Assert.That(PlannedWorkMath.BillDebt(80, 2, 2f, firstAttemptPaid: true)).IsEqualTo(240);
        await Assert.That(PlannedWorkMath.BillDebt(80, 2, 2f)).IsEqualTo(320);
    }

    [Test]
    public async Task FillingAvailableTiersCannotMakeDefaultRetentionDropSlotsOnReload()
    {
        var source = new ReadoutModel();
        source.CreateGroup(10, "Industry");
        source.SetTiers(10, new() { new() { "Bioferrite" }, new() { "Steel" } });
        var session = new DefaultSession(SetupDocument.Capture(source).ToXml(Package));
        await Assert.That(session.Model.AddGroupSlot(10, "WoodLog", 1, -1)).IsTrue();
        await Assert.That(session.Model.AddGroupSlot(10, "Silver", 2, -1)).IsTrue();
        var restored = session.Flush();
        await Assert.That(Tiers(restored)).IsEqualTo("Bioferrite,Steel | WoodLog | Silver");
        await Assert.That(Tiers(Apply(Read(session.Saved.ToXml(Package), false)))).IsEqualTo("Steel | WoodLog | Silver");
        session.Model.SetThreshold("Steel", 100, 20);
        await Assert.That(Tiers(session.Flush())).IsEqualTo("Bioferrite,Steel | WoodLog | Silver");
    }

    [Test]
    [Arguments("Leathers", "Leder", "Leather_Plain")]
    [Arguments("Stone blocks", "Steinblöcke", "BlocksGranite")]
    public async Task PoolHighlightsFollowDisplayedNamesAcrossLanguageChangesAndRenames(string seedName, string translated, string member)
    {
        var catalog = new FakeResourceCatalog().With(member, member);
        var pool = new ResourcePool { Id = 1, Name = seedName, SeedName = seedName, Members = new() { member } };
        var input = new LayoutInput
        {
            Catalog = catalog, Pools = PoolSnapshot.Build(new[] { pool }, catalog),
            Groups = new() { new() { Id = 1, Name = "Textiles", Tiers = new() { new() { "#1" } } } },
            Counts = new Dictionary<string, int> { [member] = 20 }, Thresholds = new(),
            SearchText = translated, SearchCulture = System.Globalization.CultureInfo.GetCultureInfo("de-DE"),
            TranslateSeedName = name => name == seedName ? translated : name
        };
        bool Highlight() => ReadoutLayoutEngine.Build(input).Cells.Any(c => c.Kind == CellKind.Highlight && c.Token == "#1");
        await Assert.That(Highlight()).IsTrue();
        input.SearchText = seedName;
        await Assert.That(Highlight()).IsFalse();
        input.TranslateSeedName = name => name;
        await Assert.That(Highlight()).IsTrue();
        input.TranslateSeedName = name => name == seedName ? translated : name;
        pool.SeedName = null; // Legacy/custom names must never be inferred to be seeded.
        input.Pools = PoolSnapshot.Build(new[] { pool }, catalog);
        await Assert.That(Highlight()).IsTrue();
        pool.SeedName = seedName;
        pool.Name = "Mein Leder";
        input.Pools = PoolSnapshot.Build(new[] { pool }, catalog);
        input.SearchText = "Mein Leder";
        await Assert.That(Highlight()).IsTrue();
    }

    [Test]
    public async Task ClearingNestedCategoryRemovesInheritedCoverageAndPreservesOtherResources()
    {
        var catalog = new FakeResourceCatalog()
            .With("Cloth", "cloth").With("DevilstrandCloth", "devilstrand")
            .With("Leather_Plain", "plainleather").With("Leather_Heavy", "heavy fur")
            .With("Steel", "steel")
            .WithCategory("Textiles", "textiles", "Cloth", "DevilstrandCloth", "Leather_Plain", "Leather_Heavy")
            .WithCategory("Leathers", "leathers", "Leather_Plain", "Leather_Heavy");
        var members = PoolTriState.SetCategoryScope(new() { "Steel" }, "Textiles",
            catalog.CountedDefsIn("Textiles"), true, catalog);
        members = PoolTriState.SetCategoryScope(members, "Leathers",
            catalog.CountedDefsIn("Leathers"), false, catalog);
        await Assert.That(PoolTriState.CategoryState(members, "Leathers", catalog)).IsEqualTo(TriState.Off);
        await Assert.That(PoolTriState.IsSelected(members, "Cloth", catalog)).IsTrue();
        await Assert.That(PoolTriState.IsSelected(members, "DevilstrandCloth", catalog)).IsTrue();
        await Assert.That(PoolTriState.IsSelected(members, "Steel", catalog)).IsTrue();

        members = PoolTriState.SetCategoryScope(members, "Leathers",
            catalog.CountedDefsIn("Leathers"), true, catalog);
        members = PoolTriState.SetCategoryScope(members, "Textiles",
            catalog.CountedDefsIn("Textiles"), true, catalog);
        members = PoolTriState.SetCategoryScope(members, "Textiles",
            catalog.CountedDefsIn("Textiles"), false, catalog);
        await Assert.That(PoolTriState.CategoryState(members, "Textiles", catalog)).IsEqualTo(TriState.Off);
        await Assert.That(PoolTriState.IsSelected(members, "Steel", catalog)).IsTrue();

        // Clearing a partial category is also an explicit removal request.
        members = PoolTriState.SetCategoryScope(new() { "Cloth", "Steel" }, "Textiles",
            catalog.CountedDefsIn("Textiles"), false, catalog);
        await Assert.That(PoolTriState.CategoryState(members, "Textiles", catalog)).IsEqualTo(TriState.Off);
    }

    [Test]
    public async Task DefaultUpdatesPreserveDormantTierPlacementAcrossOrdinaryEditsAndReloads()
    {
        var source = new ReadoutModel();
        source.CreateGroup(10, "Industry");
        source.SetTiers(10, new() { new() { "Bioferrite" }, new() { "Steel" } });
        var session = new DefaultSession(SetupDocument.Capture(source).ToXml(Package));
        session.Model.SetGroupSlotShowWhenZero(10, "Steel", false);
        await Assert.That(Tiers(session.Flush())).IsEqualTo("Bioferrite | ~Steel");
        session.Model.SetThreshold("Steel", 100, 20);
        await Assert.That(Tiers(session.Flush())).IsEqualTo("Bioferrite | ~Steel");
        session.Model.AddGroupSlot(10, "WoodLog", 0, -1);
        await Assert.That(Tiers(session.Flush())).IsEqualTo("Bioferrite | ~Steel,WoodLog");
    }

    [Test]
    public async Task ReusingDormantNamesPreservesBothObjectsAndKeepsTheirOwnRulesAcrossBatches()
    {
        var source = new ReadoutModel();
        source.CreatePool(1, "Metals").Members.Add("Bioferrite");
        source.SetPoolIcon(1, "Bioferrite");
        source.CreateGroup(10, "Industry");
        source.AddGroupSlot(10, "#1", 0, -1);
        source.SetThreshold("#1", 50, 10);
        source.SetCountRule("#1", new CountRule(BasisOverride.ForceOn, BasisOverride.ForceOff));
        var player = new PlayerSetup();
        player.EnabledGroups["Industry"] = false;
        var session = new DefaultSession(SetupDocument.Capture(source, player).ToXml(Package));
        await Assert.That(session.Model.Pools.Count).IsEqualTo(0);
        await Assert.That(session.Model.Groups.Count).IsEqualTo(0);
        session.Model.CreatePool(1, "Metals").Members.Add("Steel");
        session.Model.CreateGroup(10, "Industry");
        session.Model.AddGroupSlot(10, "WoodLog", 0, -1);
        for (int batch = 0; batch < 3; batch++)
        {
            if (batch == 1)
            {
                session.Model.RenamePool(1, "Construction materials");
                session.Model.RenameGroup(10, "Production");
            }
            if (batch == 2) session.Model.SetThreshold("Steel", 200, 40);
            var restored = session.Flush();
            await Assert.That(restored.Pools.Count).IsEqualTo(2);
            await Assert.That(restored.Groups.Count).IsEqualTo(2);
            var dormant = restored.Pools.Single(p => p.Members.Contains("Bioferrite"));
            var replacement = restored.Pools.Single(p => p.Members.Contains("Steel"));
            await Assert.That(dormant.IconDefName).IsEqualTo("Bioferrite");
            await Assert.That(restored.Thresholds["#" + dormant.Id].Low).IsEqualTo(50);
            await Assert.That(restored.CountRules["#" + dormant.Id].StorageOnly).IsEqualTo(BasisOverride.ForceOn);
            await Assert.That(restored.Thresholds.ContainsKey("#" + replacement.Id)).IsFalse();
            await Assert.That(restored.CountRules.ContainsKey("#" + replacement.Id)).IsFalse();
            var dormantGroup = restored.Groups.Single(g => TierOps.Contains(g.Tiers, "#" + dormant.Id));
            await Assert.That(session.Saved.Player!.EnabledGroups[dormantGroup.Name]).IsFalse();
            await Assert.That(restored.Groups.Any(g => TierOps.Contains(g.Tiers, "WoodLog"))).IsTrue();
        }
    }

    private static string Tiers(ReadoutModel model) => string.Join(" | ", model.Groups[0].Tiers.Select(t => string.Join(",", t)));
    private static string? Package(string token) => SlotToken.Canonical(token) == "Bioferrite" ? "ludeon.rimworld.anomaly" : null;
    private static Func<int> Ids(int first) => () => first++;
    private static SetupDocument Read(string xml, bool active)
    {
        if (!SetupDocument.TryRead(xml, out var document, out var error, _ => active,
            token => active || SlotToken.Canonical(token) != "Bioferrite")) throw new Exception(error);
        return document!;
    }
    private static ReadoutModel Apply(SetupDocument document)
    {
        var model = new ReadoutModel();
        if (!document.TryApply(model, SetupSections.Model, Ids(1), Ids(10), out _, out var error)) throw new Exception(error);
        return model;
    }
    private sealed class DefaultSession
    {
        public readonly ReadoutModel Model;
        public SetupDocument Saved;
        private SetupDocument baseline;
        public DefaultSession(string xml)
        {
            Saved = Read(xml, true);
            Model = Apply(Read(xml, false));
            baseline = SetupDocument.Capture(Model);
        }
        public ReadoutModel Flush()
        {
            // Match SetupDefaults' detached document and available baseline across dirty batches.
            var current = SetupDocument.Capture(Model, new PlayerSetup());
            var nextBaseline = SetupDocument.Capture(Model);
            current.RetainUnavailableFrom(Saved, baseline);
            Saved = Read(current.ToXml(Package), true);
            baseline = nextBaseline;
            return Apply(Saved);
        }
    }
}
