using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// Band geometry (0..21 axis, whole levels, min span 4): default adjacent
/// bands, edge/slide clamps, display-row packing and validation.
public class SkillProgressionMathTests
{
    [Test]
    public async Task DefaultBandsAreAdjacentAndValid()
    {
        var (mins, maxes) = SkillProgressionMath.DefaultBands(3);
        await Assert.That(mins).IsEquivalentTo(new[] { 0, 7, 14 });
        await Assert.That(maxes).IsEquivalentTo(new[] { 7, 14, 21 });
        await Assert.That(SkillProgressionMath.Validate(3, mins, maxes)).IsTrue();
    }

    [Test]
    public async Task EverySupportedCountYieldsValidAdjacentFullAxisDefaults()
    {
        for (int count = 1; count <= SkillProgressionMath.MaxInitialRoles; count++)
        {
            var (mins, maxes) = SkillProgressionMath.DefaultBands(count);
            await Assert.That(SkillProgressionMath.Validate(count, mins, maxes)).IsTrue();
            // Defaults tile the whole axis: start at 0, end at the top, and
            // each band starts exactly where the previous one ends.
            await Assert.That(mins[0]).IsEqualTo(0).Because($"count {count}: axis start");
            await Assert.That(maxes[count - 1])
                .IsEqualTo(SkillProgressionMath.MaxLevel).Because($"count {count}: axis end");
            for (int i = 1; i < count; i++)
                await Assert.That(mins[i]).IsEqualTo(maxes[i - 1])
                    .Because($"count {count}: band {i} not adjacent");
        }
    }

    [Test]
    public async Task EdgeClampKeepsSpanAndAxis()
    {
        // Band [4, 12]: min may reach 0..8, max may reach 8..21.
        await Assert.That(SkillProgressionMath.ClampEdge(4, 12, movingMin: true, desired: -3)).IsEqualTo(0);
        await Assert.That(SkillProgressionMath.ClampEdge(4, 12, movingMin: true, desired: 11)).IsEqualTo(8);
        await Assert.That(SkillProgressionMath.ClampEdge(4, 12, movingMin: false, desired: 5)).IsEqualTo(8);
        await Assert.That(SkillProgressionMath.ClampEdge(4, 12, movingMin: false, desired: 25)).IsEqualTo(21);
        await Assert.That(SkillProgressionMath.ClampEdge(4, 12, movingMin: false, desired: 15)).IsEqualTo(15);
    }

    [Test]
    public async Task SlideClampPreservesWidth()
    {
        // Band [4, 12] (width 8): min slides within 0..13.
        await Assert.That(SkillProgressionMath.ClampSlide(4, 12, desiredMin: -5)).IsEqualTo(0);
        await Assert.That(SkillProgressionMath.ClampSlide(4, 12, desiredMin: 20)).IsEqualTo(13);
        await Assert.That(SkillProgressionMath.ClampSlide(4, 12, desiredMin: 6)).IsEqualTo(6);
    }

    [Test]
    public async Task PackRowsSeparatesOverlapsOnly()
    {
        // crafting 0-14, tailor 2-21, smith 4-21, fabricator 7-21: all overlap -> 4 rows.
        var rows = SkillProgressionMath.PackRows(
            new List<(int min, int max)> { (0, 14), (2, 21), (4, 21), (7, 21) });
        await Assert.That(rows).IsEquivalentTo(new[] { 0, 1, 2, 3 });

        // Adjacent bands share row 0 (max exclusive: [0,7) and [7,14) do not overlap).
        var adjacent = SkillProgressionMath.PackRows(
            new List<(int min, int max)> { (0, 7), (7, 14), (14, 21) });
        await Assert.That(adjacent).IsEquivalentTo(new[] { 0, 0, 0 });

        // Mixed: (0,10) and (10,21) share a row; (5,15) needs its own.
        var mixed = SkillProgressionMath.PackRows(
            new List<(int min, int max)> { (0, 10), (5, 15), (10, 21) });
        await Assert.That(mixed).IsEquivalentTo(new[] { 0, 1, 0 });
    }

    [Test]
    public async Task PackRowsExpectsSortedInput()
    {
        // Caller sorts by (min, max); packing is greedy first-fit.
        var rows = SkillProgressionMath.PackRows(
            new List<(int min, int max)> { (0, 7), (0, 21), (7, 14) });
        await Assert.That(rows).IsEquivalentTo(new[] { 0, 1, 0 });
    }

    [Test]
    public async Task ValidationRejectsBadBands()
    {
        List<int> L(params int[] v) => v.ToList();
        await Assert.That(SkillProgressionMath.Validate(0, L(), L())).IsFalse();
        await Assert.That(SkillProgressionMath.Validate(2, L(0), L(7, 21))).IsFalse();   // count mismatch
        await Assert.That(SkillProgressionMath.Validate(1, L(-1), L(7))).IsFalse();      // below axis
        await Assert.That(SkillProgressionMath.Validate(1, L(10), L(22))).IsFalse();     // above axis
        await Assert.That(SkillProgressionMath.Validate(1, L(10), L(13))).IsFalse();     // span 3 < 4
        await Assert.That(SkillProgressionMath.Validate(1, L(17), L(21))).IsTrue();
        await Assert.That(SkillProgressionMath.Validate(2, L(0, 0), L(21, 21))).IsTrue(); // full overlap fine
    }

    [Test]
    public async Task SharedEdgeClampRespectsBothSpans()
    {
        // Left band starts at 4, right band ends at 21: edge roams 8..17.
        await Assert.That(SkillProgressionMath.ClampSharedEdge(4, 21, desired: 2)).IsEqualTo(8);
        await Assert.That(SkillProgressionMath.ClampSharedEdge(4, 21, desired: 25)).IsEqualTo(17);
        await Assert.That(SkillProgressionMath.ClampSharedEdge(4, 21, desired: 12)).IsEqualTo(12);
        // Minimal roaming room: both spans already at MinSpan.
        await Assert.That(SkillProgressionMath.ClampSharedEdge(0, 8, desired: 21)).IsEqualTo(4);
    }
}
