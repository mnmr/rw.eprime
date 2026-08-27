using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class PanelSurfaceSizingTests
{
    [Test]
    public async Task RasterizesLogicalExtentAtUiScale()
    {
        PanelSurfaceSizing sizing = PanelSurfaceSizing.Create(
            headerWidth: 140f,
            contentWidth: 231f,
            logicalHeight: 300,
            uiScale: 1.25f);

        await Assert.That(sizing.PixelWidth).IsEqualTo(289);
        await Assert.That(sizing.PixelHeight).IsEqualTo(375);
    }

    [Test]
    public async Task ExpandedContentDoesNotResizeHeader()
    {
        PanelSurfaceSizing sizing = PanelSurfaceSizing.Create(
            headerWidth: 140f,
            contentWidth: 231f,
            logicalHeight: 300,
            uiScale: 1.25f);

        await Assert.That(sizing.LogicalWidth).IsEqualTo(231);
        await Assert.That(sizing.HeaderWidth).IsEqualTo(140f);
    }

    [Test]
    public async Task PresentationExtentMapsEveryTexturePixelOneToOne()
    {
        PanelSurfaceSizing collapsed = PanelSurfaceSizing.Create(
            headerWidth: 140f,
            contentWidth: 153f,
            logicalHeight: 300,
            uiScale: 1.25f);
        PanelSurfaceSizing expanded = PanelSurfaceSizing.Create(
            headerWidth: 140f,
            contentWidth: 464f,
            logicalHeight: 300,
            uiScale: 1.25f);

        await Assert.That(collapsed.PresentationWidth)
            .IsGreaterThan(153.59f);
        await Assert.That(collapsed.PresentationWidth)
            .IsLessThan(153.61f);
        await Assert.That(expanded.PresentationWidth)
            .IsEqualTo(464f);
    }
}
