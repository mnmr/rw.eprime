using RimShared.Common;

namespace Implanner.Core.Tests;

/// Proves the RimSharedRoot compile-include wiring: shared sources are
/// compiled into Implanner.Core and behave observably. Behavioral coverage
/// of the shared types themselves lives in Shared\Tests.
public class SharedSourceWiringTests
{
    [Test]
    public async Task SharedGateCompiledIntoCoreFiresOncePerBoundary()
    {
        var gate = new FixedTickBoundaryGate(2500);

        await Assert.That(gate.Observe(0)).IsTrue();
        await Assert.That(gate.Observe(2499)).IsFalse();
        await Assert.That(gate.Observe(2500)).IsTrue();
        await Assert.That(gate.Observe(2500)).IsFalse();
    }
}
