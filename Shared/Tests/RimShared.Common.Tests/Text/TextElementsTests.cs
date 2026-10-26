using RimShared.Common;

namespace RimShared.Common.Tests;

public class TextElementsTests
{
    [Test]
    [Arguments("e\u0301")]
    [Arguments("か\u3099")]
    [Arguments("ｶﾞ")]
    [Arguments("\U00020000")]
    [Arguments("\u1112\u1161\u11AB")]
    [Arguments("\U0001F469\u200D\U0001F4BB")]
    public async Task NameLimitKeepsTheCompleteLastElement(string lastElement)
    {
        // This pure input boundary supplements the native NameDialog repro:
        // the 40th displayed element survives, and the 41st does not.
        string accepted = new string('a', 39) + lastElement;

        await Assert.That(TextElements.Prefix(accepted + "b", 40)).IsEqualTo(accepted);
        await Assert.That(ReferenceEquals(TextElements.Prefix(accepted, 40), accepted)).IsTrue();
    }

    [Test]
    public async Task UnlimitedAndEmptyNamesReuseTheOriginalInput()
    {
        const string name = "a\u0301\U00020000";
        await Assert.That(ReferenceEquals(TextElements.Prefix(name, int.MaxValue), name)).IsTrue();
        await Assert.That(TextElements.Prefix(name, 0)).IsEqualTo("");
        await Assert.That(TextElements.Prefix("", 40)).IsEqualTo("");
    }
}
