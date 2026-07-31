using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class TimezoneCrossingPolicyTests
{
    [Test]
    public async Task CaravanCrossingInvalidatesTravelerPawns()
    {
        var response = TimezoneCrossingPolicy.Respond(
            anyTimeRuledRole: true, previousTimeZone: 2, newTimeZone: 3,
            isTraveler: true, hasSpawnedMap: false);

        await Assert.That(response)
            .IsEqualTo(TimezoneCrossingResponse.InvalidateTravelerPawns);
    }

    [Test]
    public async Task MapCrossingInvalidatesTimeRuledHolders()
    {
        var response = TimezoneCrossingPolicy.Respond(
            anyTimeRuledRole: true, previousTimeZone: 2, newTimeZone: 3,
            isTraveler: false, hasSpawnedMap: true);

        await Assert.That(response)
            .IsEqualTo(TimezoneCrossingResponse.InvalidateMapTimeRuled);
    }

    [Test]
    public async Task SameTimezoneMoveIsIgnored()
    {
        var caravan = TimezoneCrossingPolicy.Respond(
            anyTimeRuledRole: true, previousTimeZone: 5, newTimeZone: 5,
            isTraveler: true, hasSpawnedMap: false);
        var map = TimezoneCrossingPolicy.Respond(
            anyTimeRuledRole: true, previousTimeZone: 5, newTimeZone: 5,
            isTraveler: false, hasSpawnedMap: true);

        await Assert.That(caravan).IsEqualTo(TimezoneCrossingResponse.None);
        await Assert.That(map).IsEqualTo(TimezoneCrossingResponse.None);
    }

    [Test]
    public async Task NoTimedRolesIgnoresCrossing()
    {
        var response = TimezoneCrossingPolicy.Respond(
            anyTimeRuledRole: false, previousTimeZone: 2, newTimeZone: 3,
            isTraveler: false, hasSpawnedMap: true);

        await Assert.That(response).IsEqualTo(TimezoneCrossingResponse.None);
    }

    [Test]
    public async Task ObjectWithoutPawnContextIsIgnored()
    {
        var response = TimezoneCrossingPolicy.Respond(
            anyTimeRuledRole: true, previousTimeZone: 2, newTimeZone: 3,
            isTraveler: false, hasSpawnedMap: false);

        await Assert.That(response).IsEqualTo(TimezoneCrossingResponse.None);
    }
}
