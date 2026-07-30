using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class RevisionedCacheTests
{
    private sealed class BuildState
    {
        public int Builds;
    }

    [Test]
    public async Task ARevisionChangeRebuildsAnOtherwiseCachedValue()
    {
        var cache = new RevisionedCache<string, object, string>();
        var state = new BuildState();
        var firstRevision = new object();
        var secondRevision = new object();

        var first = cache.Get(
            "Steel", firstRevision, state,
            static s => "tip-" + ++s.Builds);
        var reused = cache.Get(
            "Steel", firstRevision, state,
            static s => "tip-" + ++s.Builds);
        var rebuilt = cache.Get(
            "Steel", secondRevision, state,
            static s => "tip-" + ++s.Builds);

        await Assert.That(reused).IsEqualTo(first);
        await Assert.That(rebuilt).IsEqualTo("tip-2");
        await Assert.That(state.Builds).IsEqualTo(2);
    }

    [Test]
    public async Task ClearReleasesEveryOwnedValue()
    {
        var cache = new RevisionedCache<string, int, object>();
        object first = cache.Get("a", 1, 0, static _ => new object());
        cache.Get("b", 1, 0, static _ => new object());

        cache.Clear();
        object rebuilt = cache.Get("a", 1, 0, static _ => new object());

        await Assert.That(cache.Count).IsEqualTo(1);
        await Assert.That(rebuilt).IsNotSameReferenceAs(first);
    }
}
