using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class CoverageMathTests
{
    private static readonly FakeCatalog Catalog = new FakeCatalog()
        .WithWorkType("Cooking", "Cook", "Butcher", "Brew")
        .WithWorkType("Hauling", "HaulGeneral");

    private static JobEntry Type(string name) => new(JobEntryKind.WorkType, name);
    private static JobEntry Giver(string name) => new(JobEntryKind.WorkGiver, name);

    [Test]
    public async Task CoverageExpandsWorkTypesAndSkipsUnknownEntries()
    {
        var coverage = CoverageMath.CoverageOf(
            new[] { Type("Cooking"), Giver("HaulGeneral"), Giver("ModdedGone"), Type("ModdedType") },
            Catalog);
        await Assert.That(coverage).IsEquivalentTo(new[] { "Cook", "Butcher", "Brew", "HaulGeneral" });
    }

    [Test]
    public async Task OrderedCoverageFollowsEntryOrderExpandsInCatalogOrderAndDedupes()
    {
        // Giver first, then its own work type: the giver keeps its earlier
        // slot, the expansion appends only the remaining givers; unknown
        // entries contribute nothing.
        var ordered = CoverageMath.OrderedCoverageOf(
            new[] { Giver("Butcher"), Type("ModdedType"), Type("Cooking"), Giver("HaulGeneral") },
            Catalog);
        await Assert.That(string.Join(",", ordered)).IsEqualTo("Butcher,Cook,Brew,HaulGeneral");
    }

    [Test]
    public async Task WorkTypeCoversItsGiverSubsetHoweverSpelled()
    {
        var cookType = CoverageMath.CoverageOf(new[] { Type("Cooking") }, Catalog);
        var butcher = CoverageMath.CoverageOf(new[] { Giver("Butcher") }, Catalog);
        await Assert.That(CoverageMath.Covers(cookType, butcher)).IsTrue();
        await Assert.That(CoverageMath.Covers(butcher, cookType)).IsFalse();
    }

    [Test]
    public async Task EqualCoverageIsNotCoveredButIsMutuallyRedundantByLowerId()
    {
        var asType = CoverageMath.CoverageOf(new[] { Type("Cooking") }, Catalog);
        var asGivers = CoverageMath.CoverageOf(
            new[] { Giver("Cook"), Giver("Butcher"), Giver("Brew") }, Catalog);

        await Assert.That(CoverageMath.Covers(asType, asGivers)).IsFalse();
        await Assert.That(CoverageMath.Covers(asGivers, asType)).IsFalse();
        await Assert.That(CoverageMath.CoversOrMatches(asType, asGivers)).IsTrue();
        await Assert.That(CoverageMath.CoversOrMatches(asGivers, asType)).IsTrue();
        // Only the lower id drops the other — never both ways.
        await Assert.That(CoverageMath.MakesRedundant(asType, 3, asGivers, 7)).IsTrue();
        await Assert.That(CoverageMath.MakesRedundant(asGivers, 7, asType, 3)).IsFalse();
    }

    [Test]
    public async Task EmptyCoverageNeitherCoversNorIsCovered()
    {
        var empty = CoverageMath.CoverageOf(new JobEntry[0], Catalog);
        var cook = CoverageMath.CoverageOf(new[] { Type("Cooking") }, Catalog);
        await Assert.That(CoverageMath.Covers(cook, empty)).IsFalse();
        await Assert.That(CoverageMath.Covers(empty, cook)).IsFalse();
        await Assert.That(CoverageMath.MakesRedundant(empty, 1, empty, 2)).IsFalse();
    }

    [Test]
    public async Task SameIdNeverMakesItselfRedundant()
    {
        var cook = CoverageMath.CoverageOf(new[] { Type("Cooking") }, Catalog);
        await Assert.That(CoverageMath.MakesRedundant(cook, 5, cook, 5)).IsFalse();
    }

    [Test]
    public async Task ImmediateCoverageShadowsDescendantsButNeverEquals()
    {
        var cooking = CoverageMath.CoverageOf(new[] { Type("Cooking") }, Catalog);
        var butcherBrew = CoverageMath.CoverageOf(
            new[] { Giver("Butcher"), Giver("Brew") }, Catalog);
        var butcher = CoverageMath.CoverageOf(new[] { Giver("Butcher") }, Catalog);
        var haul = CoverageMath.CoverageOf(new[] { Giver("HaulGeneral") }, Catalog);
        var cookingAgain = CoverageMath.CoverageOf(
            new[] { Giver("Cook"), Giver("Butcher"), Giver("Brew") }, Catalog);

        // Both cooking subsets hide behind full cooking; the incomparable haul
        // survives; equal coverages both survive (Covers is strict).
        var immediate = CoverageMath.ImmediatelyCoveredIndexes(
            new[] { cooking, butcherBrew, butcher, haul, cookingAgain });
        await Assert.That(immediate).IsEquivalentTo(new[] { 0, 3, 4 });

        // Without full cooking present, Butcher+Brew surfaces and shadows Butcher.
        var reduced = CoverageMath.ImmediatelyCoveredIndexes(
            new[] { butcherBrew, butcher, haul });
        await Assert.That(reduced).IsEquivalentTo(new[] { 0, 2 });

        await Assert.That(CoverageMath.ImmediatelyCoveredIndexes(
            new List<HashSet<string>>())).IsEmpty();
    }

    [Test]
    public async Task FirstCoveredIndexFindsTheEarliestGiverOrMaxValue()
    {
        var ordered = CoverageMath.OrderedCoverageOf(
            new[] { Type("Cooking"), Giver("HaulGeneral") }, Catalog);
        var brewHaul = CoverageMath.CoverageOf(
            new[] { Giver("Brew"), Giver("HaulGeneral") }, Catalog);
        var haul = CoverageMath.CoverageOf(new[] { Giver("HaulGeneral") }, Catalog);
        var none = CoverageMath.CoverageOf(new[] { Giver("ModdedGone") }, Catalog);

        // Cooking expands Cook,Butcher,Brew: Brew sits at index 2.
        await Assert.That(CoverageMath.FirstCoveredIndex(ordered, brewHaul)).IsEqualTo(2);
        await Assert.That(CoverageMath.FirstCoveredIndex(ordered, haul)).IsEqualTo(3);
        await Assert.That(CoverageMath.FirstCoveredIndex(ordered, none)).IsEqualTo(int.MaxValue);
    }
}
