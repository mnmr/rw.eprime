using QualityJobs.Core;

namespace QualityJobs.Core.Tests;

public class FixedTickBoundaryGateTests
{
    [Test]
    public async Task FirstObservationAtABoundaryFires()
    {
        var gate = new FixedTickBoundaryGate(2500);

        await Assert.That(gate.Observe(2500)).IsTrue();
    }

    [Test]
    public async Task TicksBeforeTheNextBoundaryReuseTheCurrentGeneration()
    {
        var gate = new FixedTickBoundaryGate(2500);
        gate.Observe(2500);

        await Assert.That(gate.Observe(4999)).IsFalse();
    }

    [Test]
    public async Task TheConfiguredBoundaryFiresOnce()
    {
        var gate = new FixedTickBoundaryGate(2500);
        gate.Observe(2500);

        await Assert.That(gate.Observe(5000)).IsTrue();
        await Assert.That(gate.Observe(5000)).IsFalse();
    }

    [Test]
    public async Task PausedTicksDoNotRepeatARefresh()
    {
        var gate = new FixedTickBoundaryGate(2500);

        await Assert.That(gate.Observe(0)).IsTrue();
        await Assert.That(gate.Observe(0)).IsFalse();
    }

    [Test]
    public async Task LoadingAnEarlierTickStartsANewGeneration()
    {
        var gate = new FixedTickBoundaryGate(2500);
        gate.Observe(7500);

        await Assert.That(gate.Observe(100)).IsTrue();
        await Assert.That(gate.Observe(100)).IsFalse();
    }

    [Test]
    public async Task CrossingSeveralBoundariesStillFiresOnlyOncePerObservation()
    {
        var gate = new FixedTickBoundaryGate(2500);
        gate.Observe(0);

        await Assert.That(gate.Observe(10000)).IsTrue();
        await Assert.That(gate.Observe(10000)).IsFalse();
    }
}
