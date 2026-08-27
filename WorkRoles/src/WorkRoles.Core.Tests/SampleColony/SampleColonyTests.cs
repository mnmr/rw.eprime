using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.SampleColony;

/// Integrity guard for the sample-colony fixture (Fisso-NAM savegame):
/// regression tests built on it can trust the extracted colony shape and the
/// projection into ColonyView.
public class SampleColonyTests
{
    [Test]
    public async Task ColonyShapeMatchesTheSavegame()
    {
        ColonyView colony = SampleColony.BuildColonyView();
        await Assert.That(colony.Pawns.Count).IsEqualTo(25); // 27 colonists, 2 off-map
        await Assert.That(colony.Roles.Count).IsEqualTo(47);
        await Assert.That(colony.Paths.Count).IsEqualTo(11);
        await Assert.That(colony.OrderTemplate.Count).IsGreaterThan(0);
        string hunterRole = SampleColony.RoleLabel(colony.HunterRoleId);
        await Assert.That(hunterRole).IsEqualTo("Hunter");
    }

    [Test]
    public async Task FlowProjectionCarriesLevelsBucketsAndAssignments()
    {
        // Flow: custom xenogerm, gene-boosted 20s with Major passions, aptitude
        // floors at 0, full work capability, 12 role assignments in the save.
        PawnView flow = SampleColony.BuildPawnView(SampleColony.Pawn("Flow"));
        HashSet<string> assignments = [.. flow.Existing.Select(assignment => SampleColony.RoleLabel(assignment.RoleId))];

        await Assert.That(flow.SkillLevels["Shooting"]).IsEqualTo(20);
        await Assert.That(flow.SkillLevels["Crafting"]).IsEqualTo(20);
        await Assert.That(flow.SkillLevels["Melee"]).IsEqualTo(0);
        await Assert.That(flow.ShootingLevel).IsEqualTo(20);
        await Assert.That(flow.HasRangedWeapon).IsTrue();
        await Assert.That((int)flow.SignalBuckets["Crafting"]).IsGreaterThanOrEqualTo((int)SignalBucket.Strong);
        await Assert.That((int)flow.SignalBuckets["Melee"]).IsLessThanOrEqualTo((int)SignalBucket.Poor);
        await Assert.That(assignments.SetEquals(["Core", "Basics", "Mech", "Crafter", "Builder", "Fabricator", "Tailor", "Smith", "Hauler", "Cleaner", "Hunter", "Gene Maker"])).IsTrue();
        await Assert.That(flow.CapableWorkTypes.Count).IsEqualTo(22);
    }

    [Test]
    public async Task BashyProjectionOmitsDisabledMedicineAndCarriesMeleeWeaponState()
    {
        // Bashy: Medicine totally disabled in the save, melee-armed.
        PawnView bashy = SampleColony.BuildPawnView(SampleColony.Pawn("Bashy"));
        await Assert.That(bashy.SkillLevels.ContainsKey("Medicine")).IsFalse();
        await Assert.That(bashy.HasRangedWeapon).IsFalse();
    }

    [Test]
    public async Task DoctorCatalogProjectionCarriesDemandAndChampionTuning()
    {
        RecommendationCatalogProjection catalog = SampleColony.BuildCatalog();

        RoleView doctor = catalog.Roles.First(r => r.Id == SampleColony.RoleId("Doctor"));
        await Assert.That(doctor.Unskilled).IsFalse();
        await Assert.That(RoleDemand.RequirementFor(doctor.ColonyMin, doctor.CoveragePercent, 25).RequiredTotal).IsEqualTo(6);
        // championPenalty=false comes straight from the WS_Doctor def tuning
        // and must survive the projection.
        await Assert.That(doctor.ChampionPenalty).IsFalse();
    }

    [Test]
    public async Task HaulerCatalogProjectionIsUnskilledAndNeverPlanned()
    {
        RecommendationCatalogProjection catalog = SampleColony.BuildCatalog();

        // Def-driven demand: Hauler ships without demand and is never planned.
        RoleView hauler = catalog.Roles.First(r => r.Id == SampleColony.RoleId("Hauler"));
        await Assert.That(hauler.Unskilled).IsTrue();
        await Assert.That(hauler.IsNever).IsTrue();
    }

    [Test]
    public async Task BuilderCatalogProjectionKeepsSnapshotBackedModdedGiver()
    {
        RecommendationCatalogProjection catalog = SampleColony.BuildCatalog();

        // Snapshot-backed coverage keeps the modded giver the save recorded.
        RoleView builder = catalog.Roles.First(r => r.Id == SampleColony.RoleId("Builder"));
        await Assert.That(builder.Coverage.Contains("QJ_FinishQualityWork_Construction")).IsTrue();
    }

    [Test]
    public async Task ColonyRecommendationOrderKeepsEveryAvailableConfiguredRoleInOrder()
    {
        ColonyView colony = SampleColony.BuildColonyView();
        string actualOrder = string.Join(",", colony.OrderTemplate);

        await Assert.That(actualOrder).IsEqualTo("43,3,1,6,7,8,9,14,16,19,21,36,20,22,23,24,13,25,28,50");
    }

    [Test]
    public async Task RecommendationPlanBuildsDeterministically()
    {
        string first = PlanFingerprint(RecommendationPlan.Build(SampleColony.BuildColonyView()));
        string second = PlanFingerprint(RecommendationPlan.Build(SampleColony.BuildColonyView()));
        await Assert.That(first).IsEqualTo(second);
        await Assert.That(first.Length).IsGreaterThan(0);
    }

    private static string PlanFingerprint(RecommendationPlan plan)
    {
        List<string> lines = [];
        for (int pawn = 0; pawn < plan.PawnCount; pawn++)
        {
            List<int> roles = [];
            for (int i = 0; i < plan.RoleCountAt(pawn); i++)
                roles.Add(plan.RoleAt(pawn, i));
            lines.Add($"{pawn}:{string.Join(",", roles)}");
        }
        return string.Join("|", lines);
    }
}
