using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class ScopeCacheStampTests
{
    [Test]
    public async Task SameRevisionsReuseDependentCacheKeys()
    {
        var first = new ScopeCacheStamp(uiRevision: 17, pawnListRevision: 4);
        var second = new ScopeCacheStamp(uiRevision: 17, pawnListRevision: 4);

        await Assert.That(second).IsEqualTo(first);
    }

    [Test]
    public async Task ScopeChangeInvalidatesEveryDependentCacheKey()
    {
        var before = new ScopeCacheStamp(uiRevision: 17, pawnListRevision: 4);
        var after = new ScopeCacheStamp(uiRevision: 17, pawnListRevision: 5);

        await Assert.That(after).IsNotEqualTo(before);
    }

    [Test]
    public async Task GlobalUiChangeStillInvalidatesDependentCacheKeys()
    {
        var before = new ScopeCacheStamp(uiRevision: 17, pawnListRevision: 4);
        var after = new ScopeCacheStamp(uiRevision: 18, pawnListRevision: 4);

        await Assert.That(after).IsNotEqualTo(before);
    }
}
