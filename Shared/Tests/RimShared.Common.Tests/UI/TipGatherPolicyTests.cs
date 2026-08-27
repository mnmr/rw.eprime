namespace RimShared.Common.Tests;

public class TipGatherPolicyTests
{
    [Test]
    [Arguments(TipRefresh.Pinned)]
    [Arguments(TipRefresh.PerSession)]
    public async Task MissingTextAlwaysGathers(TipRefresh refresh)
    {
        await Assert.That(TipGatherPolicy.ShouldGather(refresh, hasText: false, frame: 100, lastObservedFrame: 100)).IsTrue();
    }

    [Test]
    public async Task PinnedTextNeverRegathers()
    {
        await Assert.That(TipGatherPolicy.ShouldGather(TipRefresh.Pinned, hasText: true, frame: 5000, lastObservedFrame: 10)).IsFalse();
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task SessionContinuityReusesGatheredText(int frameGap)
    {
        await Assert.That(TipGatherPolicy.ShouldGather(TipRefresh.PerSession, hasText: true, frame: 100 + frameGap, lastObservedFrame: 100)).IsFalse();
    }

    [Test]
    public async Task SessionRegathersAfterADisplayGap()
    {
        await Assert.That(TipGatherPolicy.ShouldGather(TipRefresh.PerSession, hasText: true, frame: 102, lastObservedFrame: 100)).IsTrue();
    }
}
