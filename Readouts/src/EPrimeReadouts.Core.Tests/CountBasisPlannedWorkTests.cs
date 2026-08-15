using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Planned-work debt is subtracted by the same single resolution point that
/// applies the storage-only and hide-forbidden options, so every displayed
/// count agrees.
public class CountBasisPlannedWorkTests
{
    // 150 map-wide, 110 stored, 140 unforbidden, 100 stored-and-unforbidden.
    private static readonly SearchCount Steel = new(150, 110, 140, 100);

    [Test]
    public async Task NoDebtLeavesTheNarrowedCountAlone()
    {
        await Assert.That(CountBasis.Displayed(Steel,
            storageOnly: true, hideForbidden: false,
            debt: 0, allowNegative: false)).IsEqualTo(110);
    }

    [Test]
    public async Task DebtIsSubtractedFromTheNarrowedBasis()
    {
        await Assert.That(CountBasis.Displayed(Steel,
            storageOnly: true, hideForbidden: false,
            debt: 30, allowNegative: false)).IsEqualTo(80);
    }

    [Test]
    public async Task DebtAppliesToEveryBasisCombination()
    {
        await Assert.That(CountBasis.Displayed(Steel, false, false, 50, false))
            .IsEqualTo(100);
        await Assert.That(CountBasis.Displayed(Steel, true, false, 50, false))
            .IsEqualTo(60);
        await Assert.That(CountBasis.Displayed(Steel, false, true, 50, false))
            .IsEqualTo(90);
        await Assert.That(CountBasis.Displayed(Steel, true, true, 50, false))
            .IsEqualTo(50);
    }

    [Test]
    public async Task OverrunClampsToZeroByDefault()
    {
        await Assert.That(CountBasis.Displayed(Steel,
            storageOnly: true, hideForbidden: true,
            debt: 500, allowNegative: false)).IsEqualTo(0);
    }

    [Test]
    public async Task OverrunShowsAsNegativeWhenAllowed()
    {
        await Assert.That(CountBasis.Displayed(Steel,
            storageOnly: true, hideForbidden: true,
            debt: 130, allowNegative: true)).IsEqualTo(-30);
    }

    [Test]
    public async Task AllowNegativeDoesNotChangeANonOverrunCount()
    {
        await Assert.That(CountBasis.Displayed(Steel,
            storageOnly: false, hideForbidden: false,
            debt: 20, allowNegative: true)).IsEqualTo(130);
    }

    [Test]
    public async Task NegativeDebtIsIgnored()
    {
        // A malformed debt must never inflate a counter.
        await Assert.That(CountBasis.Displayed(Steel,
            storageOnly: false, hideForbidden: false,
            debt: -40, allowNegative: true)).IsEqualTo(150);
    }

    [Test]
    public async Task TheDebtFreeOverloadStillMatchesTheOriginalBehaviour()
    {
        await Assert.That(CountBasis.Displayed(Steel, true, true))
            .IsEqualTo(100);
    }
}
