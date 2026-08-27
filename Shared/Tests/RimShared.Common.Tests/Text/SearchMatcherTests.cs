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

    [Test]
    public async Task QueryShorterThanMinLengthIsInactive()
    {
        await Assert.That(SearchMatcher.IsActive("a")).IsFalse();
        await Assert.That(SearchMatcher.IsActive(" a ")).IsFalse();
        await Assert.That(SearchMatcher.IsActive(" ab ")).IsTrue();
        await Assert.That(SearchMatcher.Matches("simple meal", "m")).IsFalse();
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
