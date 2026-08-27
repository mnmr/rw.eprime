namespace RimShared.Common.Tests;

public class RevisionPairGateTests
{
    [Test]
    public async Task InitialObservationRefreshesAndAnUnchangedPairDoesNot()
    {
        var gate = new RevisionPairGate();

        await Assert.That(gate.ShouldRefresh(4, 9)).IsTrue();
        await Assert.That(gate.ShouldRefresh(4, 9)).IsFalse();
    }

    [Test]
    public async Task FirstRevisionChangingRequiresExactlyOneRefresh()
    {
        var gate = new RevisionPairGate();
        gate.ShouldRefresh(4, 9);

        await Assert.That(gate.ShouldRefresh(5, 9)).IsTrue();
        await Assert.That(gate.ShouldRefresh(5, 9)).IsFalse();
    }

    [Test]
    public async Task SecondRevisionChangingRequiresExactlyOneRefresh()
    {
        var gate = new RevisionPairGate();
        gate.ShouldRefresh(4, 9);

        await Assert.That(gate.ShouldRefresh(4, 10)).IsTrue();
        await Assert.That(gate.ShouldRefresh(4, 10)).IsFalse();
    }
}
