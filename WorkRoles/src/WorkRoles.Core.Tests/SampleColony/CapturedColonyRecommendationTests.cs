using System.Xml.Linq;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.SampleColony;

/// Exact no-Misc baseline for every pawn in both game-version captures. This
/// protects the published colony outcome, including ordered roles, selected
/// training paths and pin-preserving Fix My Colony targets. It characterizes
/// existing behavior; focused scenario tests still establish policy correctness.
/// Owner accepted these recorded outcomes as the preservation baseline. The
/// 1.6 and 1.7 ordered roles, active paths and fix targets match exactly.
/// WR5 deliberately adds ordinary Grower for Pheanox: conditional Farmer Away
/// cannot cover ordinary growing demand while its rules are inactive. Keep
/// the original capture intact and review that one literal correction here.
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
            string expectedRoles = expected[pawn].Attribute("name")!.Value == "Pheanox"
                ? "43,1,39,36,55,21,12,20,22,24,48,17,18,13,25,28"
                : (string)expected[pawn].Attribute("roles")!;
            await Assert.That(string.Join(",", Enumerable.Range(0, plan.RoleCountAt(pawn))
                .Select(i => plan.RoleAt(pawn, i)))).IsEqualTo(expectedRoles).Because(context);
            await Assert.That(string.Join(",", Enumerable.Range(0, plan.PathCountAt(pawn))
                .Where(i => plan.PathActivatedAt(pawn, i)).Select(i => plan.PathAt(pawn, i))))
                .IsEqualTo((string)expected[pawn].Attribute("paths")!).Because(context);
            await Assert.That((string)expectedFix[pawn].Attribute("id")!).IsEqualTo((string)expected[pawn].Attribute("id")!);
            await Assert.That(string.Join(",", fix[pawn].RoleIds)).IsEqualTo(expected[pawn].Attribute("name")!.Value == "Pheanox"
                ? "43,1,39,36,55,21,12,20,22,24,48,17,18,13,25,28"
                : (string)expectedFix[pawn].Attribute("roles")!).Because(context);
            await Assert.That(fix[pawn].Changed).IsEqualTo((bool)expectedFix[pawn].Attribute("changed")!).Because(context);
        }
    }

    // The shipped default exclusions only hide work Core and Basics already
    // give every colonist. Lifting them, one at a time or all together, must
    // leave every captured colonist's roles, paths and fix targets untouched.
    [Test]
    [Arguments("1.6")]
    [Arguments("1.7")]
    public async Task DefaultExclusionsAreInertForCapturedHumanRecommendations(string version)
    {
        string[] defaults = ["WS_Rescuer", "WS_Firefighter", "WS_Patient", "WS_Bedrest", "WS_Laborer"];
        string baseline = Outcome(new CapturedColony(version), []);
        foreach (string template in defaults)
            await Assert.That(Outcome(new CapturedColony(version), [template])).IsEqualTo(baseline).Because(template);
        await Assert.That(Outcome(new CapturedColony(version), defaults)).IsEqualTo(baseline).Because("all defaults");
    }

    private static string Outcome(CapturedColony capture, string[] liftedTemplates)
    {
        ColonyView colony = capture.Colony;
        int lifted = 0;
        foreach (RoleView role in colony.Roles)
            if (liftedTemplates.Contains(role.TemplateDefName) && role.ExcludedFromRecommendations)
            { role.ExcludedFromRecommendations = false; lifted++; }
        if (liftedTemplates.Length > 0 && lifted == 0)
            throw new InvalidOperationException("Capture has no excluded role for " + string.Join(",", liftedTemplates));
        RecommendationPlan plan = RecommendationPlan.Build(colony, capture.Tuning);
        IReadOnlyList<PawnFixTarget> fix = ColonyFixPlanner.Build(colony, plan);
        var lines = new List<string>();
        for (int pawn = 0; pawn < plan.PawnCount; pawn++)
            lines.Add(string.Join(",", Enumerable.Range(0, plan.RoleCountAt(pawn)).Select(i => plan.RoleAt(pawn, i)))
                + "|" + string.Join(",", Enumerable.Range(0, plan.PathCountAt(pawn)).Where(i => plan.PathActivatedAt(pawn, i)).Select(i => plan.PathAt(pawn, i)))
                + "|" + string.Join(",", fix[pawn].RoleIds) + "|" + fix[pawn].Changed);
        return string.Join("\n", lines);
    }
}
