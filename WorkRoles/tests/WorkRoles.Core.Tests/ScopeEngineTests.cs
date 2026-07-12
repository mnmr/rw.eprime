using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class ScopeEngineTests
{
    private static LocationInfo Loc(string id, string label, bool ship = false) =>
        new LocationInfo { Id = id, Label = label, IsShip = ship };

    [Test]
    public async Task OptionsAreAllCurrentThenShipsThenSettlementsAlphabetically()
    {
        var options = ScopeEngine.BuildOptions(new[]
        {
            Loc("1", "Boarwood"),
            Loc("2", "The Wanderer", ship: true),
            Loc("3", "Attica"),
        });
        await Assert.That(options.Select(o => o.Kind)).IsEquivalentTo(new[]
        {
            ScopeKind.All, ScopeKind.CurrentLocation,
            ScopeKind.Location, ScopeKind.Location, ScopeKind.Location,
        });
        await Assert.That(options.Skip(2).Select(o => o.Label))
            .IsEquivalentTo(new[] { "The Wanderer", "Attica", "Boarwood" });
    }

    [Test]
    public async Task NoNamedLocationsYieldsJustAllAndCurrent()
    {
        var options = ScopeEngine.BuildOptions(new LocationInfo[0]);
        await Assert.That(options.Select(o => o.Kind))
            .IsEquivalentTo(new[] { ScopeKind.All, ScopeKind.CurrentLocation });
    }

    [Test]
    public async Task MatchesRespectsKind_AndCaravansOnlyAppearUnderAll()
    {
        var all = new ScopeOption { Kind = ScopeKind.All };
        var current = new ScopeOption { Kind = ScopeKind.CurrentLocation };
        var named = new ScopeOption { Kind = ScopeKind.Location, LocationId = "7" };

        await Assert.That(ScopeEngine.Matches(all, null, "1")).IsTrue();       // caravan pawn
        await Assert.That(ScopeEngine.Matches(current, null, "1")).IsFalse();
        await Assert.That(ScopeEngine.Matches(named, null, "1")).IsFalse();

        await Assert.That(ScopeEngine.Matches(current, "1", "1")).IsTrue();
        await Assert.That(ScopeEngine.Matches(current, "2", "1")).IsFalse();
        await Assert.That(ScopeEngine.Matches(named, "7", "1")).IsTrue();
        await Assert.That(ScopeEngine.Matches(named, "8", "1")).IsFalse();
    }

    [Test]
    public async Task SpansMultipleLocations_TrueForMixedMapsOrMapsPlusCaravans()
    {
        await Assert.That(ScopeEngine.SpansMultipleLocations(new[] { "1", "1", "1" })).IsFalse();
        await Assert.That(ScopeEngine.SpansMultipleLocations(new[] { "1", "2" })).IsTrue();
        await Assert.That(ScopeEngine.SpansMultipleLocations(new[] { "1", null })).IsTrue();
        await Assert.That(ScopeEngine.SpansMultipleLocations(new string[] { null, null })).IsFalse();
        await Assert.That(ScopeEngine.SpansMultipleLocations(new string[0])).IsFalse();
    }

    [Test]
    public async Task RevalidateFallsBackToCurrentWhenNamedLocationDisappears()
    {
        var options = ScopeEngine.BuildOptions(new[] { Loc("1", "Boarwood") });
        var stale = new ScopeOption { Kind = ScopeKind.Location, LocationId = "gone" };
        var alive = new ScopeOption { Kind = ScopeKind.Location, LocationId = "1" };

        await Assert.That(ScopeEngine.Revalidate(stale, options).Kind)
            .IsEqualTo(ScopeKind.CurrentLocation);
        await Assert.That(ScopeEngine.Revalidate(alive, options).LocationId).IsEqualTo("1");
        await Assert.That(ScopeEngine.Revalidate(null, options).Kind)
            .IsEqualTo(ScopeKind.CurrentLocation);
    }
}
