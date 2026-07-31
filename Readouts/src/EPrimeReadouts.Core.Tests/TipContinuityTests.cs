using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class TipContinuityTests
{
    [Test]
    public async Task FirstInvocationStartsANewDisplay()
    {
        await Assert.That(TipContinuity.IsBroken(TipContinuity.NoFrame, 0)).IsTrue();
        await Assert.That(TipContinuity.IsBroken(TipContinuity.NoFrame, 12345)).IsTrue();
    }

    [Test]
    public async Task SameAndAdjacentFramesContinueTheDisplay()
    {
        // The getter can run more than once inside a single displayed frame.
        await Assert.That(TipContinuity.IsBroken(100, 100)).IsFalse();
        await Assert.That(TipContinuity.IsBroken(100, 101)).IsFalse();
    }

    [Test]
    public async Task AGapOfMoreThanOneFrameMeansTheTipClosed()
    {
        await Assert.That(TipContinuity.IsBroken(100, 102)).IsTrue();
        await Assert.That(TipContinuity.IsBroken(100, 5000)).IsTrue();
    }
}
