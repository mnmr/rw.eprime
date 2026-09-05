using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// The header surface must rebuild exactly when a header-visible input
/// changes — and for nothing else. Equality is field-wise so the steady
/// per-event comparison allocates nothing.
public class PanelHeaderRevisionTests
{
    private static PanelHeaderRevision Baseline() => new(
        showSearch: true, showTitle: false,
        searchText: "steel", title: "Readouts", titleWidth: 60f,
        headerWidth: 140f, headerHeight: 26, uiRevision: 7,
        rasterScale: 1.25f);

    [Test]
    public async Task EqualFieldsCompareEqual()
    {
        await Assert.That(Baseline().Equals(Baseline())).IsTrue();
    }

    [Test]
    public async Task EachDependencyInvalidates()
    {
        var baseline = Baseline();
        var changed = new[]
        {
            new PanelHeaderRevision(false, false, "steel", "Readouts", 60f, 140f, 26, 7, 1.25f),
            new PanelHeaderRevision(true, false, "wood", "Readouts", 60f, 140f, 26, 7, 1.25f),
            new PanelHeaderRevision(true, false, "steel", "Other", 60f, 140f, 26, 7, 1.25f),
            new PanelHeaderRevision(true, false, "steel", "Readouts", 61f, 140f, 26, 7, 1.25f),
            new PanelHeaderRevision(true, false, "steel", "Readouts", 60f, 150f, 26, 7, 1.25f),
            new PanelHeaderRevision(true, false, "steel", "Readouts", 60f, 140f, 27, 7, 1.25f),
            new PanelHeaderRevision(true, false, "steel", "Readouts", 60f, 140f, 26, 8, 1.25f),
            new PanelHeaderRevision(true, false, "steel", "Readouts", 60f, 140f, 26, 7, 1f),
        };
        foreach (var revision in changed)
            await Assert.That(revision.Equals(baseline)).IsFalse();
    }

    [Test]
    public async Task SurfaceGrowsPastThePanelWidthOnlyForATitleThatNeedsIt()
    {
        // Title after the 26-unit gear column: 26 + 120 = 146 > 140.
        var wideTitle = new PanelHeaderRevision(
            false, true, "", "EPrime's Readouts", 120f, 140f, 26, 7, 1f);
        await Assert.That(wideTitle.SurfaceWidth).IsEqualTo(146f);

        var narrowTitle = new PanelHeaderRevision(
            false, true, "", "Readouts", 60f, 140f, 26, 7, 1f);
        await Assert.That(narrowTitle.SurfaceWidth).IsEqualTo(140f);

        // The search field and the empty header always fit the panel.
        var search = new PanelHeaderRevision(
            true, false, "steel", "EPrime's Readouts", 120f, 140f, 26, 7, 1f);
        await Assert.That(search.SurfaceWidth).IsEqualTo(140f);
        var plain = new PanelHeaderRevision(
            false, false, "", "EPrime's Readouts", 120f, 140f, 26, 7, 1f);
        await Assert.That(plain.SurfaceWidth).IsEqualTo(140f);
    }

    [Test]
    public async Task NullStringsNormalizeToEmpty()
    {
        var left = new PanelHeaderRevision(
            false, true, null!, null!, 60f, 140f, 26, 7, 1f);
        var right = new PanelHeaderRevision(
            false, true, "", "", 60f, 140f, 26, 7, 1f);
        await Assert.That(left.Equals(right)).IsTrue();
    }
}
