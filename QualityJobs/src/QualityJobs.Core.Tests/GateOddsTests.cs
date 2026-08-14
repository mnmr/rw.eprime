namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

/// A configured gate implies a fixed quality distribution, and therefore a
/// fixed expected cost for a quality target. No live pawn is involved: the
/// gate is the prediction.
public class GateOddsTests
{
    private static ResumeCondition Gate(
        int minSkill, bool inspired = false, bool specialist = false)
        => new(minSkill, inspired, specialist);

    [Test]
    public async Task GateDistributionMatchesTheRawOddsForItsSettings()
    {
        double[] expected = QualityOdds.Distribution(15, inspired: false, roleOffset: 0);
        double[] actual = GateOdds.DistributionFor(Gate(15));

        await Assert.That(actual.Length).IsEqualTo(expected.Length);
        for (int q = 0; q < expected.Length; q++)
            await Assert.That(actual[q]).IsEqualTo(expected[q]);
    }

    [Test]
    public async Task RequiringInspirationShiftsTheDistributionUp()
    {
        double[] expected = QualityOdds.Distribution(15, inspired: true, roleOffset: 0);
        double[] actual = GateOdds.DistributionFor(Gate(15, inspired: true));

        for (int q = 0; q < expected.Length; q++)
            await Assert.That(actual[q]).IsEqualTo(expected[q]);
    }

    [Test]
    public async Task RequiringASpecialistAppliesOneLevelOfRoleOffset()
    {
        double[] expected = QualityOdds.Distribution(15, inspired: false,
            roleOffset: GateOdds.SpecialistRoleOffset);
        double[] actual = GateOdds.DistributionFor(Gate(15, specialist: true));

        for (int q = 0; q < expected.Length; q++)
            await Assert.That(actual[q]).IsEqualTo(expected[q]);
    }

    [Test]
    public async Task NoQualityTargetCostsOneAttempt()
    {
        await Assert.That(GateOdds.AttemptsFor(Gate(15), 0)).IsEqualTo(1f);
    }

    [Test]
    public async Task AnEasyTargetAtAHighGateCostsAboutOneAttempt()
    {
        float attempts = GateOdds.AttemptsFor(Gate(20), (int)QualityLevel.Normal);

        await Assert.That(attempts).IsGreaterThanOrEqualTo(1f);
        await Assert.That(attempts).IsLessThan(1.2f);
    }

    [Test]
    public async Task AnUnreachableTargetCostsTheCappedEstimate()
    {
        // An uninspired gate can never roll Legendary, so the target is
        // unreachable and the estimate saturates rather than diverging.
        await Assert.That(GateOdds.AttemptsFor(Gate(20), (int)QualityLevel.Legendary))
            .IsEqualTo(ExpectedAttempts.Max);
    }

    [Test]
    public async Task RequiringInspirationMakesLegendaryReachable()
    {
        float attempts = GateOdds.AttemptsFor(
            Gate(20, inspired: true), (int)QualityLevel.Legendary);

        await Assert.That(attempts).IsGreaterThanOrEqualTo(1f);
        await Assert.That(attempts).IsLessThan(ExpectedAttempts.Max);
    }

    [Test]
    public async Task RequiringASpecialistMakesLegendaryReachable()
    {
        float attempts = GateOdds.AttemptsFor(
            Gate(20, specialist: true), (int)QualityLevel.Legendary);

        await Assert.That(attempts).IsLessThan(ExpectedAttempts.Max);
    }

    [Test]
    public async Task ARaisedGateNeverCostsMoreAttemptsForTheSameTarget()
    {
        float previous = float.MaxValue;
        for (int skill = 0; skill <= 20; skill++)
        {
            float attempts = GateOdds.AttemptsFor(
                Gate(skill), (int)QualityLevel.Excellent);
            await Assert.That(attempts).IsLessThanOrEqualTo(previous);
            previous = attempts;
        }
    }

    [Test]
    public async Task AHigherTargetNeverCostsFewerAttemptsAtTheSameGate()
    {
        float previous = 0f;
        for (int target = 0; target <= 6; target++)
        {
            float attempts = GateOdds.AttemptsFor(Gate(12), target);
            await Assert.That(attempts).IsGreaterThanOrEqualTo(previous);
            previous = attempts;
        }
    }

    [Test]
    public async Task TheAnswerDependsOnlyOnTheGateAndTarget()
    {
        // Two identically configured gates must predict identically — the
        // prediction is a property of the configuration, not of the colony.
        await Assert.That(GateOdds.AttemptsFor(Gate(14, specialist: true), 5))
            .IsEqualTo(GateOdds.AttemptsFor(Gate(14, specialist: true), 5));
    }

    [Test]
    public async Task AGateClampsItsSkillLikeTheConditionDoes()
    {
        // ResumeCondition clamps to 0..20; the odds must follow that, not throw.
        await Assert.That(GateOdds.AttemptsFor(Gate(99), (int)QualityLevel.Good))
            .IsEqualTo(GateOdds.AttemptsFor(Gate(20), (int)QualityLevel.Good));
        await Assert.That(GateOdds.AttemptsFor(Gate(-5), (int)QualityLevel.Good))
            .IsEqualTo(GateOdds.AttemptsFor(Gate(0), (int)QualityLevel.Good));
    }
}
