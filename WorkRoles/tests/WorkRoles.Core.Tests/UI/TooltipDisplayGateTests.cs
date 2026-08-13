namespace WorkRoles.Core.Tests.UI;

public class TooltipDisplayGateTests
{
    [Test]
    public async Task ContinuousHoverOpensOnceAndThenRemainsVisible()
    {
        var gate = new TooltipDisplayGate();

        await Assert.That(gate.Observe("role:builder", 10, 1f, 0.45f)).IsEqualTo(TooltipDisplayState.Pending);
        await Assert.That(gate.Observe("role:builder", 11, 1.44f, 0.45f)).IsEqualTo(TooltipDisplayState.Pending);
        await Assert.That(gate.Observe("role:builder", 12, 1.45f, 0.45f)).IsEqualTo(TooltipDisplayState.Opened);
        await Assert.That(gate.Observe("role:builder", 13, 1.46f, 0.45f)).IsEqualTo(TooltipDisplayState.Visible);
    }

    [Test]
    public async Task HoverGapStartsANewDelay()
    {
        var gate = new TooltipDisplayGate();
        gate.Observe("role:builder", 10, 1f, 0.45f);
        gate.Observe("role:builder", 11, 1.45f, 0.45f);

        await Assert.That(gate.Observe("role:builder", 13, 2f, 0.45f)).IsEqualTo(TooltipDisplayState.Pending);
    }

    [Test]
    public async Task HoverKeyChangeStartsANewDelay()
    {
        var gate = new TooltipDisplayGate();
        gate.Observe("role:builder", 10, 1f, 0.45f);
        gate.Observe("role:builder", 11, 1.45f, 0.45f);

        await Assert.That(gate.Observe("role:crafter", 14, 3f, 0.45f)).IsEqualTo(TooltipDisplayState.Pending);
    }

    [Test]
    public async Task ResetStartsANewDelay()
    {
        var gate = new TooltipDisplayGate();
        gate.Observe("role:crafter", 14, 3f, 0.45f);

        gate.Reset();
        await Assert.That(gate.Observe("role:crafter", 15, 4f, 0.45f)).IsEqualTo(TooltipDisplayState.Pending);
    }

    [Test]
    public async Task SuppressionPreventsPresentationWhileHoverContinues()
    {
        var gate = new TooltipDisplayGate();
        gate.Observe("role:builder", 10, 1f, 0.45f);
        gate.Observe("role:builder", 11, 1.45f, 0.45f);

        gate.SetSuppressed(true);

        await Assert.That(gate.Observe("role:builder", 12, 1.46f, 0.45f)).IsEqualTo(TooltipDisplayState.Suppressed);
    }

    [Test]
    public async Task ReleasingSuppressionRestartsTheHoverDelay()
    {
        var gate = new TooltipDisplayGate();
        gate.Observe("role:builder", 10, 1f, 0.45f);
        gate.Observe("role:builder", 11, 1.45f, 0.45f);
        gate.SetSuppressed(true);

        gate.SetSuppressed(false);

        await Assert.That(gate.Observe("role:builder", 12, 2f, 0.45f)).IsEqualTo(TooltipDisplayState.Pending);
        await Assert.That(gate.Observe("role:builder", 13, 2.45f, 0.45f)).IsEqualTo(TooltipDisplayState.Opened);
    }

    [Test]
    public async Task ResetClearsSuppressionForTheNextWindowSession()
    {
        var gate = new TooltipDisplayGate();
        gate.SetSuppressed(true);

        gate.Reset();

        await Assert.That(gate.Observe("role:builder", 10, 1f, 0.45f)).IsEqualTo(TooltipDisplayState.Pending);
    }
}
