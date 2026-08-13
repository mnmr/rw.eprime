namespace WorkRoles.Core.Tests.Persistence;

public class ImportLocationResolverTests
{
    private static readonly List<LocationInfo> IssuerLocations = new() { new LocationInfo("4", "Rimosa", false), new LocationInfo("9", "The Wanderer", true) };

    [Test]
    [Arguments("settlements", "settlements")]
    [Arguments("caravans", "caravans")]
    [Arguments("nowhere", "nowhere")]
    [Arguments("settlement:RIMOSA", "settlement:4")]
    [Arguments("ship:The Wanderer", "ship:9")]
    [Arguments("settlement:Missing", null)]
    public async Task IssuerResolvesFileNamesToInvariantRuntimeTokens(string fileToken, string expected)
    {
        await Assert.That(ImportLocationResolver.Resolve(fileToken, IssuerLocations)).IsEqualTo(expected);
    }

    [Test]
    [Arguments("ship:The Wanderer", "ship:9")]
    [Arguments("settlement:Missing", null)]
    [Arguments("ship:Localized Elsewhere", null)]
    [Arguments("nowhere", "nowhere")]
    public async Task AppliedMappingDoesNotConsultAnotherClientsLabels(string fileToken, string expectedRuntimeToken)
    {
        var resolved = ImportLocationResolver.BuildMap(["ship:The Wanderer", "settlement:Missing"], ["ship:9", ""]);

        await Assert.That(ImportLocationResolver.FromMap(fileToken, resolved)).IsEqualTo(expectedRuntimeToken);
    }
}
