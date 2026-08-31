namespace RimShared.Common.Tests.Caching;

public class DisplayEpochCacheTests
{
    [Test]
    public async Task UnchangedDisplayReusesThePublishedMeasurement()
    {
        var cache = new DisplayEpochCache<string, float>();
        var display = new DisplayEpoch(1920, 1080, 1f);

        cache.Observe(display);
        cache.Request("Steel");
        cache.TryTake(out string key);
        cache.Publish(key, 0.9f);

        cache.Observe(display);
        bool queuedAgain = cache.Request("Steel");

        await Assert.That(queuedAgain).IsFalse();
        await Assert.That(cache.TryGet("Steel", out float scale)).IsTrue();
        await Assert.That(scale).IsEqualTo(0.9f);
        await Assert.That(cache.TryTake(out _)).IsFalse();
    }

    [Test]
    public async Task UiScaleChangeQueuesEveryMeasuredIconOnce()
    {
        var cache = new DisplayEpochCache<string, float>();
        cache.Observe(new DisplayEpoch(1920, 1080, 1f));
        cache.Request("Steel");
        cache.TryTake(out string steel);
        cache.Publish(steel, 0.9f);
        cache.Request("Cloth");
        cache.TryTake(out string cloth);
        cache.Publish(cloth, 1.1f);

        cache.Observe(new DisplayEpoch(1920, 1080, 1.25f));
        bool duplicate = cache.Request("Steel");

        await Assert.That(duplicate).IsFalse();
        await Assert.That(cache.PendingCount).IsEqualTo(2);
        await Assert.That(cache.TryGet("Steel", out float stale)).IsTrue();
        await Assert.That(stale).IsEqualTo(0.9f);

        cache.TryTake(out string first);
        cache.Publish(first, 0.95f);
        cache.TryTake(out string second);
        cache.Publish(second, 1.05f);

        await Assert.That(cache.TryTake(out _)).IsFalse();
        await Assert.That(cache.IsCurrent("Steel")).IsTrue();
        await Assert.That(cache.IsCurrent("Cloth")).IsTrue();
    }

    [Test]
    public async Task ResolutionChangeQueuesAMeasuredIconForTheNewEpoch()
    {
        var cache = new DisplayEpochCache<string, float>();
        cache.Observe(new DisplayEpoch(1920, 1080, 1f));
        cache.Request("Steel");
        cache.TryTake(out string key);
        cache.Publish(key, 0.9f);

        cache.Observe(new DisplayEpoch(2560, 1440, 1f));

        await Assert.That(cache.IsCurrent("Steel")).IsFalse();
        await Assert.That(cache.PendingCount).IsEqualTo(1);
    }

    [Test]
    public async Task PendingNewIconSurvivesAnEpochChange()
    {
        var cache = new DisplayEpochCache<string, float>();
        cache.Observe(new DisplayEpoch(1920, 1080, 1f));
        cache.Request("Unmeasured");

        cache.Observe(new DisplayEpoch(1920, 1080, 1.25f));

        await Assert.That(cache.PendingCount).IsEqualTo(1);
        await Assert.That(cache.TryTake(out string key)).IsTrue();
        await Assert.That(key).IsEqualTo("Unmeasured");
    }
}
