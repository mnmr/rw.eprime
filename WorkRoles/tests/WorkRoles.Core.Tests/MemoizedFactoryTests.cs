using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class MemoizedFactoryTests
{
    [Test]
    public async Task SameKeyBuildsAndReturnsOneStableValue()
    {
        int builds = 0;
        var cache = new MemoizedFactory<int, Action>(key =>
        {
            builds++;
            return () => _ = key;
        });

        Action first = cache.For(7);
        Action repeated = cache.For(7);

        // Assert.That(Action) is interpreted as an executable delegate by the
        // runner, so compare the cached delegate references explicitly.
        await Assert.That(ReferenceEquals(repeated, first)).IsTrue();
        await Assert.That(builds).IsEqualTo(1);
    }

    [Test]
    public async Task ClearReleasesValuesSoTheyCanBeRebuilt()
    {
        var cache = new MemoizedFactory<int, object>(_ => new object());
        object before = cache.For(3);

        cache.Clear();
        object after = cache.For(3);

        await Assert.That(after).IsNotSameReferenceAs(before);
        await Assert.That(cache.Count).IsEqualTo(1);
    }
}
