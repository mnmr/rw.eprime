using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class IconRenderPlanTests
{
    [Test]
    public async Task DirectRenderingCombinesCorrectionWithVanillaScale()
    {
        IconRenderPlan plan = IconRenderPlan.Create(
            hasUsableTexture: true,
            correctionScale: 0.75f,
            vanillaDrawScale: 1.2f);

        await Assert.That(plan.UseDirectRendering).IsTrue();
        await Assert.That(plan.CorrectionScale).IsEqualTo(0.75f);
        await Assert.That(plan.FittedScale).IsGreaterThan(0.8999f);
        await Assert.That(plan.FittedScale).IsLessThan(0.9001f);
    }

    [Test]
    public async Task MissingTexturePreservesCorrectionForThingIconFallback()
    {
        IconRenderPlan plan = IconRenderPlan.Create(
            hasUsableTexture: false,
            correctionScale: 1.15f,
            vanillaDrawScale: 0.8f);

        await Assert.That(plan.UseDirectRendering).IsFalse();
        await Assert.That(plan.CorrectionScale).IsEqualTo(1.15f);
    }
}
