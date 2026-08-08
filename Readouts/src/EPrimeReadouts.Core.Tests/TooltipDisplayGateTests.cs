using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class TooltipDisplayGateTests
{
    [Test]
    public async Task ContinuousHoverOpensOnceAndThenRemainsVisible()
    {
        var gate = new TooltipDisplayGate();

        await Assert.That(gate.Observe("meals", 10, 1f, 0.45f))
            .IsEqualTo(TooltipDisplayState.Pending);
        await Assert.That(gate.Observe("meals", 11, 1.44f, 0.45f))
            .IsEqualTo(TooltipDisplayState.Pending);
        await Assert.That(gate.Observe("meals", 12, 1.45f, 0.45f))
            .IsEqualTo(TooltipDisplayState.Opened);
        await Assert.That(gate.Observe("meals", 13, 1.46f, 0.45f))
            .IsEqualTo(TooltipDisplayState.Visible);
    }

    [Test]
    public async Task GapKeyChangeAndResetStartANewDelay()
    {
        var gate = new TooltipDisplayGate();
        gate.Observe("meals", 10, 1f, 0.45f);
        gate.Observe("meals", 11, 1.45f, 0.45f);

        await Assert.That(gate.Observe("meals", 13, 2f, 0.45f))
            .IsEqualTo(TooltipDisplayState.Pending);
        await Assert.That(gate.Observe("meats", 14, 3f, 0.45f))
            .IsEqualTo(TooltipDisplayState.Pending);

        gate.Reset();
        await Assert.That(gate.Observe("meats", 15, 4f, 0.45f))
            .IsEqualTo(TooltipDisplayState.Pending);
    }
}
