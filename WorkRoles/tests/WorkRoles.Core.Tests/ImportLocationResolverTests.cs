using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class ImportLocationResolverTests
{
    private static readonly List<LocationInfo> IssuerLocations = new()
    {
        new LocationInfo("4", "Rimosa", false),
        new LocationInfo("9", "The Wanderer", true),
    };

    [Test]
    [Arguments("settlements", "settlements")]
    [Arguments("caravans", "caravans")]
    [Arguments("settlement:RIMOSA", "settlement:4")]
    [Arguments("ship:The Wanderer", "ship:9")]
    [Arguments("settlement:Missing", null)]
    public async Task IssuerResolvesFileNamesToInvariantRuntimeTokens(
        string fileToken, string expected)
    {
        await Assert.That(ImportLocationResolver.Resolve(fileToken, IssuerLocations))
            .IsEqualTo(expected);
    }

    [Test]
    public async Task AppliedMappingDoesNotConsultAnotherClientsLabels()
    {
        var resolved = ImportLocationResolver.BuildMap(
            new[] { "ship:The Wanderer", "settlement:Missing" },
            new[] { "ship:9", "" });

        await Assert.That(ImportLocationResolver.FromMap(
            "ship:The Wanderer", resolved)).IsEqualTo("ship:9");
        await Assert.That(ImportLocationResolver.FromMap(
            "settlement:Missing", resolved)).IsNull();
        await Assert.That(ImportLocationResolver.FromMap(
            "ship:Localized Elsewhere", resolved)).IsNull();
    }
}
