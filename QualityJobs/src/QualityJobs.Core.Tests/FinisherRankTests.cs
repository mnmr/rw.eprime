namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class FinisherRankTests
{
    private static CandidateFacts F(int id, int skill, bool inspired = false,
        int role = 0, int xpMilli = 0)
        => new(id, skill, inspired, role, true, true, xpMilli);

    [Test]
    public async Task InspiredNoviceRanksBelowMaster()
    {
        // Exact EV (auto spec §2.1): skill 3 + inspiration (3.380) is below
        // plain skill 20 (3.672 — the game's (int) cast floors rolls, so EV
        // sits ~0.45 below the 4.2 center).
        await Assert.That(FinisherRank.Outranks(F(1, 20), F(2, 3, inspired: true))).IsTrue();
        await Assert.That(FinisherRank.Outranks(F(2, 3, inspired: true), F(1, 20))).IsFalse();
    }

    [Test]
    public async Task InspiredMidSkillRanksAboveMaster()
    {
        // Inspired skill 15 (5.210 EV) beats plain skill 20 (3.672).
        await Assert.That(FinisherRank.Outranks(F(1, 15, inspired: true), F(2, 20))).IsTrue();
    }

    [Test]
    public async Task RoleOffsetAddsOneLevel()
    {
        // Skill 12 with role +1 (EV12 + 1.0 exactly) beats plain skill 20.
        await Assert.That(FinisherRank.Outranks(F(1, 12, role: 1), F(2, 20))).IsTrue();
    }

    [Test]
    public async Task XpBreaksEqualRank()
    {
        await Assert.That(FinisherRank.Outranks(F(1, 15, xpMilli: 500), F(2, 15, xpMilli: 100))).IsTrue();
        await Assert.That(FinisherRank.Outranks(F(2, 15, xpMilli: 100), F(1, 15, xpMilli: 500))).IsFalse();
    }

    [Test]
    public async Task ExactTieOutranksNeither()
    {
        await Assert.That(FinisherRank.Outranks(F(1, 15, xpMilli: 250), F(2, 15, xpMilli: 250))).IsFalse();
        await Assert.That(FinisherRank.Outranks(F(2, 15, xpMilli: 250), F(1, 15, xpMilli: 250))).IsFalse();
    }

    [Test]
    public async Task TableMatchesAnalyticExpectedValue()
    {
        // Lockstep guard (auto spec §2.1/§7, amended 2026-08-05): every EvMilli
        // entry equals round(1000 × EV) of the analytic distribution.
        // QualityOdds.Distribution applies shift = 2×inspired + roleOffset, so
        // Distribution(skill, false, shift) yields the shift-s distribution.
        // On mismatch the failure output prints the full expected literal list.
        var expected = new System.Text.StringBuilder();
        var actual = new System.Text.StringBuilder();
        for (int skill = 0; skill <= 20; skill++)
        for (int shift = 0; shift <= 3; shift++)
        {
            double[] d = QualityOdds.Distribution(skill, false, shift);
            double ev = 0;
            for (int q = 0; q < 7; q++) ev += q * d[q];
            expected.Append((int)Math.Round(ev * 1000.0)).Append(',');
            actual.Append(FinisherRank.EvMilliAt(skill, shift)).Append(',');
        }
        await Assert.That(actual.ToString()).IsEqualTo(expected.ToString());
    }

    [Test]
    public async Task EvMilliAtClampsOutOfRangeInputs()
    {
        // Public clamp behavior: out-of-range skill and shift (possible via
        // modded role offsets) clamp to the table edges.
        await Assert.That(FinisherRank.EvMilliAt(25, 0)).IsEqualTo(FinisherRank.EvMilliAt(20, 0));
        await Assert.That(FinisherRank.EvMilliAt(-3, 1)).IsEqualTo(FinisherRank.EvMilliAt(0, 1));
        await Assert.That(FinisherRank.EvMilliAt(10, -1)).IsEqualTo(FinisherRank.EvMilliAt(10, 0));
        await Assert.That(FinisherRank.EvMilliAt(10, 4)).IsEqualTo(FinisherRank.EvMilliAt(10, 3));
    }

    [Test]
    public async Task DistributionShiftIdentityHolds()
    {
        // RankMilliOf assumes Distribution's internal shift = 2×inspired +
        // roleOffset. Guard the identity so a future QualityOdds change that
        // breaks it fails here instead of silently diverging from the table.
        double[] a = QualityOdds.Distribution(10, true, 1);
        double[] b = QualityOdds.Distribution(10, false, 3);
        for (int q = 0; q < 7; q++)
            await Assert.That(a[q]).IsEqualTo(b[q]);
    }
}
