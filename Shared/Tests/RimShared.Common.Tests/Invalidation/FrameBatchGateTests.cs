namespace RimShared.Common.Tests.Invalidation;

public class FrameBatchGateTests
{
    [Test]
    public async Task MultipleMapUpdatesCanEnterOnlyOneBatchPerFrame()
    {
        var gate = new FrameBatchGate();

        bool firstMap = gate.TryEnter(120);
        bool secondMap = gate.TryEnter(120);
        bool nextFrame = gate.TryEnter(121);

        await Assert.That(firstMap).IsTrue();
        await Assert.That(secondMap).IsFalse();
        await Assert.That(nextFrame).IsTrue();
    }

    [Test]
    public async Task ResetAllowsTheCurrentFrameToEnterAgain()
    {
        var gate = new FrameBatchGate();
        gate.TryEnter(42);

        gate.Reset();

        await Assert.That(gate.TryEnter(42)).IsTrue();
    }
}
