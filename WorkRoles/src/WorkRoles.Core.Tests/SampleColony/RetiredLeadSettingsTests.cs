using System.Xml.Linq;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.SampleColony;

public class RetiredLeadSettingsTests
{
    [Test]
    [Arguments("1.6")]
    [Arguments("1.7")]
    public async Task OldLeadSettingsRemainPortableWithoutOfferingInactiveControls(string version)
    {
        // Visible descriptor metadata is the options panel's public input.
        // Retired save keys must survive alongside a live setting, while the
        // full captured colony keeps its ordered recommendations and fix targets.
        SetupOptions imported = SetupOptions.Read(XElement.Parse("""
            <GameSettings>
              <Option name="recommendation.leadMinimumConnectedTargets">20</Option>
              <Option name="recommendation.leadMinimumSignal">5</Option>
              <Option name="recommendation.orderingSkillDivisor">7</Option>
            </GameSettings>
            """), player: false)!;
        SetupOptions reloaded = SetupOptions.Read(imported.ToXml("GameSettings"), player: false)!;
        await Assert.That(reloaded.values["recommendation.leadMinimumConnectedTargets"]).IsEqualTo("20");
        await Assert.That(reloaded.values["recommendation.leadMinimumSignal"]).IsEqualTo("5");
        await Assert.That(reloaded.values["recommendation.orderingSkillDivisor"]).IsEqualTo("7");

        var capture = new CapturedColony(version);
        RecommendationsTuningOptions changed = capture.Tuning;
        foreach (string key in new[] { "leadMinimumConnectedTargets", "leadMinimumSignal" })
        {
            await Assert.That(RecommendationsTuningOptions.TryOption(key, out var option)).IsTrue();
            changed = changed.With(option, int.Parse(reloaded.values["recommendation." + key]));
        }
        RecommendationPlan baseline = RecommendationPlan.Build(capture.Colony, capture.Tuning);
        RecommendationPlan legacy = RecommendationPlan.Build(capture.Colony, changed);
        IReadOnlyList<PawnFixTarget> before = ColonyFixPlanner.Build(capture.Colony, baseline);
        IReadOnlyList<PawnFixTarget> after = ColonyFixPlanner.Build(capture.Colony, legacy);
        for (int pawn = 0; pawn < baseline.PawnCount; pawn++)
        {
            await Assert.That(string.Join(",", Enumerable.Range(0, legacy.RoleCountAt(pawn)).Select(i => legacy.RoleAt(pawn, i))))
                .IsEqualTo(string.Join(",", Enumerable.Range(0, baseline.RoleCountAt(pawn)).Select(i => baseline.RoleAt(pawn, i))));
            await Assert.That(string.Join(",", Enumerable.Range(0, legacy.PathCountAt(pawn)).Select(i => (legacy.PathAt(pawn, i), legacy.PathActivatedAt(pawn, i)))))
                .IsEqualTo(string.Join(",", Enumerable.Range(0, baseline.PathCountAt(pawn)).Select(i => (baseline.PathAt(pawn, i), baseline.PathActivatedAt(pawn, i)))));
            await Assert.That(string.Join(",", after[pawn].RoleIds)).IsEqualTo(string.Join(",", before[pawn].RoleIds));
            await Assert.That(after[pawn].Changed).IsEqualTo(before[pawn].Changed);
        }
        string[] visible = RecommendationsTuningOptions.Descriptors.Where(d => !d.Hidden).Select(d => d.StableKey).ToArray();
        await Assert.That(visible.Contains("leadMinimumConnectedTargets") || visible.Contains("leadMinimumSignal"))
            .IsFalse().Because("retired settings must not be presented as working controls");
        await Assert.That(visible.Contains("orderingSkillDivisor")).IsTrue();
    }
}
