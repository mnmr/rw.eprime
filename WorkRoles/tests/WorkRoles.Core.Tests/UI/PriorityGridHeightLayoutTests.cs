namespace WorkRoles.Core.Tests.UI;

public class PriorityGridHeightLayoutTests
{
    [Test]
    public async Task ShortRosterReservesMeasuredFixedAndScrollChromeHeight()
    {
        var layout = PriorityGridHeightLayout.Calculate(rowCount: 3, rowHeight: 27f, fixedContentHeight: 108f, windowMarginsHeight: 36f, scrollChromeHeight: 24f, maxWindowHeight: 900f);

        await Assert.That(layout.RowsContentHeight).IsEqualTo(81f);
        await Assert.That(layout.RowsPanelHeight).IsEqualTo(105f);
        await Assert.That(layout.WindowHeight).IsEqualTo(249f);
    }

    [Test]
    public async Task TallRosterCapsWindowAndAssignsRemainingHeightToRowsPanel()
    {
        var layout = PriorityGridHeightLayout.Calculate(rowCount: 50, rowHeight: 27f, fixedContentHeight: 108f, windowMarginsHeight: 36f, scrollChromeHeight: 24f, maxWindowHeight: 900f);

        await Assert.That(layout.RowsContentHeight).IsEqualTo(1350f);
        await Assert.That(layout.RowsPanelHeight).IsEqualTo(756f);
        await Assert.That(layout.WindowHeight).IsEqualTo(900f);
    }

    [Test]
    public async Task NegativeRowCountIsRejected()
    {
        await Assert
            .That(() => PriorityGridHeightLayout.Calculate(rowCount: -1, rowHeight: 27f, fixedContentHeight: 108f, windowMarginsHeight: 36f, scrollChromeHeight: 24f, maxWindowHeight: 900f))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ZeroRowHeightIsRejected()
    {
        await Assert
            .That(() => PriorityGridHeightLayout.Calculate(rowCount: 1, rowHeight: 0f, fixedContentHeight: 108f, windowMarginsHeight: 36f, scrollChromeHeight: 24f, maxWindowHeight: 900f))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task WindowTooShortForFixedContentIsRejected()
    {
        await Assert
            .That(() => PriorityGridHeightLayout.Calculate(rowCount: 1, rowHeight: 27f, fixedContentHeight: 108f, windowMarginsHeight: 36f, scrollChromeHeight: 24f, maxWindowHeight: 167f))
            .Throws<ArgumentOutOfRangeException>();
    }
}
