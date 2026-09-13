namespace QualityJobs.Core.Tests;

using QualityJobs.Core;

public class QualityExpertiseTests
{
    [Test]
    public async Task FractionalStatsDoNotRoundUpToAGuaranteedUpgrade()
    {
        await Assert.That(QualityBonus.FromStat(0.9999f)).IsEqualTo(999);
        await Assert.That(QualityBonus.FromStat(1.9999f)).IsEqualTo(1999);
        await Assert.That(QualityBonus.FromStat(1f)).IsEqualTo(1000);
    }

    [Test]
    public async Task ResolvedAutoBestOddsIncludeItsActualInspirationAndRole()
    {
        var actualFinisher = new CandidateFacts(1, 15, true, 1, true, true, qualityBonusMilli: 250);
        // Independently reviewed threshold: skill 15, inspiration +2, role +1
        // and 25% expertise give more than 85% Legendary, despite no requirement
        // to be inspired or a specialist in the configured automatic gate.
        double chance = GateOdds.SuccessChanceFor(actualFinisher, 6);
        await Assert.That(chance).IsGreaterThan(0.85);
        await Assert.That(chance).IsLessThan(1.0);
    }

    [Test]
    [Arguments(15, 500)]
    [Arguments(20, 50)]
    public async Task QualityExpertWinsSelectionAndCompletionGate(int expertSkill, int bonusMilli)
    {
        // A speed-focused master has more skill/XP; the quality expert's bonus
        // nevertheless gives the better expected result. Both the finishing
        // gate and dispatch must use that same assessment.
        var master = new CandidateFacts(1, 20, false, 0, true, true, 900);
        var expert = new CandidateFacts(2, expertSkill, false, 0, true, true,
            100, qualityBonusMilli: bonusMilli);
        CandidateFacts[] colony = [master, expert];
        var condition = new ResumeCondition(0, false, false);

        await Assert.That(FinisherSelector.SelectBest(colony, condition)).IsEqualTo(expert.Id);
        await Assert.That(FinisherSelector.SelectAutoBest(colony, colony, condition)).IsEqualTo(expert.Id);
        await Assert.That(GateDecision.DecideAuto(true, false, expert, colony, condition)).IsEqualTo(GateOutcome.Complete);
        await Assert.That(GateDecision.DecideAuto(true, false, master, colony, condition)).IsEqualTo(GateOutcome.Pause);
        // An unavailable colony-wide expert still holds the automatic gate.
        await Assert.That(FinisherSelector.SelectAutoBest([master], colony, condition)).IsEqualTo(FinisherSelector.None);
    }

    [Test]
    public async Task ExpertiseDoesNotReplaceManualRequirementsOrExistingQualityBonuses()
    {
        var expert = new CandidateFacts(1, 15, false, 0, true, true, qualityBonusMilli: 500);
        var inspiredSpecialist = new CandidateFacts(2, 15, true, 1, true, true);
        CandidateFacts[] colony = [expert, inspiredSpecialist];

        await Assert.That(FinisherSelector.SelectAutoBest(colony, colony, default)).IsEqualTo(inspiredSpecialist.Id);
        await Assert.That(GateDecision.Decide(true, false, expert, new ResumeCondition(20, false, false))).IsEqualTo(GateOutcome.Pause);
        await Assert.That(GateDecision.Decide(true, false, expert, new ResumeCondition(0, false, true))).IsEqualTo(GateOutcome.Pause);
    }

    [Test]
    public async Task QualityBonusCanReachLegendaryAndSupportsMoreThanOneUpgrade()
    {
        double[] ordinary = QualityOdds.Distribution(20, false, 0);
        double[] quarterBonus = QualityOdds.Distribution(20, false, 0, qualityBonusMilli: 250);
        double[] wholeBonus = QualityOdds.Distribution(20, false, 0, qualityBonusMilli: 1000);
        // With no inspiration or role, Legendary is reachable only by upgrading
        // a Masterwork. A 25% bonus converts a quarter of those outcomes.
        await Assert.That(quarterBonus[6]).IsEqualTo(ordinary[5] / 4).Within(1e-12);
        await Assert.That(wholeBonus[6]).IsEqualTo(ordinary[5]).Within(1e-12);
        await Assert.That(GateOdds.SuccessChanceFor(new ResumeCondition(20, false, false),
            6, qualityBonusMilli: 1000)).IsEqualTo(ordinary[5]).Within(1e-12);
        // After +1 guaranteed and a 25% second upgrade, every Masterwork reaches
        // Legendary and one quarter of Excellent outcomes join it.
        double[] extraBonus = QualityOdds.Distribution(20, false, 0, qualityBonusMilli: 1250);
        await Assert.That(extraBonus[6]).IsEqualTo(ordinary[5] + ordinary[4] / 4).Within(1e-12);
        await Assert.That(extraBonus.Sum()).IsEqualTo(1.0).Within(1e-12);
        double[] capped = QualityOdds.Distribution(20, true, 1, qualityBonusMilli: 6000);
        await Assert.That(capped[6]).IsEqualTo(1.0).Within(1e-12);
    }

    [Test]
    public async Task BonusNormalizationAndSaturationPreserveRankingAndOddsAgreement()
    {
        // The ranking is fixed-point and allocation-free; the displayed odds
        // use an analytic distribution. Check their independently meaningful
        // agreement, including the diminishing return near Legendary.
        foreach (int skill in new[] { 0, 15, 20 })
        foreach (bool inspired in new[] { false, true })
        foreach (int role in new[] { 0, 1 })
        foreach (int bonus in new[] { 50, 500, 1000, 1250, 6000 })
        {
            var candidate = new CandidateFacts(1, skill, inspired, role, true, true,
                qualityBonusMilli: bonus);
            double[] odds = QualityOdds.Distribution(skill, inspired, role, bonus);
            double expected = odds.Select((p, quality) => p * quality).Sum() * 1000;
            await Assert.That((double)FinisherRank.RankMilliOf(candidate)).IsEqualTo(expected).Within(1.0);
        }
        await Assert.That(QualityBonus.FromStat(0.05f)).IsEqualTo(50);
        await Assert.That(QualityBonus.FromStat(1.25f)).IsEqualTo(1250);
        await Assert.That(QualityBonus.FromStat(float.NaN)).IsEqualTo(0);
        await Assert.That(QualityBonus.FromStat(-1f)).IsEqualTo(0);
        await Assert.That(QualityBonus.FromStat(float.PositiveInfinity)).IsEqualTo(6000);
        var saturated = new CandidateFacts(1, 0, false, 0, true, true, qualityBonusMilli: int.MaxValue);
        await Assert.That(FinisherRank.RankMilliOf(saturated)).IsEqualTo(6000);
    }
}
