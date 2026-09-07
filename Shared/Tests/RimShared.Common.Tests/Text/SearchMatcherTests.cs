using RimShared.Common;

namespace RimShared.Common.Tests;

public class SearchMatcherTests
{
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
