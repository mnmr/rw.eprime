namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class GateDecisionTests
{
    private static readonly ResumeCondition Skilled15 = new(15, false, false);

    private static CandidateFacts Worker(int skill, bool inspired = false)
        => new(1, skill, inspired, 0, true, true);

    [Test]
    public async Task UnmanagedBillCompletes()
    {
        var d = GateDecision.Decide(billManaged: false, debugCompleted: false,
            Worker(0), Skilled15);
        await Assert.That(d).IsEqualTo(GateOutcome.Complete);
    }

    [Test]
    public async Task DebugCompletedBypassesGate()
    {
        var d = GateDecision.Decide(true, debugCompleted: true, Worker(0), Skilled15);
        await Assert.That(d).IsEqualTo(GateOutcome.Complete);
    }

    [Test]
    public async Task QualifyingWorkerSelfFinishes()
    {
        var d = GateDecision.Decide(true, false, Worker(18), Skilled15);
        await Assert.That(d).IsEqualTo(GateOutcome.Complete);
    }

    [Test]
    public async Task UnqualifiedWorkerPauses()
    {
        var d = GateDecision.Decide(true, false, Worker(14), Skilled15);
        await Assert.That(d).IsEqualTo(GateOutcome.Pause);
    }

    [Test]
    public async Task DispatchedFinisherWhoLostInspirationRePauses()
    {
        var needsInspiration = new ResumeCondition(0, true, false);
        var d = GateDecision.Decide(true, false, Worker(20, inspired: false), needsInspiration);
        await Assert.That(d).IsEqualTo(GateOutcome.Pause);
    }
}
