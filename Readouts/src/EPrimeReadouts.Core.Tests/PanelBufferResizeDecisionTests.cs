using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class PanelBufferResizeDecisionTests
{
    [Test]
    public async Task ResizingInactiveSurfacesRetainsPublishedFront()
    {
        PanelBufferResizeDecision decision =
            PanelBufferResizeDecision.Create(
                hasFront: true,
                workingWidth: 200,
                workingHeight: 300,
                backWidth: 200,
                backHeight: 300,
                nextWidth: 260,
                nextHeight: 300);

        await Assert.That(decision.KeepFront).IsTrue();
        await Assert.That(decision.ReplaceWorking).IsTrue();
        await Assert.That(decision.ReplaceBack).IsTrue();
    }
}
