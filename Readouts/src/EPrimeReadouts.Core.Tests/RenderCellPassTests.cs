using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class RenderCellPassTests
{
    [Test]
    public async Task TextCellsAreSeparatedFromVisualCellsForBatchedState()
    {
        await Assert.That(RenderCellPass.PassOf(CellKind.Counter))
            .IsEqualTo(CellRenderPass.Text);
        await Assert.That(RenderCellPass.PassOf(CellKind.Label))
            .IsEqualTo(CellRenderPass.Text);
        await Assert.That(RenderCellPass.PassOf(CellKind.Icon))
            .IsEqualTo(CellRenderPass.Visual);
        await Assert.That(RenderCellPass.PassOf(CellKind.GroupBack))
            .IsEqualTo(CellRenderPass.Visual);
        await Assert.That(RenderCellPass.PassOf(CellKind.Triangle))
            .IsEqualTo(CellRenderPass.Visual);
        await Assert.That(RenderCellPass.PassOf(CellKind.Highlight))
            .IsEqualTo(CellRenderPass.Visual);
        await Assert.That(RenderCellPass.PassOf(CellKind.EmptySlot))
            .IsEqualTo(CellRenderPass.Visual);
    }
}
