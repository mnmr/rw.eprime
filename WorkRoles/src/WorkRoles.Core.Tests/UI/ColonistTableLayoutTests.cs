using WorkRoles.Core;

namespace WorkRoles.Core.Tests.UI;

public class ColonistTableLayoutTests
{
    [Test]
    public async Task PriorityGridButtonUsesTheFullHeaderRightEdge()
    {
        ColonistTableHeaderLayout layout =
            ColonistTableHeaderLayout.Calculate(
                tableLeft: 10f, tableTop: 20f, tableWidth: 300f);

        await Assert.That(layout.HeaderWidth).IsEqualTo(300f);
        await Assert.That(layout.ScrollContentWidth).IsEqualTo(284f);
        await Assert.That(layout.PriorityGridLeft).IsEqualTo(266f);
        await Assert.That(layout.PriorityGridTop).IsEqualTo(25f);
        await Assert.That(310f
            - (layout.PriorityGridLeft + layout.PriorityGridWidth))
            .IsEqualTo(5f);
        await Assert.That(layout.PriorityGridTop - 20f).IsEqualTo(5f);
    }
}
