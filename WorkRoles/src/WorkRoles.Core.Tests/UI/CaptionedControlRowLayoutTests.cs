using WorkRoles.Core;

namespace WorkRoles.Core.Tests.UI;

public class CaptionedControlRowLayoutTests
{
    [Test]
    [Arguments(14.2f, 16f)]
    [Arguments(21.3f, 22f)]
    public async Task FontFallbackOnlyGrowsTheVisualCaptionBounds(
        float lineHeight, float expectedVisualHeight)
    {
        CaptionedControlRowLayout layout =
            CaptionedControlRowLayout.Calculate(
                lineHeight, captionAdvance: 16f,
                controlHeight: 24f, captionGap: 1f);

        await Assert.That(layout.CaptionVisualHeight)
            .IsEqualTo(expectedVisualHeight);
        await Assert.That(layout.CaptionAdvance).IsEqualTo(16f);
        await Assert.That(layout.RowHeight).IsEqualTo(41f);
    }
}
