using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class PoolListHeightCacheTests
{
    [Test]
    public async Task PoolVersionChangeUsesCurrentRowCountWithoutRemeasuringCaption()
    {
        var cache = new PoolListHeightCache(
            headerHeight: 28f,
            captionGap: 4f,
            rowHeight: 26f,
            maxVisibleRows: 8,
            footerHeight: 30f);
        int captionMeasurements = 0;

        float beforeAdd = cache.GetDesiredHeight(
            owner: cache,
            poolsVersion: 3,
            metricRevision: 1,
            rowCount: 1,
            folded: false,
            availableWidth: 200f,
            caption: "Pool help",
            measureCaption: (_, _) =>
            {
                captionMeasurements++;
                return 12f;
            });
        float afterAdd = cache.GetDesiredHeight(
            owner: cache,
            poolsVersion: 4,
            metricRevision: 1,
            rowCount: 2,
            folded: false,
            availableWidth: 200f,
            caption: "Pool help",
            measureCaption: (_, _) =>
            {
                captionMeasurements++;
                return 12f;
            });

        await Assert.That(beforeAdd).IsEqualTo(100f);
        await Assert.That(afterAdd).IsEqualTo(126f);
        await Assert.That(captionMeasurements).IsEqualTo(1);
    }

    [Test]
    public async Task MetricRevisionChangeRemeasuresCaptionAndUpdatesDesiredHeight()
    {
        var cache = new PoolListHeightCache(
            headerHeight: 28f,
            captionGap: 4f,
            rowHeight: 26f,
            maxVisibleRows: 8,
            footerHeight: 30f);
        float measuredHeight = 12f;
        int captionMeasurements = 0;

        float initial = cache.GetDesiredHeight(
            owner: cache,
            poolsVersion: 3,
            metricRevision: 1,
            rowCount: 1,
            folded: false,
            availableWidth: 200f,
            caption: "Pool help",
            measureCaption: Measure);
        measuredHeight = 20f;
        float rescaled = cache.GetDesiredHeight(
            owner: cache,
            poolsVersion: 3,
            metricRevision: 2,
            rowCount: 1,
            folded: false,
            availableWidth: 200f,
            caption: "Pool help",
            measureCaption: Measure);

        await Assert.That(initial).IsEqualTo(100f);
        await Assert.That(rescaled).IsEqualTo(108f);
        await Assert.That(captionMeasurements).IsEqualTo(2);

        float Measure(string _, float __)
        {
            captionMeasurements++;
            return measuredHeight;
        }
    }

    [Test]
    public async Task DifferentOwnerCannotReuseEqualRevisionValues()
    {
        var cache = new PoolListHeightCache(28f, 4f, 26f, 8, 30f);
        var worldA = new object();
        var worldB = new object();

        float oneRow = cache.GetDesiredHeight(worldA, 3, 1, 1, false, 200f,
            "Pool help", static (_, _) => 12f);
        float threeRows = cache.GetDesiredHeight(worldB, 3, 1, 3, false, 200f,
            "Pool help", static (_, _) => 12f);

        await Assert.That(oneRow).IsEqualTo(100f);
        await Assert.That(threeRows).IsEqualTo(152f);
    }

    [Test]
    public async Task ChangedCaptionRemeasuresWithoutMetricRevisionChange()
    {
        var cache = new PoolListHeightCache(28f, 4f, 26f, 8, 30f);
        int measurements = 0;

        cache.GetDesiredHeight(cache, 3, 1, 1, false, 200f, "Short",
            (_, _) => { measurements++; return 12f; });
        cache.GetDesiredHeight(cache, 3, 1, 1, false, 200f, "Long localized caption",
            (_, _) => { measurements++; return 20f; });

        await Assert.That(measurements).IsEqualTo(2);
    }

    [Test]
    public async Task WidthChangeRemeasuresCaptionWithoutChangingOtherDependencies()
    {
        var cache = new PoolListHeightCache(28f, 4f, 26f, 8, 30f);
        int measurements = 0;

        float wide = cache.GetDesiredHeight(cache, 3, 1, 1, false, 240f,
            "Pool help", (_, width) => { measurements++; return width > 200f ? 12f : 24f; });
        float narrow = cache.GetDesiredHeight(cache, 3, 1, 1, false, 160f,
            "Pool help", (_, width) => { measurements++; return width > 200f ? 12f : 24f; });

        await Assert.That(wide).IsEqualTo(100f);
        await Assert.That(narrow).IsEqualTo(112f);
        await Assert.That(measurements).IsEqualTo(2);
    }
}
