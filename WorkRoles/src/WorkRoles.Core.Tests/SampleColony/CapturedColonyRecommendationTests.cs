using System.Xml.Linq;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.SampleColony;

/// Exact no-Misc baseline for every pawn in both game-version captures. This
/// protects the published colony outcome, including ordered roles, selected
/// training paths and pin-preserving Fix My Colony targets. It characterizes
/// existing behavior; focused scenario tests still establish policy correctness.
/// Owner accepted these recorded outcomes as the preservation baseline. The
/// 1.6 and 1.7 ordered roles, active paths and fix targets match exactly.
public class CapturedColonyRecommendationTests
{
    private const string Roster = "Takeo,Madcat,Pun,Hysott,Jet,Hasonove,Severin,Noah,Barbor,Tontrool,Koen,Twiggy,Bashy,Flow,Noin,Cockroach,Niklas,Macey,Hazelnut,Blackwell,Pheanox,Han,Quinn,Selma,Gorilla,Flo,Morg";

    [Test]
    [Arguments("1.6", false, false)]
    [Arguments("1.7", false, false)]
    [Arguments("1.6", false, true)]
    [Arguments("1.7", false, true)]
    [Arguments("1.6", true, false)]
    [Arguments("1.7", true, false)]
    [Arguments("1.6", true, true)]
    [Arguments("1.7", true, true)]
    public async Task EveryColonistRetainsTheCapturedRecommendationsAndFixTargets(string version, bool integrate, bool inactiveRobot)
    {
        var capture = new CapturedColony(version);
        ColonyView colony = capture.Colony;
        colony.IntegrateMechanicalWorkers = integrate;
        if (inactiveRobot) colony.MechanicalWorkers.Add(new MechanicalWorkerView
        {
            Id = 999, ProvidesService = false,
            ExecutableGivers = { "CleanFilth", "HaulGeneral" },
        });
        XElement[] expected = CapturedColony.Required(capture.Document, "actual").Elements("pawn").ToArray();
        XElement[] expectedFix = CapturedColony.Required(capture.Document, "actualFixTargets").Elements("pawn").ToArray();
        await Assert.That(string.Join(",", expected.Select(p => (string)p.Attribute("name")!).Order(StringComparer.Ordinal)))
            .IsEqualTo(string.Join(",", Roster.Split(',').Order(StringComparer.Ordinal)));
        await Assert.That(expected.Select(p => (string)p.Attribute("id")!).Distinct().Count()).IsEqualTo(27);
        await Assert.That(colony.Pawns.Count).IsEqualTo(expected.Length);
        await Assert.That(expectedFix.Length).IsEqualTo(expected.Length);

        RecommendationPlan plan = RecommendationPlan.Build(colony, capture.Tuning);
        IReadOnlyList<PawnFixTarget> fix = ColonyFixPlanner.Build(colony, plan);
        await Assert.That(plan.PawnCount).IsEqualTo(expected.Length);
        await Assert.That(fix.Count).IsEqualTo(expected.Length);
        for (int pawn = 0; pawn < expected.Length; pawn++)
        {
            string context = $"Fisso-NAM {version}: {expected[pawn].Attribute("name")!.Value}";
            await Assert.That(string.Join(",", Enumerable.Range(0, plan.RoleCountAt(pawn))
                .Select(i => plan.RoleAt(pawn, i)))).IsEqualTo((string)expected[pawn].Attribute("roles")!).Because(context);
            await Assert.That(string.Join(",", Enumerable.Range(0, plan.PathCountAt(pawn))
                .Where(i => plan.PathActivatedAt(pawn, i)).Select(i => plan.PathAt(pawn, i))))
                .IsEqualTo((string)expected[pawn].Attribute("paths")!).Because(context);
            await Assert.That((string)expectedFix[pawn].Attribute("id")!).IsEqualTo((string)expected[pawn].Attribute("id")!);
            await Assert.That(string.Join(",", fix[pawn].RoleIds)).IsEqualTo((string)expectedFix[pawn].Attribute("roles")!).Because(context);
            await Assert.That(fix[pawn].Changed).IsEqualTo((bool)expectedFix[pawn].Attribute("changed")!).Because(context);
        }
    }
}
