using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// The age-band selector's mapping and click transitions: bands 3-6, 7-9,
/// 10-12, 13-17, 18+ against the stored (minAge, maxAge) gates where 0 means
/// no gate on that end.
public class AgeBandsTests
{
    [Test]
    public async Task StoredGatesMapToBandSelectionsAndBack()
    {
        await Assert.That(AgeBands.SelectionFor(0, 0)).IsEqualTo((0, 4));
        await Assert.That(AgeBands.StoredFor(0, 4)).IsEqualTo((0, 0));

        await Assert.That(AgeBands.SelectionFor(7, 0)).IsEqualTo((1, 4));
        await Assert.That(AgeBands.StoredFor(1, 4)).IsEqualTo((7, 0));

        await Assert.That(AgeBands.SelectionFor(0, 12)).IsEqualTo((0, 2));
        await Assert.That(AgeBands.StoredFor(0, 2)).IsEqualTo((0, 12));

        await Assert.That(AgeBands.SelectionFor(13, 0)).IsEqualTo((3, 4));
        await Assert.That(AgeBands.SelectionFor(10, 17)).IsEqualTo((2, 3));
        await Assert.That(AgeBands.StoredFor(2, 3)).IsEqualTo((10, 17));

        // A leading selection that starts at the first band stores min 0:
        // nobody under 3 can work, so 0 and 3 are the same gate.
        await Assert.That(AgeBands.StoredFor(0, 0)).IsEqualTo((0, 6));

        // Hand-set values between band edges display as the containing bands.
        await Assert.That(AgeBands.SelectionFor(5, 0)).IsEqualTo((0, 4));
        await Assert.That(AgeBands.SelectionFor(15, 0)).IsEqualTo((3, 4));
        await Assert.That(AgeBands.SelectionFor(0, 8)).IsEqualTo((0, 1));
    }

    [Test]
    public async Task ClicksExtendTrimCollapseAndNeverEmptyTheSelection()
    {
        // Unselected band: extend the range to reach it (gap-free by
        // construction).
        await Assert.That(AgeBands.Click(2, 2, 4)).IsEqualTo((2, 4));
        await Assert.That(AgeBands.Click(2, 3, 0)).IsEqualTo((0, 3));

        // Selected end band: trim it off.
        await Assert.That(AgeBands.Click(0, 4, 0)).IsEqualTo((1, 4));
        await Assert.That(AgeBands.Click(0, 4, 4)).IsEqualTo((0, 3));

        // Selected interior band: collapse to just that band.
        await Assert.That(AgeBands.Click(0, 4, 2)).IsEqualTo((2, 2));
        await Assert.That(AgeBands.Click(1, 3, 2)).IsEqualTo((2, 2));

        // The only selected band: no-op, the selection can never empty.
        await Assert.That(AgeBands.Click(1, 1, 1)).IsEqualTo((1, 1));
    }

    [Test]
    public async Task BandsBelowTheEarliestUnlockAgeLackJobs()
    {
        // Doctor-shaped: the earliest work unlocks at 10, so even the oldest
        // colonist in 3-6 or 7-9 has nothing to do; 10-12, 13-17 and the
        // open 18+ band all have jobs.
        await Assert.That(AgeBands.BandLacksJobs(0, 10)).IsTrue();
        await Assert.That(AgeBands.BandLacksJobs(1, 10)).IsTrue();
        await Assert.That(AgeBands.BandLacksJobs(2, 10)).IsFalse();
        await Assert.That(AgeBands.BandLacksJobs(3, 10)).IsFalse();
        await Assert.That(AgeBands.BandLacksJobs(4, 10)).IsFalse();

        // Nothing age-gated: every band has jobs.
        await Assert.That(AgeBands.BandLacksJobs(0, 0)).IsFalse();
    }
}
