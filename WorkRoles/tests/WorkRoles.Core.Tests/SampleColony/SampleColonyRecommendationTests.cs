using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// Recommendation regressions pinned to the sample colony's published outcome:
/// the ordered per-pawn role list and the removals implied by it.
public class SampleColonyRecommendationTests
{
    /// Theith Noin: a miner/artist with burning passions in both, minor
    /// shooting, and a stack of unskilled chores. The plan keeps his skilled
    /// roles and auto/rules roles, and drops the unskilled extras that the
    /// colony's Unskilled strategies hand to other pawns.
    [Test]
    public async Task NoinKeepsSkilledRolesAndShedsUnskilledExtras()
    {
        SamplePawn noin = SampleColony.Pawn("Noin");
        string current = string.Join(", ",
            noin.Assignments.Select(a => SampleColony.RoleLabel(a.RoleId)));
        await Assert.That(current).IsEqualTo(
            "Core, Basics, Cleaner, Hauler, Farmer Away, Butcher, Brewer, "
            + "Miner, Artist, Grunt, Hunter, Gene Maker");

        RecommendationPlan plan = RecommendationPlan.Build(
            SampleColony.BuildColonyView());
        int pawnIndex = -1;
        for (int i = 0; i < SampleColony.CurrentMapPawns.Count; i++)
            if (SampleColony.CurrentMapPawns[i] == noin) pawnIndex = i;

        var recommended = new List<int>();
        for (int i = 0; i < plan.RoleCountAt(pawnIndex); i++)
            recommended.Add(plan.RoleAt(pawnIndex, i));
        await Assert.That(string.Join(", ",
                recommended.Select(SampleColony.RoleLabel)))
            .IsEqualTo("Core, Basics, Farmer Away, Miner, Artist, Butcher, "
                + "Brewer, Grunt, Hunter");

        string removed = string.Join(", ", noin.Assignments
            .Where(a => !recommended.Contains(a.RoleId))
            .Select(a => SampleColony.RoleLabel(a.RoleId)));
        await Assert.That(removed).IsEqualTo("Cleaner, Hauler, Gene Maker");
    }

    /// Barbor (Barborbar "Barbor" Bico): the colony's medic-track pawn
    /// (Medicine 11, no passion), strong animals and shooting, currently also
    /// wearing Childcare, Warden and unskilled chores.
    [Test]
    public async Task BarborKeepsMedicTrackAndGainsPainterGruntResearcher()
    {
        SamplePawn barbor = SampleColony.Pawn("Barbor");
        string current = string.Join(", ",
            barbor.Assignments.Select(a => SampleColony.RoleLabel(a.RoleId)));
        await Assert.That(current).IsEqualTo(
            "Core, Medic, Basics, Handler, Childcare, Warden, Fisher, "
            + "Hauler, Cleaner, Hunter, Gene Maker");

        RecommendationPlan plan = RecommendationPlan.Build(
            SampleColony.BuildColonyView());
        int pawnIndex = -1;
        for (int i = 0; i < SampleColony.CurrentMapPawns.Count; i++)
            if (SampleColony.CurrentMapPawns[i] == barbor) pawnIndex = i;

        var recommended = new List<int>();
        for (int i = 0; i < plan.RoleCountAt(pawnIndex); i++)
            recommended.Add(plan.RoleAt(pawnIndex, i));
        await Assert.That(string.Join(", ",
                recommended.Select(SampleColony.RoleLabel)))
            .IsEqualTo("Core, Medic, Basics, Handler, Fisher, Painter, "
                + "Grunt, Researcher, Hunter");
    }

    /// Takeo Mahoney: broad generalist (burning Construction, minor Plants,
    /// Animals and Intellectual, Medicine 10) with pinned Childcare and both
    /// away-rules roles; the plan trades Joint Maker for Miner and keeps the
    /// rest.
    [Test]
    public async Task TakeoTradesJointMakerForMinerAndKeepsHisSpread()
    {
        SamplePawn takeo = SampleColony.Pawn("Takeo");
        string current = string.Join(", ",
            takeo.Assignments.Select(a => SampleColony.RoleLabel(a.RoleId)));
        await Assert.That(current).IsEqualTo(
            "Core, Medic, Basics, Builder, Farmer Away, Herder, Hunter, "
            + "Miner Away, Farmer, Joint Maker, Fisher, Grunt, Researcher, "
            + "Childcare, Gene Maker");

        RecommendationPlan plan = RecommendationPlan.Build(
            SampleColony.BuildColonyView());
        int pawnIndex = -1;
        for (int i = 0; i < SampleColony.CurrentMapPawns.Count; i++)
            if (SampleColony.CurrentMapPawns[i] == takeo) pawnIndex = i;

        var recommended = new List<int>();
        for (int i = 0; i < plan.RoleCountAt(pawnIndex); i++)
            recommended.Add(plan.RoleAt(pawnIndex, i));
        await Assert.That(string.Join(", ",
                recommended.Select(SampleColony.RoleLabel)))
            .IsEqualTo("Core, Medic, Basics, Builder, Farmer Away, Hunter, "
                + "Herder, Miner Away, Farmer, Miner, Fisher, Grunt, "
                + "Researcher, Childcare");
    }
}
