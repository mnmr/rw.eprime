using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class PanelVisualOptionsTests
{
    [Test]
    public async Task BandOpacityIsClampedToAValidAlphaRange()
    {
        await Assert.That(new PanelVisualOptions(-0.5f).BandOpacity)
            .IsEqualTo(0f);
        await Assert.That(new PanelVisualOptions(1.5f).BandOpacity)
            .IsEqualTo(1f);
    }

    [Test]
    public async Task EqualOpacityProducesEqualRendererOptions()
    {
        await Assert.That(new PanelVisualOptions(0.35f))
            .IsEqualTo(new PanelVisualOptions(0.35f));
        await Assert.That(new PanelVisualOptions(0.25f))
            .IsNotEqualTo(new PanelVisualOptions(0.35f));
    }
}
