using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class TipTableAlignmentTests
{
    [Test]
    public async Task NumericCellsAlignToTheRightEdgeOfTheirColumn()
    {
        await Assert.That(TipTableLayout.TextX(
                columnX: 108f, columnWidth: 24f, textWidth: 18f,
                TipColumnAlignment.Right))
            .IsEqualTo(114f);
        await Assert.That(TipTableLayout.TextX(
                columnX: 108f, columnWidth: 24f, textWidth: 18f,
                TipColumnAlignment.Left))
            .IsEqualTo(108f);
    }
}
