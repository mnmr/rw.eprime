using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// The unified signal tier enum. Classification behavior lives in
/// SkillSignalAggregatorTests; this only pins the rung ladder itself.
public class SignalBucketsTests
{
    [Test]
    public async Task TiersOrderAsSpecified()
    {
        // Rung order is load-bearing: rules compare buckets numerically, so
        // each NAME must sit on its exact rung (a value swap inverts rules).
        string ladder = string.Join(",", Enum.GetValues(typeof(SignalBucket))
            .Cast<SignalBucket>().OrderBy(x => (int)x).Select(x => $"{(int)x}:{x}"));
        await Assert.That(ladder)
            .IsEqualTo("0:Awful,1:Poor,2:Neutral,3:Strong,4:Great,5:Exceptional");
    }
}
