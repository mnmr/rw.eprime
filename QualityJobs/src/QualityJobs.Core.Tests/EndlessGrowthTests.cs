namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

/// With Endless Growth loaded skills reach 100 and quality rolls follow that
/// mod's curve and Legendary clamp. These tests flip the session-wide rule,
/// so they never run beside the vanilla-rule tests.
[NotInParallel]
public class EndlessGrowthTests
{
    private static CandidateFacts F(int id, int skill, bool inspired = false, int role = 0)
        => new(id, skill, inspired, role, true, true);

    private static IDisposable Endless()
    {
        SkillRules.EndlessGrowth = true;
        return new Restore();
    }

    private sealed class Restore : IDisposable
    {
        public void Dispose() => SkillRules.EndlessGrowth = false;
    }

    [Test]
    public async Task ThresholdsAcceptSkillsUpToOneHundred()
    {
        using var _ = Endless();
        await Assert.That(ConfigurationLimits.Skill(85)).IsEqualTo(85);
        await Assert.That(ConfigurationLimits.Skill(140)).IsEqualTo(100);
        await Assert.That(new ResumeCondition(60, false, false).MinSkill).IsEqualTo(60);
        // A skill-60 gate is a real gate: a level-59 worker fails it.
        await Assert.That(new ResumeCondition(60, false, false).IsSatisfiedBy(F(1, 59))).IsFalse();
        await Assert.That(new ResumeCondition(60, false, false).IsSatisfiedBy(F(1, 60))).IsTrue();
    }

    [Test]
    public async Task WithoutTheModThresholdsStillStopAtTwenty()
    {
        await Assert.That(ConfigurationLimits.Skill(85)).IsEqualTo(20);
        await Assert.That(new ResumeCondition(60, false, false).MinSkill).IsEqualTo(20);
    }

    [Test]
    public async Task CurveMatchesVanillaCentersBelowTwentyAndKeepsRising()
    {
        // Endless Growth's curve reproduces every vanilla center up to 20
        // (its points interpolate linearly through the vanilla table), then
        // keeps climbing to 6.0 at 50 and holds 10.0 from 100 on.
        double[] vanilla =
        {
            0.7, 1.1, 1.5, 1.8, 2.0, 2.2, 2.4, 2.6, 2.8, 2.95,
            3.1, 3.25, 3.4, 3.5, 3.6, 3.7, 3.8, 3.9, 4.0, 4.1, 4.2,
        };
        for (int skill = 0; skill <= 20; skill++)
            await Assert.That(QualityOdds.EndlessGrowthCenter(skill)).IsEqualTo(vanilla[skill]).Within(1e-9);
        await Assert.That(QualityOdds.EndlessGrowthCenter(25)).IsEqualTo(4.6).Within(1e-9);
        await Assert.That(QualityOdds.EndlessGrowthCenter(50)).IsEqualTo(6.0).Within(1e-9);
        await Assert.That(QualityOdds.EndlessGrowthCenter(100)).IsEqualTo(10.0).Within(1e-9);
        await Assert.That(QualityOdds.EndlessGrowthCenter(140)).IsEqualTo(10.0).Within(1e-9);
    }

    [Test]
    public async Task OddsFollowTheModsRollRule()
    {
        using var _ = Endless();
        double prev = -1;
        for (int skill = 0; skill <= 100; skill++)
        {
            double[] d = QualityOdds.Distribution(skill, false, 0);
            await Assert.That(Math.Abs(d.Sum() - 1.0)).IsLessThan(1e-9);
            double mean = 0;
            for (int q = 0; q < d.Length; q++) mean += q * d[q];
            await Assert.That(mean).IsGreaterThanOrEqualTo(prev);
            prev = mean;
        }
        // An uninspired master can roll Legendary directly (the mod raises
        // the clamp), which vanilla never allows.
        await Assert.That(QualityOdds.Distribution(20, false, 0)[(int)QualityLevel.Legendary]).IsGreaterThan(0.0);
        // The mod's own description: 50 is mostly Masterwork and beyond,
        // and at 100 practically everything is Legendary.
        double[] fifty = QualityOdds.Distribution(50, false, 0);
        await Assert.That(fifty[(int)QualityLevel.Masterwork] + fifty[(int)QualityLevel.Legendary]).IsGreaterThan(0.9);
        await Assert.That(QualityOdds.Distribution(100, false, 0)[(int)QualityLevel.Legendary]).IsGreaterThan(0.99);
        // Skills above the mod's ceiling predict like the ceiling.
        await Assert.That(QualityOdds.Distribution(140, false, 0)).IsEquivalentTo(QualityOdds.Distribution(100, false, 0));
    }

    [Test]
    public async Task EndlessGrowthTableMatchesAnalyticExpectedValue()
    {
        using var _ = Endless();
        // Lockstep guard like FinisherRankTests.TableMatchesAnalyticExpectedValue:
        // every EvMilliEndless entry equals round(1000 × EV) of the analytic
        // distribution under the mod's rule. On mismatch the failure output
        // prints the full expected literal list.
        // EvMilliAt exposes shifts 0..3; the bonus columns 4..6 are reached
        // through RankMilliOf with a whole-level quality bonus on top of a
        // saturated role shift, exactly as a VSE expert would.
        var expected = new System.Text.StringBuilder();
        var actual = new System.Text.StringBuilder();
        for (int skill = 0; skill <= 100; skill++)
        {
            for (int shift = 0; shift <= 6; shift++)
            {
                double[] d = QualityOdds.Distribution(skill, false, shift);
                double ev = 0;
                for (int q = 0; q < 7; q++) ev += q * d[q];
                expected.Append((int)Math.Round(ev * 1000.0)).Append(", ");
                int value = shift <= 3
                    ? FinisherRank.EvMilliAt(skill, shift)
                    : FinisherRank.RankMilliOf(new CandidateFacts(1, skill, false, 3, true, true,
                        qualityBonusMilli: (shift - 3) * QualityBonus.Scale));
                actual.Append(value).Append(", ");
            }
            expected.Append('\n');
            actual.Append('\n');
        }
        await Assert.That(actual.ToString()).IsEqualTo(expected.ToString());
    }

    [Test]
    public async Task HigherModdedSkillOutranksAVanillaMaster()
    {
        using var _ = Endless();
        await Assert.That(FinisherRank.Outranks(F(1, 40), F(2, 20))).IsTrue();
        await Assert.That(FinisherRank.Outranks(F(2, 20), F(1, 40))).IsFalse();
        // Still ranked by expected quality, not raw skill: an inspired 20
        // (two levels up) beats a plain 25.
        await Assert.That(FinisherRank.Outranks(F(1, 20, inspired: true), F(2, 25))).IsTrue();
        await Assert.That(FinisherRank.EvMilliAt(140, 0)).IsEqualTo(FinisherRank.EvMilliAt(100, 0));
    }
}
