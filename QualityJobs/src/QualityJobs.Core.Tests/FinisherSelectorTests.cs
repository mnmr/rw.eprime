namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class FinisherSelectorTests
{
    private static readonly ResumeCondition AnySkilled = new(10, false, false);

    private static CandidateFacts F(int id, int skill, bool inspired = false,
        int role = 0, bool work = true, bool recipeSkill = true)
        => new(id, skill, inspired, role, work, recipeSkill);

    [Test]
    public async Task PicksInspiredOverHigherSkill()
    {
        var best = FinisherSelector.SelectBest(
            new[] { F(1, 20), F(2, 12, inspired: true) }, AnySkilled);
        await Assert.That(best).IsEqualTo(2);
    }

    [Test]
    public async Task RoleOffsetBeatsSkill()
    {
        var best = FinisherSelector.SelectBest(
            new[] { F(1, 20), F(2, 12, role: 1) }, AnySkilled);
        await Assert.That(best).IsEqualTo(2);
    }

    [Test]
    public async Task SkillThenLowestIdTieBreak()
    {
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
        // Both candidates are inspired, so inspiration does not differentiate them;
        // the higher-skill pawn must win.
        var best = FinisherSelector.SelectBest(
            new[] { F(4, 12, inspired: true), F(2, 18, inspired: true) }, AnySkilled);
        await Assert.That(best).IsEqualTo(2);
    }
}
