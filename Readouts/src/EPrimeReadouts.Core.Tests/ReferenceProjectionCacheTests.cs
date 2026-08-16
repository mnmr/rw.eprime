using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class ReferenceProjectionCacheTests
{
    private sealed class Source
    {
        internal Source(string value) => Value = value;
        internal string Value { get; }
    }

    [Test]
    public async Task SameSourceReferenceReusesProjectionWithoutRebuilding()
    {
        int builds = 0;
        var cache = new ReferenceProjectionCache<Source, string>(
            source => { builds++; return source.Value; },
            StringComparer.Ordinal);
        var source = new Source("leather");

        string first = cache.Get(source);
        string second = cache.Get(source);

        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(builds).IsEqualTo(1);
    }

    [Test]
    public async Task EqualProjectionFromNewSourcePreservesProjectionIdentity()
    {
        var cache = new ReferenceProjectionCache<Source, string>(
            source => new string(source.Value.ToCharArray()),
            StringComparer.Ordinal);

        string first = cache.Get(new Source("leather"));
        string second = cache.Get(new Source("leather"));

        await Assert.That(second).IsSameReferenceAs(first);
    }

    [Test]
    public async Task ChangedProjectionFromNewSourceReplacesProjection()
    {
        var cache = new ReferenceProjectionCache<Source, string>(
            source => new string(source.Value.ToCharArray()),
            StringComparer.Ordinal);

        string first = cache.Get(new Source("leather"));
        string second = cache.Get(new Source("steel"));

        await Assert.That(second).IsNotSameReferenceAs(first);
        await Assert.That(second).IsEqualTo("steel");
    }

    [Test]
    public async Task ClearReleasesSourceAndProjection()
    {
        int builds = 0;
        var cache = new ReferenceProjectionCache<Source, string>(
            source => { builds++; return source.Value; },
            StringComparer.Ordinal);
        var source = new Source("leather");
        cache.Get(source);

        cache.Clear();
        cache.Get(source);

        await Assert.That(builds).IsEqualTo(2);
    }
}
