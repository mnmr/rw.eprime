using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class InclinedLabelGeometryTests
{
    [Test]
    public async Task FortyFiveDegreeLabelMeasuresVerticalExtentAndRightRunOutIndependently()
    {
        var geometry = InclinedLabelGeometry.Calculate(
            labelWidth: 100f,
            labelHeight: 20f,
            angleDegrees: 45f);

        await Assert.That(geometry.VerticalExtent).IsGreaterThan(84.85f);
        await Assert.That(geometry.VerticalExtent).IsLessThan(84.86f);
        await Assert.That(geometry.RightRunOut).IsGreaterThan(70.71f);
        await Assert.That(geometry.RightRunOut).IsLessThan(70.72f);
        await Assert.That(geometry.AnchorToCenterX).IsGreaterThan(28.28f);
        await Assert.That(geometry.AnchorToCenterX).IsLessThan(28.29f);
        await Assert.That(geometry.AnchorToCenterY).IsGreaterThan(-42.43f);
        await Assert.That(geometry.AnchorToCenterY).IsLessThan(-42.42f);
    }

    [Test]
    public async Task InvalidLabelDimensionsAndAnglesAreRejected()
    {
        await Assert.That(() => InclinedLabelGeometry.Calculate(-1f, 20f, 45f))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => InclinedLabelGeometry.Calculate(100f, float.NaN, 45f))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => InclinedLabelGeometry.Calculate(100f, 20f, 0f))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => InclinedLabelGeometry.Calculate(100f, 20f, 90f))
            .Throws<ArgumentOutOfRangeException>();
    }
}
