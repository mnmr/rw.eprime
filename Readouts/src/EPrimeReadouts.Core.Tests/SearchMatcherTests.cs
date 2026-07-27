using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class SearchMatcherTests
{
    [Test]
    public async Task WhitespaceQueryIsInactive()
    {
        await Assert.That(SearchMatcher.IsActive(null)).IsFalse();
        await Assert.That(SearchMatcher.IsActive("  ")).IsFalse();
        await Assert.That(SearchMatcher.IsActive("a")).IsTrue();
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
