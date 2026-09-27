using TUnit.Assertions.Enums;
using WorkRoles.Core;

namespace WorkRoles.Core.Tests.UI;

public class SegmentWidthLayoutTests
{
    // Measured Small-font label widths plus padding for the Time demand
    // picker (Part-time, Full-time, Opportunistic).
    private static readonly float[] TimeLabels = { 62f, 59f, 91f };

    [Test]
    public async Task RoomyColumnKeepsEqualSegments()
    {
        float[] widths = SegmentWidthLayout.Allocate(TimeLabels, 330f);

        await Assert.That(widths).IsEquivalentTo(
            new[] { 110f, 110f, 110f }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task FloorColumnGivesTheLongLabelItsFullWidth()
    {
        // An equal 72.7px split would clip Opportunistic; the total fits, so
        // the 6px of slack is shared evenly.
        float[] widths = SegmentWidthLayout.Allocate(TimeLabels, 218f);

        await Assert.That(widths).IsEquivalentTo(
            new[] { 64f, 61f, 93f }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task OverfullRowCapsOnlyTheLabelsThatCannotFit()
    {
        // The 204px layout-floor space: Part-time and Full-time keep their
        // full widths, only Opportunistic gives up room (and truncates).
        float[] widths = SegmentWidthLayout.Allocate(TimeLabels, 204f);

        await Assert.That(widths).IsEquivalentTo(
            new[] { 62f, 59f, 83f }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task EveryLabelTooWideSharesTheSpaceEqually()
    {
        float[] widths = SegmentWidthLayout.Allocate(TimeLabels, 90f);

        await Assert.That(widths).IsEquivalentTo(
            new[] { 30f, 30f, 30f }, CollectionOrdering.Matching);
    }
}
