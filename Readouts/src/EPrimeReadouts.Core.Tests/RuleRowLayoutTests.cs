using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// A count-rule row puts its label on the left and the three-segment
/// selector on the right. When the two do not fit side by side the selector
/// first gives up its caption padding, and when even that is not enough it
/// moves to its own line under the label instead of overlapping it.
public class RuleRowLayoutTests
{
    [Test]
    public async Task WideRowKeepsThePreferredSelectorBesideTheLabel()
    {
        var layout = RuleRowLayout.Solve(
            rowWidth: 600f, labelWidth: 175f, preferredSelectorWidth: 303f,
            minSelectorWidth: 255f, gap: 8f);

        await Assert.That(layout.Stacked).IsFalse();
        await Assert.That(layout.SelectorWidth).IsEqualTo(303f);
    }

    [Test]
    public async Task TightRowNarrowsTheSelectorDownToItsMinimum()
    {
        var layout = RuleRowLayout.Solve(
            rowWidth: 460f, labelWidth: 175f, preferredSelectorWidth: 303f,
            minSelectorWidth: 255f, gap: 8f);

        await Assert.That(layout.Stacked).IsFalse();
        await Assert.That(layout.SelectorWidth).IsEqualTo(460f - 175f - 8f);
    }

    [Test]
    public async Task RowTooNarrowForBothStacksTheSelectorUnderTheLabel()
    {
        var layout = RuleRowLayout.Solve(
            rowWidth: 400f, labelWidth: 175f, preferredSelectorWidth: 303f,
            minSelectorWidth: 255f, gap: 8f);

        await Assert.That(layout.Stacked).IsTrue();
        await Assert.That(layout.SelectorWidth).IsEqualTo(303f);

        // A row narrower than the selector itself shrinks it to the row.
        var cramped = RuleRowLayout.Solve(
            rowWidth: 280f, labelWidth: 175f, preferredSelectorWidth: 303f,
            minSelectorWidth: 255f, gap: 8f);
        await Assert.That(cramped.Stacked).IsTrue();
        await Assert.That(cramped.SelectorWidth).IsEqualTo(280f);
    }
}
