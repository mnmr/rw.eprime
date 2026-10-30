using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.SampleColony;

public class TrainingEligibilityRegressionTests
{
    [Test]
    [Arguments("1.6", true)]
    [Arguments("1.7", true)]
    [Arguments("1.6", false)]
    [Arguments("1.7", false)]
    public async Task AuthoredDoctorPathStopsAssigningAnIneligibleMedicAndRecoversWhenRestored(string version, bool blocker)
    {
        var capture = new CapturedColony(version);
        ColonyView colony = capture.Colony;
        RoleView medic = colony.Roles.Single(r => r.TemplateDefName == "WS_Medic");
        RoleView doctor = colony.Roles.Single(r => r.TemplateDefName == "WS_Doctor");
        int barbor = capture.Document.Element("actual")!.Elements("pawn")
            .Select((pawn, index) => (pawn, index)).Single(p => (string?)p.pawn.Attribute("name") == "Barbor").index;
        colony.Pawns[barbor].Existing.RemoveAll(a => a.RoleId == medic.Id);
        PathView authored = colony.Paths.Single(p => p.Id == doctor.Id);
        string savedPath = Path(authored);
        RecommendationPlan valid = RecommendationPlan.Build(colony, capture.Tuning);
        await Assert.That(Roles(valid, barbor)).Contains(medic.Id);
        await Assert.That(ActivePaths(valid, barbor)).Contains(doctor.Id);

        medic.Blocker = blocker;
        medic.HasRules = !blocker;
        RecommendationPlan rejected = RecommendationPlan.Build(colony, capture.Tuning);
        await Assert.That(Roles(rejected, barbor)).DoesNotContain(medic.Id);
        await Assert.That(ActivePaths(rejected, barbor)).DoesNotContain(doctor.Id);
        await Assert.That(Path(authored)).IsEqualTo(savedPath);
        for (int pawn = 0; pawn < colony.Pawns.Count; pawn++)
            if (!colony.Pawns[pawn].Existing.Any(a => a.RoleId == medic.Id))
                await Assert.That(Roles(rejected, pawn)).DoesNotContain(medic.Id);

        medic.Blocker = false;
        medic.HasRules = false;
        RecommendationPlan restored = RecommendationPlan.Build(colony, capture.Tuning);
        for (int pawn = 0; pawn < colony.Pawns.Count; pawn++)
        {
            await Assert.That(string.Join(",", Roles(restored, pawn))).IsEqualTo(string.Join(",", Roles(valid, pawn)));
            await Assert.That(string.Join(",", ActivePaths(restored, pawn))).IsEqualTo(string.Join(",", ActivePaths(valid, pawn)));
        }
    }

    private static string Path(PathView path) => string.Join(",", path.RoleIds) + ":"
        + string.Join(",", path.BandMins) + ":" + string.Join(",", path.BandMaxes);
    internal static int[] Roles(RecommendationPlan plan, int pawn) => Enumerable.Range(0, plan.RoleCountAt(pawn)).Select(i => plan.RoleAt(pawn, i)).ToArray();
    internal static int[] ActivePaths(RecommendationPlan plan, int pawn) => Enumerable.Range(0, plan.PathCountAt(pawn))
        .Where(i => plan.PathActivatedAt(pawn, i)).Select(i => plan.PathAt(pawn, i)).ToArray();
}
