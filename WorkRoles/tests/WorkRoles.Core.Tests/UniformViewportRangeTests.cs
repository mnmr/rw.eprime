using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class UniformViewportRangeTests
{
    [Test]
    public async Task ZeroItemsProduceAnEmptyRangeEvenWithOverscan()
    {
        var range = UniformViewportRange.Calculate(
            itemCount: 0,
            itemExtent: 26f,
            contentStart: 170f,
            viewportStart: 200f,
            viewportExtent: 300f,
            overscan: 4);

        await Assert.That(range.Start).IsEqualTo(0);
        await Assert.That(range.EndExclusive).IsEqualTo(0);
        await Assert.That(range.IsEmpty).IsTrue();
    }

    [Test]
    public async Task ExactViewportBoundariesUseAHalfOpenRange()
    {
        var range = UniformViewportRange.Calculate(
            itemCount: 10,
            itemExtent: 20f,
            contentStart: 100f,
            viewportStart: 120f,
            viewportExtent: 40f);

        await Assert.That(range.Start).IsEqualTo(1);
        await Assert.That(range.EndExclusive).IsEqualTo(3);
        await Assert.That(range.Count).IsEqualTo(2);
    }

    [Test]
    public async Task PartialItemsAtBothEdgesAreIncluded()
    {
        var range = UniformViewportRange.Calculate(
            itemCount: 10,
            itemExtent: 20f,
            contentStart: 100f,
            viewportStart: 119f,
            viewportExtent: 42f);

        await Assert.That(range.Start).IsEqualTo(0);
        await Assert.That(range.EndExclusive).IsEqualTo(4);
    }

    [Test]
    public async Task ScrolledViewportUsesFloorForStartAndCeilingForEnd()
    {
        var range = UniformViewportRange.Calculate(
            itemCount: 100,
            itemExtent: 10f,
            contentStart: 0f,
            viewportStart: 35f,
            viewportExtent: 20f);

        await Assert.That(range.Start).IsEqualTo(3);
        await Assert.That(range.EndExclusive).IsEqualTo(6);
    }

    [Test]
    [Arguments(-50f, 70f, 0, 2)]
    [Arguments(95f, 20f, 9, 10)]
    [Arguments(120f, 20f, 10, 10)]
    [Arguments(-40f, 20f, 0, 0)]
    public async Task RangeIsClampedToAvailableItems(
        float viewportStart,
        float viewportExtent,
        int expectedStart,
        int expectedEndExclusive)
    {
        var range = UniformViewportRange.Calculate(
            itemCount: 10,
            itemExtent: 10f,
            contentStart: 0f,
            viewportStart: viewportStart,
            viewportExtent: viewportExtent);

        await Assert.That(range.Start).IsEqualTo(expectedStart);
        await Assert.That(range.EndExclusive).IsEqualTo(expectedEndExclusive);
    }

    [Test]
    [Arguments(0, 2, 0, 4)]
    [Arguments(3, 2, 1, 7)]
    [Arguments(8, 2, 6, 10)]
    public async Task OverscanExpandsThenClampsTheHalfOpenRange(
        int firstVisibleItem,
        int overscan,
        int expectedStart,
        int expectedEndExclusive)
    {
        var range = UniformViewportRange.Calculate(
            itemCount: 10,
            itemExtent: 10f,
            contentStart: 0f,
            viewportStart: firstVisibleItem * 10f,
            viewportExtent: 20f,
            overscan: overscan);

        await Assert.That(range.Start).IsEqualTo(expectedStart);
        await Assert.That(range.EndExclusive).IsEqualTo(expectedEndExclusive);
    }

    [Test]
    public async Task ExpandingHeaderViewportByRunOutRetainsTrailingColumn()
    {
        const float headerRunOut = 15f;
        var range = UniformViewportRange.Calculate(
            itemCount: 10,
            itemExtent: 10f,
            contentStart: 0f,
            viewportStart: 105f - headerRunOut,
            viewportExtent: 20f + headerRunOut);

        await Assert.That(range.Start).IsEqualTo(9);
        await Assert.That(range.EndExclusive).IsEqualTo(10);
    }

    [Test]
    public async Task InvalidExtentsAndOverscanAreRejected()
    {
        await Assert.That(() => UniformViewportRange.Calculate(1, 0f, 0f, 0f, 1f))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => UniformViewportRange.Calculate(1, 1f, 0f, 0f, -1f))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => UniformViewportRange.Calculate(1, 1f, 0f, 0f, 1f, -1))
            .Throws<ArgumentOutOfRangeException>();
    }
}
