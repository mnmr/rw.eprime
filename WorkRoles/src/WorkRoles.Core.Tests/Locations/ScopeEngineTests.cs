namespace WorkRoles.Core.Tests.Locations;

public class ScopeEngineTests
{
    private static LocationInfo Loc(string id, string label, bool ship = false) => new LocationInfo(id, label, ship);

    [Test]
    public async Task LocationInfoPublishesNoPublicMutableFields()
    {
        Type type = typeof(LocationInfo);

        await Assert.That(type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)).IsEmpty();
    }

    [Test]
    public async Task OptionsAreAllCurrentThenShipsThenSettlementsAlphabetically()
    {
        IReadOnlyList<ScopeOption> options = ScopeEngine.BuildOptions([Loc("1", "Boarwood"), Loc("2", "The Wanderer", ship: true), Loc("3", "Attica")]);
        string actualKindOrder = string.Join(",", options.Select(option => option.Kind));
        string actualLabelOrder = string.Join(",", options.Skip(2).Select(option => option.Label));

        await Assert.That(actualKindOrder).IsEqualTo("All,CurrentLocation,Location,Location,Location");
        await Assert.That(actualLabelOrder).IsEqualTo("The Wanderer,Attica,Boarwood");
    }

    [Test]
    public async Task NoNamedLocationsYieldsJustAllAndCurrent()
    {
        IReadOnlyList<ScopeOption> options = ScopeEngine.BuildOptions([]);
        await Assert.That(options.Select(o => o.Kind)).IsEquivalentTo([ScopeKind.All, ScopeKind.CurrentLocation]);
    }

    [Test]
    public async Task InactiveCatalogLocationsDoNotBecomePawnScopeOptions()
    {
        IReadOnlyList<ScopeOption> options = ScopeEngine.BuildOptions([
            new LocationInfo("4", "Rimosa", isShip: false),
            new LocationInfo("GravEngine42", "The Wanderer", isShip: true, isActive: false),
        ]);

        await Assert.That(options.Skip(2).Select(option => option.Label
            ?? throw new InvalidOperationException("Named options require labels."))).IsEquivalentTo(["Rimosa"]);
    }

    [Test]
    [Arguments(ScopeKind.All, null, null, "1", true)]
    [Arguments(ScopeKind.CurrentLocation, null, null, "1", false)]
    [Arguments(ScopeKind.Location, "7", null, "1", false)]
    [Arguments(ScopeKind.CurrentLocation, null, "1", "1", true)]
    [Arguments(ScopeKind.CurrentLocation, null, "2", "1", false)]
    [Arguments(ScopeKind.Location, "7", "7", "1", true)]
    [Arguments(ScopeKind.Location, "7", "8", "1", false)]
    public async Task MatchesRespectsScopeKind(ScopeKind kind, string? selectedLocationId, string? pawnLocationId, string currentLocationId, bool expected)
    {
        var option = new ScopeOption { Kind = kind, LocationId = selectedLocationId };

        await Assert.That(ScopeEngine.Matches(option, pawnLocationId, currentLocationId)).IsEqualTo(expected);
    }

    [Test]
    public async Task RepeatedMapDoesNotSpanMultipleLocations()
    {
        await Assert.That(ScopeEngine.SpansMultipleLocations(["1", "1", "1"])).IsFalse();
    }

    [Test]
    public async Task DifferentMapsSpanMultipleLocations()
    {
        await Assert.That(ScopeEngine.SpansMultipleLocations(["1", "2"])).IsTrue();
    }

    [Test]
    public async Task MapAndCaravanSpanMultipleLocations()
    {
        await Assert.That(ScopeEngine.SpansMultipleLocations(["1", null!])).IsTrue();
    }

    [Test]
    public async Task CaravansDoNotSpanMultipleLocations()
    {
        await Assert.That(ScopeEngine.SpansMultipleLocations([null!, null!])).IsFalse();
    }

    [Test]
    public async Task EmptyPawnSetDoesNotSpanMultipleLocations()
    {
        await Assert.That(ScopeEngine.SpansMultipleLocations([])).IsFalse();
    }

    [Test]
    public async Task RevalidateFallsBackToCurrentWhenNamedLocationDisappears()
    {
        IReadOnlyList<ScopeOption> options = ScopeEngine.BuildOptions([Loc("1", "Boarwood")]);
        var stale = new ScopeOption { Kind = ScopeKind.Location, LocationId = "gone" };

        await Assert.That(ScopeEngine.Revalidate(stale, options).Kind).IsEqualTo(ScopeKind.CurrentLocation);
    }

    [Test]
    public async Task RevalidateKeepsNamedLocationThatStillExists()
    {
        IReadOnlyList<ScopeOption> options = ScopeEngine.BuildOptions([Loc("1", "Boarwood")]);
        var alive = new ScopeOption { Kind = ScopeKind.Location, LocationId = "1" };

        await Assert.That(ScopeEngine.Revalidate(alive, options).LocationId).IsEqualTo("1");
    }

    [Test]
    public async Task RevalidateFallsBackToCurrentForNullSelection()
    {
        IReadOnlyList<ScopeOption> options = ScopeEngine.BuildOptions([Loc("1", "Boarwood")]);

        await Assert.That(ScopeEngine.Revalidate(null!, options).Kind).IsEqualTo(ScopeKind.CurrentLocation);
    }
}
