namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class AutoGateTests
{
    private static readonly ResumeCondition NoFilters = new(0, false, false);
    private static readonly ResumeCondition InspiredOnly = new(0, true, false);

    private static CandidateFacts F(int id, int skill, bool inspired = false,
        int role = 0, bool work = true, bool recipeSkill = true, int xpMilli = 0)
        => new(id, skill, inspired, role, work, recipeSkill, xpMilli);

    [Test]
    public async Task TopRankedWorkerPasses()
    {
        var worker = F(1, 18);
        var pool = new[] { worker, F(2, 15), F(3, 10) };
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(worker, pool, NoFilters)).IsTrue();
    }

    [Test]
    public async Task OutrankedWorkerPauses()
    {
        var worker = F(1, 15);
        var pool = new[] { worker, F(2, 18) };
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(worker, pool, NoFilters)).IsFalse();
    }

    [Test]
    public async Task UnavailableTopStillBlocks()
    {
        // Pool composition is game-side: a downed/off-map best is still IN the
        // pool (auto spec §2.2). Core must honor it as a blocker.
        var worker = F(1, 15);
        var awayBest = F(2, 20);
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(
            worker, new[] { worker, awayBest }, NoFilters)).IsFalse();
    }

    [Test]
    public async Task FilteredOutOutrankerDoesNotBlock()
    {
        // requireInspired: the uninspired skill-20 is not in the eligible pool,
        // so the inspired skill-10 worker is the best and passes.
        var worker = F(1, 10, inspired: true);
        var pool = new[] { worker, F(2, 20) };
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(worker, pool, InspiredOnly)).IsTrue();
    }

    [Test]
    public async Task WorkerFailingFiltersPauses()
    {
        var worker = F(1, 20); // not inspired
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(
            worker, new[] { worker }, InspiredOnly)).IsFalse();
    }

    [Test]
    public async Task WorkTypeDisabledOutrankerDoesNotBlock()
    {
        var worker = F(1, 15);
        var pool = new[] { worker, F(2, 20, work: false) };
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(worker, pool, NoFilters)).IsTrue();
    }

    [Test]
    public async Task ExactTieAdmitsBothWorkers()
    {
        var a = F(1, 15, xpMilli: 250);
        var b = F(2, 15, xpMilli: 250);
        var pool = new[] { a, b };
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(a, pool, NoFilters)).IsTrue();
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(b, pool, NoFilters)).IsTrue();
    }

    [Test]
    public async Task XpTieBreakBlocksLowerXpWorker()
    {
        var worker = F(1, 15, xpMilli: 100);
        var pool = new[] { worker, F(2, 15, xpMilli: 900) };
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(worker, pool, NoFilters)).IsFalse();
    }

    [Test]
    public async Task SelectAutoBestPicksDispatchableBest()
    {
        var onMap = new[] { F(1, 18), F(2, 15) };
        var pool = new[] { F(1, 18), F(2, 15), F(3, 10) };
        await Assert.That(FinisherSelector.SelectAutoBest(onMap, pool, NoFilters)).IsEqualTo(1);
    }

    [Test]
    public async Task SelectAutoBestReturnsNoneWhenBestIsAway()
    {
        // Colony best (id 3, skill 20) is not dispatchable → nobody is dispatched;
        // the item waits (auto spec §2.4).
        var onMap = new[] { F(1, 18), F(2, 15) };
        var pool = new[] { F(1, 18), F(2, 15), F(3, 20) };
        await Assert.That(FinisherSelector.SelectAutoBest(onMap, pool, NoFilters))
            .IsEqualTo(FinisherSelector.None);
    }

    [Test]
    public async Task SelectAutoBestTieDispatchesLowestId()
    {
        var tied = new[] { F(5, 15, xpMilli: 250), F(2, 15, xpMilli: 250) };
        await Assert.That(FinisherSelector.SelectAutoBest(tied, tied, NoFilters)).IsEqualTo(2);
    }

    [Test]
    public async Task SelectBestOfPoolRanksWholePool()
    {
        var pool = new[] { F(1, 12), F(2, 15, inspired: true), F(3, 20) };
        // Inspired skill 15 has higher expected quality than plain skill 20.
        await Assert.That(FinisherSelector.SelectBestOfPool(pool, NoFilters)).IsEqualTo(2);
    }

    [Test]
    public async Task SelectBestOfPoolEmptyReturnsNone()
    {
        await Assert.That(FinisherSelector.SelectBestOfPool(
            Array.Empty<CandidateFacts>(), NoFilters)).IsEqualTo(FinisherSelector.None);
    }

    [Test]
    public async Task DecideAutoUnmanagedCompletes()
    {
        var worker = F(1, 3);
        var pool = new[] { worker, F(2, 20) };
        await Assert.That(GateDecision.DecideAuto(false, false, worker, pool, NoFilters))
            .IsEqualTo(GateOutcome.Complete);
    }

    [Test]
    public async Task DecideAutoDebugCompletedCompletes()
    {
        var worker = F(1, 3);
        var pool = new[] { worker, F(2, 20) };
        await Assert.That(GateDecision.DecideAuto(true, true, worker, pool, NoFilters))
            .IsEqualTo(GateOutcome.Complete);
    }

    [Test]
    public async Task DecideAutoOutrankedWorkerPauses()
    {
        var worker = F(1, 3);
        var pool = new[] { worker, F(2, 20) };
        await Assert.That(GateDecision.DecideAuto(true, false, worker, pool, NoFilters))
            .IsEqualTo(GateOutcome.Pause);
    }

    [Test]
    public async Task SpecialistFilterRestrictsPool()
    {
        var specialistOnly = new ResumeCondition(0, false, true);
        // Non-specialist skill-20 is filtered out of the pool, so the
        // specialist skill-10 worker is the best and passes...
        var worker = F(1, 10, role: 1);
        var pool = new[] { worker, F(2, 20) };
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(worker, pool, specialistOnly)).IsTrue();
        // ...while a non-specialist worker fails the filter outright.
        var nonSpecialist = F(3, 20);
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(
            nonSpecialist, new[] { nonSpecialist }, specialistOnly)).IsFalse();
    }

    [Test]
    public async Task RecipeSkillFailingOutrankerDoesNotBlock()
    {
        var worker = F(1, 15);
        var pool = new[] { worker, F(2, 20, recipeSkill: false) };
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(worker, pool, NoFilters)).IsTrue();
    }

    [Test]
    public async Task MinSkillIsIgnoredInAutoMode()
    {
        // Auto spec §2.2: the dynamic threshold replaces MinSkill entirely.
        // A worker below the condition's MinSkill still passes when top-ranked.
        var highFloor = new ResumeCondition(15, false, false);
        var worker = F(1, 10);
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(
            worker, new[] { worker }, highFloor)).IsTrue();
    }

    [Test]
    public async Task WorkerAbsentFromPoolIsStillGatedAgainstIt()
    {
        // The caller passes worker facts separately; pool membership is not
        // required (e.g. a mech worker vs the colonist pool, Task 5).
        var worker = F(9, 15);
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(
            worker, new[] { F(2, 12) }, NoFilters)).IsTrue();
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(
            worker, new[] { F(2, 18) }, NoFilters)).IsFalse();
    }

    [Test]
    public async Task DispatchGateAgreementInvariant()
    {
        // Whatever SelectAutoBest picks must pass the gate for the same pool.
        var pool = new[]
        {
            F(1, 12), F(2, 15, inspired: true), F(3, 20), F(4, 15, xpMilli: 500),
            F(5, 20, work: false), F(6, 3, inspired: true),
        };
        int picked = FinisherSelector.SelectAutoBest(pool, pool, NoFilters);
        await Assert.That(picked).IsNotEqualTo(FinisherSelector.None);
        CandidateFacts pickedFacts = default;
        foreach (var c in pool) if (c.Id == picked) pickedFacts = c;
        await Assert.That(FinisherSelector.WorkerPassesAutoGate(pickedFacts, pool, NoFilters)).IsTrue();
    }
}
