namespace WorkRoles.Core.Tests.UI;

/// Band geometry (0..21 axis, whole levels, min span 4): default adjacent
/// bands, edge/slide clamps, display-row packing and validation.
public class SkillProgressionMathTests
{
    [Test]
    public async Task DefaultBandsAreAdjacentAndValid()
    {
        var (mins, maxes) = SkillProgressionMath.DefaultBands(3);
        await Assert.That(mins).IsEquivalentTo([0, 7, 14]);
        await Assert.That(maxes).IsEquivalentTo([7, 14, 21]);
        await Assert.That(SkillProgressionMath.Validate(3, mins, maxes)).IsTrue();
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(5)]
    public async Task SupportedCountYieldsValidAdjacentFullAxisDefaults(int count)
    {
        var (mins, maxes) = SkillProgressionMath.DefaultBands(count);
        bool bandsAreAdjacent = mins.Skip(1).SequenceEqual(maxes.Take(maxes.Count - 1));

        await Assert.That(SkillProgressionMath.Validate(count, mins, maxes)).IsTrue();
        await Assert.That(mins[0]).IsEqualTo(0);
        await Assert.That(maxes[^1]).IsEqualTo(SkillProgressionMath.MaxLevel);
        await Assert.That(bandsAreAdjacent).IsTrue();
    }

    [Test]
    [Arguments(true, -3, 0)]
    [Arguments(true, 11, 8)]
    [Arguments(false, 5, 8)]
    [Arguments(false, 25, 21)]
    [Arguments(false, 15, 15)]
    public async Task EdgeClampKeepsSpanAndAxis(bool movingMin, int desired, int expected)
    {
        // Band [4, 12]: min may reach 0..8, max may reach 8..21.
        int result = SkillProgressionMath.ClampEdge(4, 12, movingMin, desired);

        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments(-5, 0)]
    [Arguments(20, 13)]
    [Arguments(6, 6)]
    public async Task SlideClampPreservesWidth(int desiredMinimum, int expectedMinimum)
    {
        // Band [4, 12] (width 8): min slides within 0..13.
        int result = SkillProgressionMath.ClampSlide(4, 12, desiredMinimum);

        await Assert.That(result).IsEqualTo(expectedMinimum);
    }

    [Test]
    public async Task PackRowsPlacesEveryOverlappingBandOnItsOwnRow()
    {
        // crafting 0-14, tailor 2-21, smith 4-21, fabricator 7-21: all overlap -> 4 rows.
        IReadOnlyList<int> rows = SkillProgressionMath.PackRows([(0, 14), (2, 21), (4, 21), (7, 21)]);

        await Assert.That(rows).IsEquivalentTo([0, 1, 2, 3]);
    }

    [Test]
    public async Task PackRowsSharesOneRowBetweenAdjacentBands()
    {
        // Adjacent bands share row 0 (max exclusive: [0,7) and [7,14) do not overlap).
        IReadOnlyList<int> rows = SkillProgressionMath.PackRows([(0, 7), (7, 14), (14, 21)]);

        await Assert.That(rows).IsEquivalentTo([0, 0, 0]);
    }

    [Test]
    public async Task PackRowsReusesTheFirstRowAfterAnOverlapEnds()
    {
        // Mixed: (0,10) and (10,21) share a row; (5,15) needs its own.
        IReadOnlyList<int> rows = SkillProgressionMath.PackRows([(0, 10), (5, 15), (10, 21)]);

        await Assert.That(rows).IsEquivalentTo([0, 1, 0]);
    }

    [Test]
    public async Task PackRowsExpectsSortedInput()
    {
        // Caller sorts by (min, max); packing is greedy first-fit.
        IReadOnlyList<int> rows = SkillProgressionMath.PackRows([(0, 7), (0, 21), (7, 14)]);

        await Assert.That(rows).IsEquivalentTo([0, 1, 0]);
    }

    [Test]
    public async Task ValidationRejectsZeroBands()
    {
        await Assert.That(SkillProgressionMath.Validate(0, [], [])).IsFalse();
    }

    [Test]
    public async Task ValidationRejectsMismatchedBandCounts()
    {
        await Assert.That(SkillProgressionMath.Validate(2, [0], [7, 21])).IsFalse();
    }

    [Test]
    public async Task ValidationRejectsBandBelowAxis()
    {
        await Assert.That(SkillProgressionMath.Validate(1, [-1], [7])).IsFalse();
    }

    [Test]
    public async Task ValidationRejectsBandAboveAxis()
    {
        await Assert.That(SkillProgressionMath.Validate(1, [10], [22])).IsFalse();
    }

    [Test]
    public async Task ValidationRejectsBandShorterThanMinimumSpan()
    {
        await Assert.That(SkillProgressionMath.Validate(1, [10], [13])).IsFalse();
    }

    [Test]
    public async Task ValidationAcceptsBandAtMinimumSpan()
    {
        await Assert.That(SkillProgressionMath.Validate(1, [17], [21])).IsTrue();
    }

    [Test]
    public async Task ValidationAcceptsFullyOverlappingBands()
    {
        await Assert.That(SkillProgressionMath.Validate(2, [0, 0], [21, 21])).IsTrue();
    }

    [Test]
    [Arguments(4, 21, 2, 8)]
    [Arguments(4, 21, 25, 17)]
    [Arguments(4, 21, 12, 12)]
    [Arguments(0, 8, 21, 4)]
    public async Task SharedEdgeClampRespectsBothSpans(int leftMinimum, int rightMaximum, int desired, int expected)
    {
        int result = SkillProgressionMath.ClampSharedEdge(leftMinimum, rightMaximum, desired);

        await Assert.That(result).IsEqualTo(expected);
    }
}
