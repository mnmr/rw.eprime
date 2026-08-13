namespace WorkRoles.Core.Tests.Invalidation;

public class FixedTickBoundaryGateTests
{
    [Test]
    public async Task FirstObservationRunsAndSchedulesTheNextExactBoundary()
    {
        var gate = new FixedTickBoundaryGate(2500);

        bool first = gate.ShouldRun(2300);
        bool beforeBoundary = gate.ShouldRun(2499);
        bool atBoundary = gate.ShouldRun(2500);

        await Assert.That(first).IsTrue();
        await Assert.That(beforeBoundary).IsFalse();
        await Assert.That(atBoundary).IsTrue();
    }

    [Test]
    public async Task IndependentContextsAdvanceOnTheirOwnTicks()
    {
        var fastMap = new FixedTickBoundaryGate(2500);
        var slowMap = new FixedTickBoundaryGate(2500);
        fastMap.ShouldRun(100);
        slowMap.ShouldRun(100);

        await Assert.That(fastMap.ShouldRun(2500)).IsTrue();
        await Assert.That(slowMap.ShouldRun(2000)).IsFalse();
        await Assert.That(slowMap.ShouldRun(2500)).IsTrue();
    }

    [Test]
    public async Task RepeatedChecksWithinOneBoundaryRunOnlyOnce()
    {
        var gate = new FixedTickBoundaryGate(2500);

        await Assert.That(gate.ShouldRun(2500)).IsTrue();
        await Assert.That(gate.ShouldRun(2500)).IsFalse();
        await Assert.That(gate.ShouldRun(2501)).IsFalse();
        await Assert.That(gate.ShouldRun(5000)).IsTrue();
    }
}
