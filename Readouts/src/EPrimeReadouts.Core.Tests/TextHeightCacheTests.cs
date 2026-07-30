using EPrimeReadouts.Core;

namespace EPrimeReadouts.Core.Tests;

public class TextHeightCacheTests
{
    private sealed class MeasureState
    {
        public int Measurements;
        public float Height;
    }

    [Test]
    public async Task HeightIsMeasuredOnlyWhenTextWidthOrRevisionChanges()
    {
        var cache = new TextHeightCache();
        var state = new MeasureState { Height = 12f };

        float first = cache.Get("Pool help", 0, 200f, 1, state, Measure);
        state.Height = 99f;
        float reused = cache.Get("Pool help", 0, 200f, 1, state, Measure);
        float resized = cache.Get("Pool help", 0, 240f, 1, state, Measure);
        float rescaled = cache.Get("Pool help", 0, 240f, 2, state, Measure);

        await Assert.That(first).IsEqualTo(12f);
        await Assert.That(reused).IsEqualTo(12f);
        await Assert.That(resized).IsEqualTo(99f);
        await Assert.That(rescaled).IsEqualTo(99f);
        await Assert.That(state.Measurements).IsEqualTo(3);

        static float Measure(MeasureState value)
        {
            value.Measurements++;
            return value.Height;
        }
    }

    [Test]
    public async Task FontIsPartOfTheMeasurementIdentity()
    {
        var cache = new TextHeightCache();
        var state = new MeasureState { Height = 12f };

        float tiny = cache.Get("Pool help", font: 0, width: 200f, revision: 1,
            state: state, measure: Measure);
        state.Height = 18f;
        float small = cache.Get("Pool help", font: 1, width: 200f, revision: 1,
            state: state, measure: Measure);

        await Assert.That(tiny).IsEqualTo(12f);
        await Assert.That(small).IsEqualTo(18f);
        await Assert.That(state.Measurements).IsEqualTo(2);

        static float Measure(MeasureState value)
        {
            value.Measurements++;
            return value.Height;
        }
    }
}
