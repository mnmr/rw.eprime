using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

/// Scroll presentation must be a pure pixel-snapped window into the cached
/// content texture: one-to-one texel mapping at every scroll offset, clamped
/// at the content bottom, and invisible when nothing can be shown.
public class PanelPresentWindowTests
{
    [Test]
    public async Task UnscrolledViewportMapsTopOfTextureOneToOne()
    {
        // 500 logical px of content at 1.25 scale = 625 physical; a 400 px
        // logical viewport shows the top 500 physical pixels.
        var window = PanelPresentWindow.Create(
            175, 625, 1.25f, scrollY: 0f, viewportLogicalHeight: 400f);
        await Assert.That(window.Visible).IsTrue();
        await Assert.That(window.TopPixels).IsEqualTo(0);
        await Assert.That(window.HeightPixels).IsEqualTo(500);
        await Assert.That(window.DestWidth).IsEqualTo(175f / 1.25f);
        await Assert.That(window.DestHeight).IsEqualTo(500f / 1.25f);
        await Assert.That(window.UvY).IsEqualTo(125f / 625f);
        await Assert.That(window.UvHeight).IsEqualTo(500f / 625f);
    }

    [Test]
    public async Task FractionalScrollSnapsToPhysicalPixelGrid()
    {
        // 10.3 logical * 1.25 = 12.875 physical → snaps to 13 whole pixels.
        var window = PanelPresentWindow.Create(
            175, 625, 1.25f, scrollY: 10.3f, viewportLogicalHeight: 400f);
        await Assert.That(window.TopPixels).IsEqualTo(13);
        await Assert.That(window.HeightPixels).IsEqualTo(500);
        await Assert.That(window.UvY).IsEqualTo((625f - 13f - 500f) / 625f);
    }

    [Test]
    public async Task BottomScrollClampsToRemainingContent()
    {
        // Scrolled to (or past) the end: the window covers exactly the
        // remaining texture rows and the destination shrinks to match.
        var window = PanelPresentWindow.Create(
            175, 625, 1.25f, scrollY: 200f, viewportLogicalHeight: 400f);
        await Assert.That(window.TopPixels).IsEqualTo(250);
        await Assert.That(window.HeightPixels).IsEqualTo(375);
        await Assert.That(window.DestHeight).IsEqualTo(375f / 1.25f);
        await Assert.That(window.UvY).IsEqualTo(0f);
    }

    [Test]
    public async Task NegativeScrollClampsToTop()
    {
        var window = PanelPresentWindow.Create(
            175, 625, 1.25f, scrollY: -5f, viewportLogicalHeight: 400f);
        await Assert.That(window.TopPixels).IsEqualTo(0);
    }

    [Test]
    public async Task DegenerateInputsAreInvisible()
    {
        await Assert.That(PanelPresentWindow.Create(
            0, 625, 1.25f, 0f, 400f).Visible).IsFalse();
        await Assert.That(PanelPresentWindow.Create(
            175, 0, 1.25f, 0f, 400f).Visible).IsFalse();
        await Assert.That(PanelPresentWindow.Create(
            175, 625, 0f, 0f, 400f).Visible).IsFalse();
        await Assert.That(PanelPresentWindow.Create(
            175, 625, 1.25f, 0f, 0f).Visible).IsFalse();
        // Scrolled fully past the content: nothing left to show.
        await Assert.That(PanelPresentWindow.Create(
            175, 625, 1.25f, 500f, 400f).Visible).IsFalse();
    }

    [Test]
    public async Task ContentShorterThanViewportShowsAllContent()
    {
        var window = PanelPresentWindow.Create(
            175, 100, 1.25f, scrollY: 0f, viewportLogicalHeight: 400f);
        await Assert.That(window.HeightPixels).IsEqualTo(100);
        await Assert.That(window.UvY).IsEqualTo(0f);
        await Assert.That(window.UvHeight).IsEqualTo(1f);
    }
}
