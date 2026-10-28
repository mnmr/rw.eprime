using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class PanelTextRevisionTests
{
    // The glyph cache must observe font changes even when counts and cell
    // rectangles stay equal (for example a sparse, minimum-sized layout).
    [Test]
    public async Task CounterFontInvalidatesGlyphsWithIdenticalContentAndGeometry()
    {
        var model = Model("128", Band.Normal);
        var tiny = PanelTextRevision.Create(model, 7, 140, 400, counterFont: 0);
        var small = PanelTextRevision.Create(model, 7, 140, 400, counterFont: 1);
        await Assert.That(tiny).IsNotEqualTo(small);
        await Assert.That(PanelTextRevision.Create(model, 7, 140, 400, counterFont: 0))
            .IsEqualTo(tiny);
    }

    [Test]
    public async Task EquivalentDisplayedTextReusesProductAcrossModels()
    {
        PanelTextRevision left = PanelTextRevision.Create(
            Model("128", Band.Normal), 7, 140, 400);
        PanelTextRevision right = PanelTextRevision.Create(
            Model("128", Band.Normal), 7, 140, 400);

        await Assert.That(left).IsEqualTo(right);
    }

    [Test]
    public async Task CounterTextInvalidatesProduct()
    {
        PanelTextRevision left = PanelTextRevision.Create(
            Model("128", Band.Normal), 7, 140, 400);
        PanelTextRevision right = PanelTextRevision.Create(
            Model("129", Band.Normal), 7, 140, 400);

        await Assert.That(left).IsNotEqualTo(right);
    }

    [Test]
    public async Task ThresholdTintInvalidatesProduct()
    {
        PanelTextRevision left = PanelTextRevision.Create(
            Model("128", Band.Normal), 7, 140, 400);
        PanelTextRevision right = PanelTextRevision.Create(
            Model("128", Band.Low), 7, 140, 400);

        await Assert.That(left).IsNotEqualTo(right);
    }

    [Test]
    [Arguments(8, 140, 400)]
    [Arguments(7, 141, 400)]
    [Arguments(7, 140, 401)]
    public async Task UiRevisionAndDimensionsInvalidateProduct(
        int uiRevision, int width, int height)
    {
        PanelTextRevision baseline = PanelTextRevision.Create(
            Model("128", Band.Normal), 7, 140, 400);
        PanelTextRevision changed = PanelTextRevision.Create(
            Model("128", Band.Normal), uiRevision, width, height);

        await Assert.That(changed).IsNotEqualTo(baseline);
    }

    private static RenderModel Model(string text, Band band)
    {
        var model = new RenderModel();
        model.Cells.Add(new RenderCell
        {
            Kind = CellKind.Counter,
            Text = text,
            Band = band,
        });
        return model;
    }
}
