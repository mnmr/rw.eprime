using WorkRoles.Core;

namespace WorkRoles.Core.Tests.UI;

public class RecommendationChipLayoutTests
{
    // Fourteen mixed-width preview chips occupy four rows at full width. The
    // first eleven fit the three-row viewport; the final three used to vanish.
    private static readonly float[] Widths =
        [80f, 84f, 86f, 108f, 112f, 96f, 78f, 96f, 104f, 92f, 120f, 88f, 86f, 84f];

    [Test]
    public async Task OverflowKeepsAnOversizedRolesRightEdgeAndRemoveControlReachable()
    {
        // The 620px role name needs its own row, but its right-hand remove
        // control must remain inside the 384px scroll content, not be clipped.
        var layout = RecommendationChipLayout.Calculate(
            [80f, 620f, 84f, 150f, 120f, 88f], 400f, 52f, 24f, 4f, 16f);

        await Assert.That(layout.ChipCount).IsEqualTo(6);
        await Assert.That(layout.Scrollable).IsTrue();
        await Assert.That(layout.ChipWidth(1)).IsEqualTo(384f);
        await Assert.That(layout.ChipLeft(1)).IsEqualTo(0f);
        await Assert.That(layout.ChipTop(1)).IsEqualTo(28f);
        for (int i = 0; i < layout.ChipCount; i++)
            await Assert.That(layout.ChipLeft(i) + layout.ChipWidth(i) <= layout.ContentWidth).IsTrue();
        float bottom = layout.ClampScroll(float.MaxValue);
        await Assert.That(layout.ChipTop(5) - bottom + 24f).IsEqualTo(52f);

        // No scroll clip is added when every row already fits. Keep that
        // existing drawing geometry, including its original measured width.
        var fullHeight = RecommendationChipLayout.Calculate(
            [80f, 620f, 84f, 150f, 120f, 88f], 400f, 108f, 24f, 4f, 16f);
        await Assert.That(fullHeight.Scrollable).IsFalse();
        await Assert.That(fullHeight.ChipWidth(1)).IsEqualTo(620f);
    }
    [Test]
    public async Task OverflowKeepsEveryRecommendationAndReflowsAwayFromTheScrollbar()
    {
        var layout = RecommendationChipLayout.Calculate(Widths, 400f, 80f, 24f, 4f, 16f);

        await Assert.That(layout.ChipCount).IsEqualTo(14);
        await Assert.That(layout.Scrollable).IsTrue();
        await Assert.That(layout.ContentWidth).IsEqualTo(384f);
        // Reserving the scrollbar adds a fifth row: the fourth chip of row
        // two and the final chip no longer fit their old rows.
        await Assert.That(layout.ChipTop(7)).IsEqualTo(56f);
        await Assert.That(layout.ChipTop(13)).IsEqualTo(112f);
        await Assert.That(layout.ContentHeight).IsEqualTo(136f);
        float bottom = layout.ClampScroll(float.MaxValue);
        await Assert.That(bottom).IsEqualTo(56f);
        await Assert.That(layout.ChipTop(13) - bottom + 24f).IsEqualTo(80f);
        for (int i = 0; i < Widths.Length; i++)
            await Assert.That(layout.ChipLeft(i) + Widths[i] <= 384f).IsTrue();
    }

    [Test]
    public async Task ACompletePreviewKeepsItsOriginalGeometryAndClampsAnOldScrollOffset()
    {
        var layout = RecommendationChipLayout.Calculate(Widths, 400f, 108f, 24f, 4f, 16f);

        await Assert.That(layout.ChipCount).IsEqualTo(14);
        await Assert.That(layout.Scrollable).IsFalse();
        await Assert.That(layout.ContentWidth).IsEqualTo(400f);
        await Assert.That(layout.ChipLeft(3)).IsEqualTo(262f);
        await Assert.That(layout.ChipTop(3)).IsEqualTo(0f);
        await Assert.That(layout.ChipLeft(13)).IsEqualTo(182f);
        await Assert.That(layout.ChipTop(13)).IsEqualTo(84f);
        await Assert.That(layout.ContentHeight).IsEqualTo(108f);
        await Assert.That(layout.ClampScroll(56f)).IsEqualTo(0f);
        await Assert.That(layout.ClampScroll(-10f)).IsEqualTo(0f);
    }
}
