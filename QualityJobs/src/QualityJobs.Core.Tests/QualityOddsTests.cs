namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class QualityOddsTests
{
    [Test]
    public async Task DistributionSumsToOne()
    {
        for (int skill = 0; skill <= 20; skill++)
        {
            var d = QualityOdds.Distribution(skill, inspired: false, roleOffset: 0);
            await Assert.That(d.Length).IsEqualTo(7);
            await Assert.That(Math.Abs(d.Sum() - 1.0)).IsLessThan(1e-9);
        }
    }

    [Test]
    public async Task UninspiredNeverRollsLegendary()
    {
        var d = QualityOdds.Distribution(20, inspired: false, roleOffset: 0);
        await Assert.That(d[(int)QualityLevel.Legendary]).IsEqualTo(0.0);
    }

    [Test]
    public async Task InspiredShiftsUpTwoLevels()
    {
        var b = QualityOdds.Distribution(15, inspired: false, roleOffset: 0);
        var i = QualityOdds.Distribution(15, inspired: true, roleOffset: 0);
        // Every base level L lands on min(L+2, 6).
        await Assert.That(Math.Abs(i[6] - (b[4] + b[5] + b[6]))).IsLessThan(1e-9);
        await Assert.That(Math.Abs(i[5] - b[3])).IsLessThan(1e-9);
        await Assert.That(i[0]).IsEqualTo(0.0);
        await Assert.That(i[1]).IsEqualTo(0.0);
    }

    [Test]
    public async Task HigherSkillNeverLowersExpectedQuality()
    {
        double prev = -1;
        for (int skill = 0; skill <= 20; skill++)
        {
            var d = QualityOdds.Distribution(skill, false, 0);
            double mean = 0;
            for (int q = 0; q < d.Length; q++) mean += q * d[q];
            await Assert.That(mean).IsGreaterThanOrEqualTo(prev);
            prev = mean;
        }
    }

    [Test]
    public async Task Skill20MasterworkChanceMatchesVanillaBallpark()
    {
        // Vanilla debug table shows ~10-13% masterwork at skill 20 uninspired.
        var d = QualityOdds.Distribution(20, false, 0);
        await Assert.That(d[(int)QualityLevel.Masterwork]).IsGreaterThan(0.05);
        await Assert.That(d[(int)QualityLevel.Masterwork]).IsLessThan(0.25);
    }

    [Test]
    public async Task SpecialistRoleOffsetShiftsUpOneLevel()
    {
        // roleOffset:1 shifts every bucket up by 1; Legendary (index 6) receives
        // the mass that was at Masterwork (index 5) in the uninspired base.
        var b = QualityOdds.Distribution(20, inspired: false, roleOffset: 0);
        var r = QualityOdds.Distribution(20, inspired: false, roleOffset: 1);
        await Assert.That(r[(int)QualityLevel.Legendary]).IsGreaterThan(0.0);
        await Assert.That(Math.Abs(r[(int)QualityLevel.Legendary] - b[(int)QualityLevel.Masterwork])).IsLessThan(1e-9);
        await Assert.That(r[0]).IsEqualTo(0.0);
    }

    [Test]
    public async Task NegativeRoleOffsetClampsAtAwful()
    {
        // With roleOffset -3 all mass shifted below index 0 accumulates at Awful.
        var base0 = QualityOdds.Distribution(0, inspired: false, roleOffset: 0);
        var neg = QualityOdds.Distribution(0, inspired: false, roleOffset: -3);
        await Assert.That(Math.Abs(neg.Sum() - 1.0)).IsLessThan(1e-9);
        await Assert.That(neg[0]).IsGreaterThan(base0[0]);
    }
}
