namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class FinisherSelectorTests
{
    private static readonly ResumeCondition AnySkilled = new(10, false, false);

    private static CandidateFacts F(int id, int skill, bool inspired = false,
        int role = 0, bool work = true, bool recipeSkill = true, int xpMilli = 0)
        => new(id, skill, inspired, role, work, recipeSkill, xpMilli);

    [Test]
    public async Task InspirationShiftOutweighsEightLevelSkillGap()
    {
        // Expected-quality outcome at these values (inspired 12 = 4953 milli-EV
        // vs plain 20 = 3672), not a categorical inspired-first rule — see
        // InspiredNoviceLosesToUninspiredMaster for the counter-case.
        var best = FinisherSelector.SelectBest(
            new[] { F(1, 20), F(2, 12, inspired: true) }, AnySkilled);
        await Assert.That(best).IsEqualTo(2);
    }

    [Test]
    public async Task RoleOffsetShiftOutweighsEightLevelSkillGap()
    {
        // Expected-quality outcome at these values (role+1 at 12 = 3965 milli-EV
        // vs plain 20 = 3672), not a categorical role-first rule.
        var best = FinisherSelector.SelectBest(
            new[] { F(1, 20), F(2, 12, role: 1) }, AnySkilled);
        await Assert.That(best).IsEqualTo(2);
    }

    [Test]
    public async Task SkillThenLowestIdTieBreak()
    {
        // Ordering is rank → XP → id; XP is equal (0) here, so the id
        // fallback decides. XpTieBreakBeatsLowerId covers the XP layer.
        var best = FinisherSelector.SelectBest(
            new[] { F(9, 15), F(3, 15), F(5, 14) }, AnySkilled);
        await Assert.That(best).IsEqualTo(3);
    }

    [Test]
    public async Task IncapableAndUnqualifiedAreFilteredOut()
    {
        var best = FinisherSelector.SelectBest(new[]
        {
            F(1, 20, work: false),          // work type disabled
            F(2, 20, recipeSkill: false),   // fails recipe skill requirements
            F(3, 9),                        // fails condition (min 10)
        }, AnySkilled);
        await Assert.That(best).IsEqualTo(FinisherSelector.None);
    }

    [Test]
    public async Task RelaxedSelectionIgnoresCondition()
    {
        // Used by the disable restore routine (spec §12): best capable pawn
        // regardless of the resume condition.
        var best = FinisherSelector.SelectBestCapable(new[] { F(1, 4), F(2, 9) });
        await Assert.That(best).IsEqualTo(2);
    }

    [Test]
    public async Task EmptyCandidateListReturnsNone()
    {
        var best = FinisherSelector.SelectBest(Array.Empty<CandidateFacts>(), AnySkilled);
        await Assert.That(best).IsEqualTo(FinisherSelector.None);
    }

    [Test]
    public async Task RelaxedSelectionStillFiltersCapability()
    {
        // High-skill pawn with work type disabled must lose to the capable lower-skill pawn.
        var best = FinisherSelector.SelectBestCapable(new[] { F(1, 20, work: false), F(2, 5) });
        await Assert.That(best).IsEqualTo(2);
    }

    [Test]
    public async Task InspiredTieFallsThroughToSkill()
    {
        // Both candidates are inspired (equal shift), and expected quality is
        // monotonic in skill at equal shift — the higher-skill pawn must win.
        var best = FinisherSelector.SelectBest(
            new[] { F(4, 12, inspired: true), F(2, 18, inspired: true) }, AnySkilled);
        await Assert.That(best).IsEqualTo(2);
    }

    [Test]
    public async Task InspiredNoviceLosesToUninspiredMaster()
    {
        // Auto-best spec §2.5: expected-quality ranking replaces inspired-first.
        // Under the old lexicographic ordering the inspired pawn always won.
        var best = FinisherSelector.SelectBest(
            new[] { F(1, 20), F(2, 3, inspired: true) },
            new ResumeCondition(0, false, false));
        await Assert.That(best).IsEqualTo(1);
    }

    [Test]
    public async Task XpTieBreakBeatsLowerId()
    {
        var best = FinisherSelector.SelectBest(
            new[] { F(1, 15, xpMilli: 100), F(9, 15, xpMilli: 900) }, AnySkilled);
        await Assert.That(best).IsEqualTo(9);
    }

    // ---- eligible-set collection (status display) --------------------------

    [Test]
    public async Task CollectEligibleReturnsAllSatisfyingBestFirst()
    {
        // Same ordering as SelectBest: rank (skill 15 ties), then XP (equal),
        // then lowest id — so 3 before 9, and 14-skill 5 last. 7 fails MinSkill.
        var results = new List<CandidateFacts>();
        FinisherSelector.CollectEligible(
            new[] { F(9, 15), F(5, 14), F(3, 15), F(7, 9) }, AnySkilled, results);
        await Assert.That(results.Count).IsEqualTo(3);
        await Assert.That(results[0].Id).IsEqualTo(3);
        await Assert.That(results[1].Id).IsEqualTo(9);
        await Assert.That(results[2].Id).IsEqualTo(5);
    }

    [Test]
    public async Task CollectEligibleFiltersIncapableAndUnqualified()
    {
        // Mirrors IncapableAndUnqualifiedAreFilteredOut: the collection must
        // agree with SelectBest that nobody qualifies.
        var results = new List<CandidateFacts>();
        FinisherSelector.CollectEligible(new[]
        {
            F(1, 20, work: false),          // work type disabled
            F(2, 20, recipeSkill: false),   // fails recipe skill requirements
            F(3, 9),                        // fails condition (min 10)
        }, AnySkilled, results);
        await Assert.That(results.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CollectEligibleClearsPriorResults()
    {
        var results = new List<CandidateFacts> { F(42, 20) };
        FinisherSelector.CollectEligible(
            Array.Empty<CandidateFacts>(), AnySkilled, results);
        await Assert.That(results.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CollectAutoEligibleAdmitsExactTiesOnly()
    {
        // Gate passers are exactly tied on (rank, XP); ordering falls through
        // to lowest id. The 14-skill pawn is outranked and must not appear.
        var tied = new[] { F(2, 15, xpMilli: 500), F(1, 15, xpMilli: 500), F(3, 14) };
        var results = new List<CandidateFacts>();
        FinisherSelector.CollectAutoEligible(
            tied, tied, new ResumeCondition(0, false, false), results);
        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results[0].Id).IsEqualTo(1);
        await Assert.That(results[1].Id).IsEqualTo(2);
    }

    [Test]
    public async Task CollectAutoEligibleRespectsColonyPoolCompetitor()
    {
        // The only dispatchable pawn is outranked by an away pool member: the
        // item waits (auto spec §2.4), so the eligible set is empty.
        var results = new List<CandidateFacts> { F(42, 20) };
        FinisherSelector.CollectAutoEligible(
            new[] { F(1, 10) },
            new[] { F(1, 10), F(2, 20) },
            new ResumeCondition(0, false, false), results);
        await Assert.That(results.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CollectAutoEligibleFilterExcludesPoolCompetitor()
    {
        // The uninspired 20-skill pool member fails the inspired filter, so it
        // cannot outrank the inspired dispatchable pawn (auto spec §2.2).
        var results = new List<CandidateFacts>();
        FinisherSelector.CollectAutoEligible(
            new[] { F(1, 5, inspired: true) },
            new[] { F(1, 5, inspired: true), F(2, 20) },
            new ResumeCondition(0, true, false), results);
        await Assert.That(results.Count).IsEqualTo(1);
        await Assert.That(results[0].Id).IsEqualTo(1);
    }
}
