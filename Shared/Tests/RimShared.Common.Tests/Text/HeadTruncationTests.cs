using RimShared.Common;

namespace RimShared.Common.Tests;

public class HeadTruncationTests
{
    private static float Measure(string value) => value.Length;

    [Test]
    public async Task NarrowLabelKeepsItsBeginningAndNeverExceedsItsCell()
    {
        await Assert.That(HeadTruncation.Fit("Nessuna corrispondenza", 10, Measure, out float width)).IsEqualTo("Nessuna...");
        await Assert.That(width).IsEqualTo(10);
        await Assert.That(HeadTruncation.Fit("Long label", 2, Measure, out width)).IsEqualTo("");
        await Assert.That(width).IsEqualTo(0);
    }

    [Test]
    [Arguments("e\u0301")]
    [Arguments("\U00020000")]
    public async Task PrefixRetainsTheCompleteFirstTextElement(string element)
    {
        await Assert.That(HeadTruncation.Fit(element + "abcdefgh", element.Length + 3, Measure, out float width)).IsEqualTo(element + "...");
        await Assert.That(width).IsEqualTo(element.Length + 3);
    }

    [Test]
    public async Task OverrideCaptionPreservesImplantIdentityAcrossPhraseOrders()
    {
        await Assert.That(HeadTruncation.FitWithSubstitution("overrides {0}", "Bionic leg", 13, Measure, out _)).IsEqualTo("Bionic leg");
        await Assert.That(HeadTruncation.FitWithSubstitution("sostituisce {0}", "Gamba bionica", 16, Measure, out _)).IsEqualTo("Gamba bionica");
        await Assert.That(HeadTruncation.FitWithSubstitution("{0}を置換", "バイオニック義足", 8, Measure, out _)).IsEqualTo("バイオニック義足");
        await Assert.That(HeadTruncation.FitWithSubstitution("{0} 대체", "생체공학 다리", 8, Measure, out _)).IsEqualTo("생체공학 다리");
    }
}
