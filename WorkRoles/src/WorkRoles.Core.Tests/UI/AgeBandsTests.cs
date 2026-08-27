namespace WorkRoles.Core.Tests.UI;

/// The age-band selector's mapping and click transitions: bands 3-6, 7-9,
/// 10-12, 13-17, 18+ against the stored (minAge, maxAge) gates where 0 means
/// no gate on that end.
public class AgeBandsTests
{
    [Test]
    [Arguments(0, 0, 0, 4)]
    [Arguments(7, 0, 1, 4)]
    [Arguments(0, 12, 0, 2)]
    [Arguments(13, 0, 3, 4)]
    [Arguments(10, 17, 2, 3)]
    [Arguments(5, 0, 0, 4)]
    [Arguments(15, 0, 3, 4)]
    [Arguments(0, 8, 0, 1)]
    public async Task StoredGatesMapToExpectedBandSelection(int minAge, int maxAge, int expectedFirstBand, int expectedLastBand)
    {
        await Assert.That(AgeBands.SelectionFor(minAge, maxAge)).IsEqualTo((expectedFirstBand, expectedLastBand));
    }

    [Test]
    [Arguments(0, 4, 0, 0)]
    [Arguments(1, 4, 7, 0)]
    [Arguments(0, 2, 0, 12)]
    [Arguments(2, 3, 10, 17)]
    [Arguments(0, 0, 0, 6)]
    public async Task BandSelectionMapsToExpectedStoredGates(int firstBand, int lastBand, int expectedMinAge, int expectedMaxAge)
    {
        await Assert.That(AgeBands.StoredFor(firstBand, lastBand)).IsEqualTo((expectedMinAge, expectedMaxAge));
    }

    [Test]
    [Arguments(2, 2, 4, 2, 4)]
    [Arguments(2, 3, 0, 0, 3)]
    [Arguments(0, 4, 0, 1, 4)]
    [Arguments(0, 4, 4, 0, 3)]
    [Arguments(0, 4, 2, 2, 2)]
    [Arguments(1, 1, 1, 1, 1)]
    public async Task ClickingBandProducesExpectedContiguousSelection(int firstBand, int lastBand, int clickedBand, int expectedFirstBand, int expectedLastBand)
    {
        await Assert.That(AgeBands.Click(firstBand, lastBand, clickedBand)).IsEqualTo((expectedFirstBand, expectedLastBand));
    }

    [Test]
    [Arguments(1, 10, true)]
    [Arguments(2, 10, false)]
    [Arguments(4, 10, false)]
    [Arguments(0, 0, false)]
    public async Task BandReportsWhetherItPrecedesTheEarliestUnlockAge(int band, int earliestUnlockAge, bool expected)
    {
        await Assert.That(AgeBands.BandLacksJobs(band, earliestUnlockAge)).IsEqualTo(expected);
    }
}
