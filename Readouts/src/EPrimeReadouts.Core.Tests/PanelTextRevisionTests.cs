using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class PanelTextRevisionTests
{
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
