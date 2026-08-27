namespace RimShared.Common.Tests;

public class VariableViewportLayoutTests
{
    [Test]
    public async Task ZeroRowsHaveZeroContentAndAnEmptyRange()
    {
        var layout = new VariableViewportLayout([]);

        var range = layout.Calculate(0f, 100f, overscan: 3);

        await Assert.That(layout.Count).IsEqualTo(0);
        await Assert.That(layout.ContentExtent).IsEqualTo(0f);
        await Assert.That(range.Start).IsEqualTo(0);
        await Assert.That(range.EndExclusive).IsEqualTo(0);
        await Assert.That(range.IsEmpty).IsTrue();
    }

    [Test]
    public async Task ExactBoundariesUseAHalfOpenRange()
    {
        var layout = new VariableViewportLayout([10f, 20f, 15f]);

        var range = layout.Calculate(viewportStart: 10f, viewportExtent: 20f);

        await Assert.That(range.Start).IsEqualTo(1);
        await Assert.That(range.EndExclusive).IsEqualTo(2);
        await Assert.That(range.Count).IsEqualTo(1);
    }

    [Test]
    public async Task PartialFirstAndLastRowsAreIncluded()
    {
        var layout = new VariableViewportLayout([10f, 20f, 15f]);

        var range = layout.Calculate(viewportStart: 5f, viewportExtent: 37f);

        await Assert.That(range.Start).IsEqualTo(0);
        await Assert.That(range.EndExclusive).IsEqualTo(3);
    }

    [Test]
    [Arguments(-20f, 10f, 0, 0)]
    [Arguments(50f, 10f, 3, 3)]
    public async Task ViewportsOutsideContentAreEmpty(float viewportStart, float viewportExtent, int expectedStart, int expectedEndExclusive)
    {
        var layout = new VariableViewportLayout([10f, 20f, 15f]);

        var range = layout.Calculate(viewportStart, viewportExtent, overscan: 2);

        await Assert.That(range.Start).IsEqualTo(expectedStart);
        await Assert.That(range.EndExclusive).IsEqualTo(expectedEndExclusive);
    }

    [Test]
    [Arguments(15f, 1)]
    [Arguments(10f, 1)]
    [Arguments(45f, 3)]
    public async Task ZeroHeightViewportIsEmptyAtItsInsertionRow(float viewportStart, int expectedIndex)
    {
        var layout = new VariableViewportLayout([10f, 20f, 15f]);

        var range = layout.Calculate(viewportStart, viewportExtent: 0f);

        await Assert.That(range.Start).IsEqualTo(expectedIndex);
        await Assert.That(range.EndExclusive).IsEqualTo(expectedIndex);
    }

    [Test]
    public async Task NonuniformOffsetsAndExtentsAreStable()
    {
        var layout = new VariableViewportLayout([7f, 31f, 4f, 19f]);

        await Assert.That(layout.ContentExtent).IsEqualTo(61f);
        await Assert.That(layout.OffsetOf(0)).IsEqualTo(0f);
        await Assert.That(layout.OffsetOf(1)).IsEqualTo(7f);
        await Assert.That(layout.OffsetOf(3)).IsEqualTo(42f);
        await Assert.That(layout.ExtentOf(1)).IsEqualTo(31f);

        var range = layout.Calculate(viewportStart: 36f, viewportExtent: 8f);
        await Assert.That(range.Start).IsEqualTo(1);
        await Assert.That(range.EndExclusive).IsEqualTo(4);
    }

    [Test]
    public async Task OverscanExpandsANonemptyRange()
    {
        var layout = new VariableViewportLayout([10f, 20f, 15f, 5f]);

        var range = layout.Calculate(viewportStart: 10f, viewportExtent: 20f, overscan: 1);

        await Assert.That(range.Start).IsEqualTo(0);
        await Assert.That(range.EndExclusive).IsEqualTo(3);
    }

    [Test]
    public async Task OverscanClampsAtContentEnd()
    {
        var layout = new VariableViewportLayout([10f, 20f, 15f, 5f]);

        var range = layout.Calculate(viewportStart: 30f, viewportExtent: 15f, overscan: 2);

        await Assert.That(range.Start).IsEqualTo(0);
        await Assert.That(range.EndExclusive).IsEqualTo(4);
    }

    [Test]
    public async Task ConstructorDefensivelyCopiesInputHeights()
    {
        float[] heights = [10f, 20f, 15f];
        var layout = new VariableViewportLayout(heights);

        heights[0] = 1000f;
        heights[1] = 1000f;

        await Assert.That(layout.ContentExtent).IsEqualTo(45f);
        await Assert.That(layout.OffsetOf(1)).IsEqualTo(10f);
        var range = layout.Calculate(viewportStart: 10f, viewportExtent: 20f);
        await Assert.That(range.Start).IsEqualTo(1);
        await Assert.That(range.EndExclusive).IsEqualTo(2);
    }

    [Test]
    [Arguments(0f)]
    [Arguments(float.NaN)]
    public async Task InvalidSingleHeightIsRejected(float invalidHeight)
    {
        await Assert.That(() => new VariableViewportLayout([invalidHeight])).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task HeightSumThatLosesFloatPrecisionIsRejected()
    {
        await Assert.That(() => new VariableViewportLayout([16777216f, 1f])).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(float.NaN, 1f, 0)]
    [Arguments(0f, -1f, 0)]
    [Arguments(0f, 1f, -1)]
    public async Task InvalidViewportArgumentIsRejected(float viewportStart, float viewportExtent, int overscan)
    {
        var layout = new VariableViewportLayout([10f]);

        await Assert.That(() => layout.Calculate(viewportStart, viewportExtent, overscan)).Throws<ArgumentOutOfRangeException>();
    }
}
