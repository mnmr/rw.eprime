using RimShared.Common;

namespace RimShared.Common.Tests;

public class TooltipPlacementTests
{
    [Test]
    public async Task OverlappingPreferredPositionMovesBelowExcludedControl()
    {
        bool placed = TooltipPlacement.TryPlace(
            10f, 10f, 50f, 30f, 500f, 500f,
            true, 20f, 20f, 80f, 30f, out float x, out float y);

        await Assert.That(placed).IsTrue();
        await Assert.That(x).IsEqualTo(26f);
        await Assert.That(y).IsEqualTo(54f);
    }

    [Test]
    public async Task PreferredPositionIsKeptWhenItDoesNotOverlap()
    {
        bool placed = TooltipPlacement.TryPlace(
            10f, 10f, 50f, 30f, 500f, 500f,
            false, 0f, 0f, 0f, 0f, out float x, out float y);

        await Assert.That(placed).IsTrue();
        await Assert.That(x).IsEqualTo(26f);
        await Assert.That(y).IsEqualTo(24f);
    }

    [Test]
    public async Task MovesAboveWhenThereIsNoRoomBelow()
    {
        bool placed = TooltipPlacement.TryPlace(
            30f, 45f, 50f, 40f, 200f, 100f,
            true, 20f, 50f, 80f, 30f, out float x, out float y);

        await Assert.That(placed).IsTrue();
        await Assert.That(x).IsEqualTo(46f);
        await Assert.That(y).IsEqualTo(6f);
    }

    [Test]
    public async Task MovesRightWhenNeitherVerticalPositionFits()
    {
        bool placed = TooltipPlacement.TryPlace(
            25f, 25f, 50f, 80f, 300f, 100f,
            true, 20f, 20f, 60f, 60f, out float x, out float y);

        await Assert.That(placed).IsTrue();
        await Assert.That(x).IsEqualTo(84f);
        await Assert.That(y).IsEqualTo(6f);
    }

    [Test]
    public async Task MovesLeftWhenOnlyTheLeftPositionFits()
    {
        bool placed = TooltipPlacement.TryPlace(
            110f, 25f, 70f, 80f, 200f, 100f,
            true, 100f, 20f, 80f, 60f, out float x, out float y);

        await Assert.That(placed).IsTrue();
        await Assert.That(x).IsEqualTo(26f);
        await Assert.That(y).IsEqualTo(6f);
    }

    [Test]
    public async Task ReturnsFalseWhenNoNonOverlappingPositionFits()
    {
        bool placed = TooltipPlacement.TryPlace(
            10f, 10f, 90f, 90f, 100f, 100f,
            true, 5f, 5f, 90f, 90f, out _, out _);

        await Assert.That(placed).IsFalse();
    }
}
