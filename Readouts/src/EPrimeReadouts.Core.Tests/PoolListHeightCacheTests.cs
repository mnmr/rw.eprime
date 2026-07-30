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
            poolsVersion: 3,
            metricRevision: 1,
            rowCount: 1,
            folded: false,
            availableWidth: 200f,
            measureCaption: _ =>
            {
                captionMeasurements++;
                return 12f;
            });
        float afterAdd = cache.GetDesiredHeight(
            poolsVersion: 4,
            metricRevision: 1,
            rowCount: 2,
            folded: false,
            availableWidth: 200f,
            measureCaption: _ =>
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
            poolsVersion: 3,
            metricRevision: 1,
            rowCount: 1,
            folded: false,
            availableWidth: 200f,
            measureCaption: Measure);
        measuredHeight = 20f;
        float rescaled = cache.GetDesiredHeight(
            poolsVersion: 3,
            metricRevision: 2,
            rowCount: 1,
            folded: false,
            availableWidth: 200f,
            measureCaption: Measure);

        await Assert.That(initial).IsEqualTo(100f);
        await Assert.That(rescaled).IsEqualTo(108f);
        await Assert.That(captionMeasurements).IsEqualTo(2);

        float Measure(float _)
        {
            captionMeasurements++;
            return measuredHeight;
        }
    }
}
