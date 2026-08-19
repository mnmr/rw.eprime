using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class GlyphQuadPlanTests
{
    [Test]
    public async Task KeepsEveryCompleteGeneratedGlyphQuad()
    {
        await Assert.That(GlyphQuadPlan.UsableVertexCount(16))
            .IsEqualTo(16);
    }
}
