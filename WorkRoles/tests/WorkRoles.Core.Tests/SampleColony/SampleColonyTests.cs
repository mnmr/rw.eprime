using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// Integrity guard for the sample-colony fixture (Fisso-NAM savegame):
/// regression tests built on it can trust the extracted colony shape and the
/// projection into ColonyView.
public class SampleColonyTests
{
    [Test]
    public async Task ColonyShapeMatchesTheSavegame()
    {
        ColonyView colony = SampleColony.BuildColonyView();
        await Assert.That(colony.Pawns.Count).IsEqualTo(25);   // 27 colonists, 2 off-map
        await Assert.That(colony.Roles.Count).IsEqualTo(47);
        await Assert.That(colony.Paths.Count).IsEqualTo(11);
        await Assert.That(colony.OrderTemplate.Count).IsGreaterThan(0);
        await Assert.That(colony.HunterRoleId)
            .IsEqualTo(SampleColony.RoleId("Hunter"));
    }

    [Test]
    public async Task PawnProjectionCarriesLevelsBucketsAndAssignments()
    {
        // Flow: custom xenogerm, gene-boosted 20s with Major passions, aptitude
        // floors at 0, full work capability, 12 role assignments in the save.
        PawnView flow = SampleColony.BuildPawnView(SampleColony.Pawn("Flow"));
        await Assert.That(flow.SkillLevels["Shooting"]).IsEqualTo(20);
        await Assert.That(flow.SkillLevels["Crafting"]).IsEqualTo(20);
        await Assert.That(flow.SkillLevels["Melee"]).IsEqualTo(0);
        await Assert.That(flow.ShootingLevel).IsEqualTo(20);
        await Assert.That(flow.HasRangedWeapon).IsTrue();
        await Assert.That((int)flow.SignalBuckets["Crafting"])
            .IsGreaterThanOrEqualTo((int)SignalBucket.Strong);
        await Assert.That((int)flow.SignalBuckets["Melee"])
            .IsLessThanOrEqualTo((int)SignalBucket.Poor);
        await Assert.That(flow.Existing.Count).IsEqualTo(12);
        await Assert.That(flow.CapableWorkTypes.Count).IsEqualTo(22);

        // Bashy: Medicine totally disabled in the save, melee-armed.
        PawnView bashy = SampleColony.BuildPawnView(SampleColony.Pawn("Bashy"));
        await Assert.That(bashy.SkillLevels.ContainsKey("Medicine")).IsFalse();
        await Assert.That(bashy.HasRangedWeapon).IsFalse();
    }

    [Test]
    public async Task CatalogResolvesScalesSnapshotGiversAndOrder()
    {
        RecommendationCatalogProjection catalog = SampleColony.BuildCatalog();

        RoleView doctor = catalog.Roles.First(
            r => r.Id == SampleColony.RoleId("Doctor"));
        await Assert.That(doctor.Mode).IsEqualTo(ScaleMode.Skilled);
        await Assert.That(doctor.RequiredTotalAt(25)).IsEqualTo(6);

        RoleView hauler = catalog.Roles.First(
            r => r.Id == SampleColony.RoleId("Hauler"));
        await Assert.That(hauler.Mode).IsEqualTo(ScaleMode.Unskilled);

        // Snapshot-backed coverage keeps the modded giver the save recorded.
        RoleView builder = catalog.Roles.First(
            r => r.Id == SampleColony.RoleId("Builder"));
        await Assert.That(builder.Coverage.Contains(
            "QJ_FinishQualityWork_Construction")).IsTrue();

        ColonyView colony = SampleColony.BuildColonyView();
        await Assert.That(string.Join(",", colony.OrderTemplate))
            .IsEqualTo(string.Join(",", SampleColonyData.RecommendationOrder
                .Where(id => colony.OrderTemplate.Contains(id))));
    }

    [Test]
    public async Task RecommendationPlanBuildsDeterministically()
    {
        string first = PlanFingerprint(RecommendationPlan.Build(
            SampleColony.BuildColonyView()));
        string second = PlanFingerprint(RecommendationPlan.Build(
            SampleColony.BuildColonyView()));
        await Assert.That(first).IsEqualTo(second);
        await Assert.That(first.Length).IsGreaterThan(0);
    }

    private static string PlanFingerprint(RecommendationPlan plan)
    {
        var lines = new List<string>();
        for (int pawn = 0; pawn < plan.PawnCount; pawn++)
        {
            var roles = new List<int>();
            for (int i = 0; i < plan.RoleCountAt(pawn); i++)
                roles.Add(plan.RoleAt(pawn, i));
            lines.Add($"{pawn}:{string.Join(",", roles)}");
        }
        return string.Join("|", lines);
    }
}
