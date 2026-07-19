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
}
