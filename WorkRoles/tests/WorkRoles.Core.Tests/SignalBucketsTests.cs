using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// The six-tier signal classifier: precedence between signal kinds, the
/// apathy override, and the neutral/poor split on bare skill level.
public class SignalBucketsTests
{
    [Test]
    public async Task TiersOrderAsSpecified()
    {
        // Rung order is load-bearing: rules compare buckets numerically.
        int[] values = Enum.GetValues(typeof(SignalBucket)).Cast<SignalBucket>()
            .Select(x => (int)x).ToArray();
        await Assert.That(values).IsEquivalentTo(new[] { 0, 1, 2, 3, 4, 5 });
    }

}
