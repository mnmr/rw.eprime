using RimShared.Common;

namespace RimShared.Common.Tests;

public class SearchMatcherTests
{
    [Test]
    public async Task TurkishDisplaySearchUsesSelectedLanguageAndKeepsDistinctLetters()
    {
        var turkish = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        await Assert.That(SearchMatcher.Matches("İnşaatçı", "inşaatçı", turkish)).IsTrue();
        await Assert.That(SearchMatcher.Matches("İtfaiyeci", "itfaiye", turkish)).IsTrue();
        await Assert.That(SearchMatcher.Matches("Işık", "ışık", turkish)).IsTrue();
        await Assert.That(SearchMatcher.Matches("Işık", "işik", turkish)).IsFalse();
        // The explicit display choice never changes identifier/default matching.
        await Assert.That(SearchMatcher.Matches("IRON", "iron")).IsTrue();
        await Assert.That(SearchMatcher.Matches("İnşaatçı", "inşaatçı")).IsFalse();
    }

    [Test]
    public async Task WhitespaceQueryIsInactive()
    {
        await Assert.That(SearchMatcher.IsActive(null)).IsFalse();
        await Assert.That(SearchMatcher.IsActive("  ")).IsFalse();
        await Assert.That(SearchMatcher.IsActive("ab")).IsTrue();
    }

    /// A single character already searches; padding whitespace is ignored.
    [Test]
    public async Task SingleCharacterQueryIsActive()
    {
        await Assert.That(SearchMatcher.IsActive("a")).IsTrue();
        await Assert.That(SearchMatcher.IsActive(" a ")).IsTrue();
        await Assert.That(SearchMatcher.IsActive(" ab ")).IsTrue();
        await Assert.That(SearchMatcher.Matches("simple meal", "m")).IsTrue();
        await Assert.That(SearchMatcher.Matches("steel", "m")).IsFalse();
    }

    [Test]
    public async Task MatchIsCaseInsensitiveSubstring()
    {
        await Assert.That(SearchMatcher.Matches("simple meal", "MEAL")).IsTrue();
        await Assert.That(SearchMatcher.Matches("simple meal", " meal ")).IsTrue();
        await Assert.That(SearchMatcher.Matches("steel", "meal")).IsFalse();
        await Assert.That(SearchMatcher.Matches(null, "meal")).IsFalse();
    }
}
