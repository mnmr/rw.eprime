using QualityJobs.Core;

namespace QualityJobs.Core.Tests;

public class TooltipDisplayGateTests
{
    [Test]
    public async Task ContinuousHoverOpensOnceAndThenRemainsVisible()
    {
        var gate = new TooltipDisplayGate();

        await Assert.That(gate.Observe("quality-range", 10, 1f, 0.45f))
            .IsEqualTo(TooltipDisplayState.Pending);
        await Assert.That(gate.Observe("quality-range", 11, 1.44f, 0.45f))
            .IsEqualTo(TooltipDisplayState.Pending);
        await Assert.That(gate.Observe("quality-range", 12, 1.45f, 0.45f))
            .IsEqualTo(TooltipDisplayState.Opened);
        await Assert.That(gate.Observe("quality-range", 13, 1.46f, 0.45f))
            .IsEqualTo(TooltipDisplayState.Visible);
    }

    [Test]
    public async Task GapKeyChangeAndResetStartANewDelay()
    {
        var gate = new TooltipDisplayGate();
        gate.Observe("quality-range", 10, 1f, 0.45f);
        gate.Observe("quality-range", 11, 1.45f, 0.45f);

        await Assert.That(gate.Observe("quality-range", 13, 2f, 0.45f))
            .IsEqualTo(TooltipDisplayState.Pending);
        await Assert.That(gate.Observe("quality-table", 14, 3f, 0.45f))
            .IsEqualTo(TooltipDisplayState.Pending);

        gate.Reset();
        await Assert.That(gate.Observe("quality-table", 15, 4f, 0.45f))
            .IsEqualTo(TooltipDisplayState.Pending);
    }
}
