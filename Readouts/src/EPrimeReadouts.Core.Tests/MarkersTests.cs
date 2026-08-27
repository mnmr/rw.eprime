using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class MarkersTests
{
    [Test]
    public async Task StateAtReturnsOneTierWithoutAnIntermediateBuffer()
    {
        await Assert.That(Markers.StateAt(3, 2, 0))
            .IsEqualTo(TriangleState.Lit);
        await Assert.That(Markers.StateAt(3, 2, 1))
            .IsEqualTo(TriangleState.Lit);
        await Assert.That(Markers.StateAt(3, 2, 2))
            .IsEqualTo(TriangleState.Dim);
        await Assert.That(Markers.StateAt(3, 2, 3))
            .IsEqualTo(TriangleState.Absent);
    }

    private static TriangleState[] Compute(int tierCount, int depth)
    {
        var states = new TriangleState[TierOps.MaxTiers];
        Markers.Compute(tierCount, depth, states);
        return states;
    }

    [Test]
    public async Task UnsetDepthDefaultsToAllTiersVisible()
    {
        await Assert.That(Markers.ClampDepth(2, 0)).IsEqualTo(2);
        await Assert.That(Markers.ClampDepth(3, 99)).IsEqualTo(3);
    }

    [Test]
    public async Task DepthCyclesOneToTierCountThenWraps()
    {
        await Assert.That(Markers.NextDepth(3, 3)).IsEqualTo(1);
        await Assert.That(Markers.NextDepth(3, 1)).IsEqualTo(2);
        await Assert.That(Markers.NextDepth(1, 1)).IsEqualTo(1);
    }

    [Test]
    public async Task TwoTierGroupAtDepthOneIsLitDimAbsent()
    {
        var states = Compute(2, 1);
        await Assert.That(states[0]).IsEqualTo(TriangleState.Lit);
        await Assert.That(states[1]).IsEqualTo(TriangleState.Dim);
        await Assert.That(states[2]).IsEqualTo(TriangleState.Absent);
    }

    [Test]
    public async Task ThreeTierGroupAtFullDepthIsAllLit()
    {
        var states = Compute(3, 3);
        await Assert.That(states[0]).IsEqualTo(TriangleState.Lit);
        await Assert.That(states[1]).IsEqualTo(TriangleState.Lit);
        await Assert.That(states[2]).IsEqualTo(TriangleState.Lit);
    }

    [Test]
    public async Task ZeroTierGroupOverwritesReusedBufferToAbsent()
    {
        var states = new TriangleState[TierOps.MaxTiers];
        states[0] = TriangleState.Lit; // simulate buffer reuse
        Markers.Compute(0, 0, states);
        await Assert.That(states[0]).IsEqualTo(TriangleState.Absent);
        await Assert.That(states[2]).IsEqualTo(TriangleState.Absent);
    }
}
