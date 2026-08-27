using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// What share of one material a deconstruct hands back. A building's default
/// fraction is the usual answer, but its def can force a material back in full
/// or withhold it entirely, and a zero default suppresses leavings outright.
public class ReturnedFractionTests
{
    [Test]
    public async Task AnOrdinaryMaterialReturnsTheBuildingsFraction()
    {
        await Assert.That(PlannedWorkMath.ReturnedFraction(
            0.5f, forced: false, blacklisted: false)).IsEqualTo(0.5f);
    }

    [Test]
    public async Task AForcedMaterialReturnsInFull()
    {
        await Assert.That(PlannedWorkMath.ReturnedFraction(
            0.5f, forced: true, blacklisted: false)).IsEqualTo(1f);
    }

    [Test]
    public async Task ABlacklistedMaterialReturnsNothing()
    {
        await Assert.That(PlannedWorkMath.ReturnedFraction(
            0.5f, forced: false, blacklisted: true)).IsEqualTo(0f);
    }

    [Test]
    public async Task ForcingBeatsBlacklisting()
    {
        // Vanilla tests the forced list first and skips the rest of the item's
        // handling, so a def listing a material in both returns it in full.
        await Assert.That(PlannedWorkMath.ReturnedFraction(
            0.5f, forced: true, blacklisted: true)).IsEqualTo(1f);
    }

    [Test]
    public async Task AZeroFractionSuppressesEveryLeaving()
    {
        // A building whose fraction is zero cannot leave resources at all, so
        // even a forced material stays gone.
        await Assert.That(PlannedWorkMath.ReturnedFraction(
            0f, forced: true, blacklisted: false)).IsEqualTo(0f);
        await Assert.That(PlannedWorkMath.ReturnedFraction(
            0f, forced: false, blacklisted: false)).IsEqualTo(0f);
    }

    [Test]
    public async Task ANegativeFractionIsTreatedAsZero()
    {
        await Assert.That(PlannedWorkMath.ReturnedFraction(
            -0.5f, forced: true, blacklisted: false)).IsEqualTo(0f);
    }

    [Test]
    public async Task AFullFractionIsPassedThrough()
    {
        await Assert.That(PlannedWorkMath.ReturnedFraction(
            1f, forced: false, blacklisted: false)).IsEqualTo(1f);
    }

    [Test]
    public async Task AnOverlargeFractionClampsToFull()
    {
        await Assert.That(PlannedWorkMath.ReturnedFraction(
            1.5f, forced: false, blacklisted: false)).IsEqualTo(1f);
    }

    [Test]
    public async Task BlacklistingStillWinsAgainstAFullFraction()
    {
        await Assert.That(PlannedWorkMath.ReturnedFraction(
            1f, forced: false, blacklisted: true)).IsEqualTo(0f);
    }

    [Test]
    public async Task AWithheldMaterialCostsItsFullRebuildEachAttempt()
    {
        // End to end: two attempts on a 100-cost material that never comes back
        // is a full extra 100 on top of the outstanding delivery.
        float returned = PlannedWorkMath.ReturnedFraction(
            0.5f, forced: false, blacklisted: true);

        await Assert.That(PlannedWorkMath.BuildableDebt(
            outstanding: 60, fullCost: 100,
            expectedAttempts: 2f, returnedFraction: returned)).IsEqualTo(160);
    }

    [Test]
    public async Task AForcedMaterialAddsNothingForRebuilds()
    {
        float returned = PlannedWorkMath.ReturnedFraction(
            0.5f, forced: true, blacklisted: false);

        await Assert.That(PlannedWorkMath.BuildableDebt(
            outstanding: 60, fullCost: 100,
            expectedAttempts: 5f, returnedFraction: returned)).IsEqualTo(60);
    }
}
