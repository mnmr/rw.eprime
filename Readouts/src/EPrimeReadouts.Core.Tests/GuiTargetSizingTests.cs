using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class GuiTargetSizingTests
{
    [Test]
    public async Task FullReadoutRetainsItsLastPixelAndOneToOneScrollWindow()
    {
        // Runtime reproduction: the 1.7 compositor makes a 447px viewport
        // for a 448px target at 1080p, shrinking every glyph and clipping the
        // last row. Add only transparent padding; logical content stays put.
        int width = GuiTargetSizing.MinimumExactExtent(259, 1920);
        int height = GuiTargetSizing.MinimumExactExtent(448, 1080);
        await Assert.That(width).IsEqualTo(259);
        await Assert.That(height).IsEqualTo(449);
        var viewport = PanelPresentWindow.Create(width, height, 1.25f, 0f, 358f);
        await Assert.That(viewport.HeightPixels).IsEqualTo(448);
        await Assert.That(viewport.DestHeight * 1.25f).IsEqualTo(448f);
        await Assert.That(viewport.UvY).IsEqualTo(1f / 449f);
    }
}
