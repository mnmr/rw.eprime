namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class ExpectedAttemptsTests
{
    private static double[] Uniform()
    {
        var d = new double[7];
        for (int i = 0; i < 7; i++) d[i] = 1.0 / 7.0;
        return d;
    }

    [Test]
    public async Task NoTargetCostsOneAttempt()
    {
        await Assert.That(ExpectedAttempts.For(Uniform(), 0)).IsEqualTo(1f);
    }

    [Test]
    public async Task NegativeTargetCostsOneAttempt()
    {
        await Assert.That(ExpectedAttempts.For(Uniform(), -3)).IsEqualTo(1f);
    }

    [Test]
    public async Task NullDistributionCostsOneAttempt()
    {
        await Assert.That(ExpectedAttempts.For(null, 5)).IsEqualTo(1f);
    }

    [Test]
    public async Task ShortDistributionCostsOneAttempt()
    {
        await Assert.That(ExpectedAttempts.For(new double[3], 2)).IsEqualTo(1f);
    }

    [Test]
    public async Task CertainSuccessCostsOneAttempt()
    {
        var d = new double[7];
        d[(int)QualityLevel.Masterwork] = 1.0;
        await Assert.That(ExpectedAttempts.For(d, (int)QualityLevel.Normal)).IsEqualTo(1f);
    }

    [Test]
    public async Task ReciprocalOfSuccessProbability()
    {
        // 25% of the mass sits at or above Excellent.
        var d = new double[7];
        d[(int)QualityLevel.Normal] = 0.75;
        d[(int)QualityLevel.Excellent] = 0.25;
        float attempts = ExpectedAttempts.For(d, (int)QualityLevel.Excellent);
        await Assert.That(Math.Abs(attempts - 4f)).IsLessThan(1e-4f);
    }

    [Test]
    public async Task SumsEveryTierAtOrAboveTheTarget()
    {
        var d = new double[7];
        d[(int)QualityLevel.Poor] = 0.5;
        d[(int)QualityLevel.Masterwork] = 0.3;
        d[(int)QualityLevel.Legendary] = 0.2;
        float attempts = ExpectedAttempts.For(d, (int)QualityLevel.Masterwork);
        await Assert.That(Math.Abs(attempts - 2f)).IsLessThan(1e-4f);
    }

    [Test]
    public async Task ImpossibleTargetClampsToTheCap()
    {
        var d = new double[7];
        d[(int)QualityLevel.Normal] = 1.0;
        await Assert.That(ExpectedAttempts.For(d, (int)QualityLevel.Legendary))
            .IsEqualTo(ExpectedAttempts.Max);
    }

    [Test]
    public async Task VanishinglyUnlikelyTargetClampsToTheCap()
    {
        var d = new double[7];
        d[(int)QualityLevel.Normal] = 0.999999;
        d[(int)QualityLevel.Legendary] = 0.000001;
        await Assert.That(ExpectedAttempts.For(d, (int)QualityLevel.Legendary))
            .IsEqualTo(ExpectedAttempts.Max);
    }

    [Test]
    public async Task TargetAboveTheTopTierClampsToLegendary()
    {
        var d = new double[7];
        d[(int)QualityLevel.Legendary] = 0.5;
        d[(int)QualityLevel.Normal] = 0.5;
        await Assert.That(ExpectedAttempts.For(d, 99))
            .IsEqualTo(ExpectedAttempts.For(d, (int)QualityLevel.Legendary));
    }

    [Test]
    public async Task NeverReturnsLessThanOneAttempt()
    {
        // Rounding slop must not produce a sub-unit multiplier.
        var d = new double[7];
        d[(int)QualityLevel.Legendary] = 1.0000001;
        await Assert.That(ExpectedAttempts.For(d, (int)QualityLevel.Legendary))
            .IsGreaterThanOrEqualTo(1f);
    }

    [Test]
    public async Task HigherTargetNeverCostsFewerAttempts()
    {
        double[] d = QualityOdds.Distribution(12, inspired: false, roleOffset: 0);
        float previous = 0f;
        for (int target = 0; target <= 6; target++)
        {
            float attempts = ExpectedAttempts.For(d, target);
            await Assert.That(attempts).IsGreaterThanOrEqualTo(previous);
            previous = attempts;
        }
    }
}
