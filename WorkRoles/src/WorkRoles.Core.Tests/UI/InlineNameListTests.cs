using WorkRoles.Core;

namespace WorkRoles.Core.Tests.UI;

public class InlineNameListTests
{
    [Test]
    public async Task NamesReserveTheActualTranslatedRemainingCount()
    {
        // Three 50px names: German's +2 weitere needs 90px, not an
        // English-sized 70px reservation. The caption must stay on the row.
        float[] names = [50, 50, 50];
        float[] german = [0, 85, 90, 95];
        await Assert.That(InlineNameList.VisibleCount(135, names, 5, german)).IsEqualTo(0);
        await Assert.That(InlineNameList.VisibleCount(150, names, 5, german)).IsEqualTo(1);
        // When the complete list fits, no unnecessary caption is reserved.
        await Assert.That(InlineNameList.VisibleCount(160, names, 5, german)).IsEqualTo(3);
        float[] shorter = [0, 25, 30, 35];
        await Assert.That(InlineNameList.VisibleCount(135, names, 5, shorter)).IsEqualTo(2);
    }
}
