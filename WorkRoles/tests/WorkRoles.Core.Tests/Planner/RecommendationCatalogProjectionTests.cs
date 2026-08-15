using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// The catalog projection is the stable boundary shared by the game adapter
/// and offline recommendation tools. These assertions belong here because a
/// final plan cannot distinguish a correctly reduced training-role profile
/// from an adapter that supplied that already-reduced profile by hand.
public class RecommendationCatalogProjectionTests
{
    [Test]
    public async Task TrainingTargetCoverageDoesNotSupplyTheTrainerSkillProfile()
    {
        var jobs = new FakeCatalog().WithWorkType("Crafting", "MakeDrug").WithWorkType("Art", "MakeArt");
        JobProfileIndex profiles = Profiles();
        var general = new RecommendationRoleSource { Id = 1, Entries = [new JobEntry(JobEntryKind.WorkGiver, "MakeDrug"), new JobEntry(JobEntryKind.WorkGiver, "MakeArt")] };
        var drugMaker = new RecommendationRoleSource { Id = 2, Entries = [new JobEntry(JobEntryKind.WorkGiver, "MakeDrug")] };
        PathView path = RecsTestBed.Path(1, (general.Id, 0, 12), (drugMaker.Id, 12, 21));

        RecommendationCatalogProjection projection = RecommendationCatalogBuilder.Build(
            [general, drugMaker],
            [path],
            jobs,
            new Dictionary<string, int> { ["Crafting"] = 400, ["Art"] = 430 },
            profiles
        );

        RoleView projectedGeneral = projection.Roles.Single(role => role.Id == general.Id);
        await Assert.That(projectedGeneral.Skills.Select(skill => skill.SkillDefName)).IsEquivalentTo(["Artistic"]);
        await Assert.That(projectedGeneral.PrimarySkill).IsEqualTo("Artistic");
    }

    [Test]
    public async Task InvalidPreferredSpecialRolesUseContentBasedFallbacks()
    {
        var jobs = new FakeCatalog().WithWorkType("Hunting", "Hunt").WithWorkType("Firefighter", "FightFire");
        JobProfileIndex profiles = SpecialProfiles();
        var preferredHunter = new RecommendationRoleSource
        {
            Id = 1,
            Enabled = false,
            SpecialRole = RecommendationSpecialRoleKind.Hunter,
            Entries = [new JobEntry(JobEntryKind.WorkType, "Hunting")],
        };
        var fallbackHunter = new RecommendationRoleSource { Id = 2, Entries = [new JobEntry(JobEntryKind.WorkType, "Hunting")] };
        var preferredFireBlocker = new RecommendationRoleSource
        {
            Id = 3,
            Blocker = true,
            HasRules = true,
            SpecialRole = RecommendationSpecialRoleKind.FireBlocker,
            Entries = [new JobEntry(JobEntryKind.WorkType, "Firefighter")],
        };
        var fallbackFireBlocker = new RecommendationRoleSource
        {
            Id = 4,
            Blocker = true,
            Entries = [new JobEntry(JobEntryKind.WorkType, "Firefighter")],
        };

        RecommendationCatalogProjection projection = RecommendationCatalogBuilder.Build(
            [preferredHunter, fallbackHunter, preferredFireBlocker, fallbackFireBlocker],
            [],
            jobs,
            new Dictionary<string, int> { ["Hunting"] = 950, ["Firefighter"] = 1400 },
            profiles
        );

        await Assert.That(projection.HunterRoleId).IsEqualTo(2);
        await Assert.That(projection.FireBlockerRoleId).IsEqualTo(4);
    }

    [Test]
    public async Task CreatingAColonyDerivesSkillMaximumsFromItsPawns()
    {
        var jobs = new FakeCatalog().WithWorkType("Crafting", "MakeDrug");
        var role = new RecommendationRoleSource { Id = 1, Entries = [new JobEntry(JobEntryKind.WorkType, "Crafting")] };
        RecommendationCatalogProjection projection = RecommendationCatalogBuilder.Build([role], [], jobs, new Dictionary<string, int> { ["Crafting"] = 400 }, Profiles());
        var first = new PawnView();
        first.SkillLevels["Crafting"] = 8;
        var second = new PawnView();
        second.SkillLevels["Crafting"] = 13;

        ColonyView colony = projection.CreateColony([role.Id], [first, second]);

        await Assert.That(colony.SkillMaxLevels["Crafting"]).IsEqualTo(13);
        await Assert.That(colony.WorkTypeSkills["Crafting"]).IsEquivalentTo(["Crafting"]);
        await Assert.That(colony.Roles.Single().Id).IsEqualTo(1);
    }

    [Test]
    public async Task ProjectionOwnsMutablePathInputs()
    {
        var jobs = new FakeCatalog().WithWorkType("Crafting", "MakeDrug");
        var training = new RecommendationRoleSource
        {
            Id = 1,
            ColonyMin = 2,
            Entries = [new JobEntry(JobEntryKind.WorkType, "Crafting")],
        };
        var target = new RecommendationRoleSource { Id = 2, Entries = [new JobEntry(JobEntryKind.WorkGiver, "MakeDrug")] };
        PathView path = RecsTestBed.Path(1, (training.Id, 0, 12), (target.Id, 12, 21));

        RecommendationCatalogProjection projection = RecommendationCatalogBuilder.Build([training, target], [path], jobs, new Dictionary<string, int> { ["Crafting"] = 400 }, Profiles());
        path.RoleIds[0] = 99;
        path.BandMins[1] = 99;

        await Assert.That(projection.Paths[0].RoleIds[0]).IsEqualTo(1);
        await Assert.That(projection.Paths[0].BandMins[1]).IsEqualTo(12);
        await Assert.That(projection.Roles.Single(role => role.Id == 1).ColonyMin).IsEqualTo(2);
    }

    [Test]
    public async Task CuratedNoXpWorkDoesNotInheritTheParentWorkTypeSkill()
    {
        // Rescue-shaped: a curated no-XP giver under a skilled work type. A
        // role covering only that giver is unskilled work; the tend-shaped
        // giver keeps its skill through the curated XP facts.
        var jobs = new FakeCatalog().WithWorkType("Doctor", "Tend", "Rescue");
        var builder = new JobProfileIndexBuilder();
        JobProfileSkillSource[] medicine = [new JobProfileSkillSource(30, "Medicine")];
        builder.AddWorkType(1, "Doctor", medicine, ["Tend", "Rescue"]);
        builder.AddGiver("Tend", 1, medicine, hasCuratedXp: true, curatedXpSkillDefNames: ["Medicine"]);
        builder.AddGiver("Rescue", 1, medicine, hasCuratedXp: true, curatedXpSkillDefNames: []);
        var rescuer = new RecommendationRoleSource { Id = 1, Entries = [new JobEntry(JobEntryKind.WorkGiver, "Rescue")] };
        var medic = new RecommendationRoleSource { Id = 2, Entries = [new JobEntry(JobEntryKind.WorkGiver, "Tend")] };

        RecommendationCatalogProjection projection = RecommendationCatalogBuilder.Build([rescuer, medic], [], jobs, new Dictionary<string, int> { ["Doctor"] = 1300 }, builder.Build());

        RoleView projectedRescuer = projection.Roles.Single(role => role.Id == rescuer.Id);
        await Assert.That(projectedRescuer.Skills.Count).IsEqualTo(0);
        await Assert.That(projectedRescuer.Unskilled).IsTrue();
        RoleView projectedMedic = projection.Roles.Single(role => role.Id == medic.Id);
        await Assert.That(projectedMedic.PrimarySkill).IsEqualTo("Medicine");
    }

    [Test]
    public async Task ChoreHeavyRoleKeepsItsOnlyTrainingSkill()
    {
        // Jailor-shaped: one Social-training giver among many curated no-XP
        // chores. Skill-less work must not dilute the share denominator and
        // erase the skill that defines the role's training purpose.
        var jobs = new FakeCatalog().WithWorkType("Warden", "Chat", "Feed", "Deliver", "Escort", "Release", "Execute");
        var builder = new JobProfileIndexBuilder();
        JobProfileSkillSource[] social = [new JobProfileSkillSource(40, "Social")];
        builder.AddWorkType(1, "Warden", social, ["Chat", "Feed", "Deliver", "Escort", "Release", "Execute"]);
        builder.AddGiver("Chat", 1, social, hasCuratedXp: true, curatedXpSkillDefNames: ["Social"]);
        string[] chores = ["Feed", "Deliver", "Escort", "Release", "Execute"];
        foreach (string chore in chores)
            builder.AddGiver(chore, 1, social, hasCuratedXp: true, curatedXpSkillDefNames: []);
        var jailor = new RecommendationRoleSource { Id = 1, Entries = [new JobEntry(JobEntryKind.WorkType, "Warden")] };

        RecommendationCatalogProjection projection = RecommendationCatalogBuilder.Build([jailor], [], jobs, new Dictionary<string, int> { ["Warden"] = 590 }, builder.Build());

        RoleView projected = projection.Roles.Single();
        await Assert.That(projected.PrimarySkill).IsEqualTo("Social");
        await Assert.That(projected.Unskilled).IsFalse();
    }

    [Test]
    public async Task ExactSecondaryUsedSkillDoesNotChangeLegacyRoleSkillProjection()
    {
        var jobs = new FakeCatalog().WithWorkType("Hunting", "Hunt");
        var builder = new JobProfileIndexBuilder();
        JobProfileSkillSource[] shooting = [new(50, "Shooting")];
        builder.AddWorkType(1, "Hunting", shooting, ["Hunt"]);
        builder.AddGiver(
            "Hunt",
            1,
            shooting,
            hasCuratedXp: true,
            curatedXpSkillDefNames: ["Shooting"],
            hasCuratedUsedSkills: true,
            curatedUsedSkillDefNames: ["Shooting", "Animals"]);
        var hunter = new RecommendationRoleSource
        {
            Id = 1,
            Entries = [new JobEntry(JobEntryKind.WorkType, "Hunting")],
        };

        RecommendationCatalogProjection projection = RecommendationCatalogBuilder.Build(
            [hunter], [], jobs,
            new Dictionary<string, int> { ["Hunting"] = 950 },
            builder.Build());

        RoleView projected = projection.Roles.Single();
        await Assert.That(projected.Skills.Select(skill => skill.SkillDefName))
            .IsEquivalentTo(["Shooting"])
            .Because("profile fidelity must not change recommendation/editor role skills in this slice");
        await Assert.That(projected.PrimarySkill).IsEqualTo("Shooting");
    }

    private static JobProfileIndex Profiles()
    {
        var builder = new JobProfileIndexBuilder();
        JobProfileSkillSource[] crafting = [new(10, "Crafting")];
        JobProfileSkillSource[] artistic = [new(11, "Artistic")];
        builder.AddWorkType(1, "Crafting", crafting, ["MakeDrug"]);
        builder.AddWorkType(2, "Art", artistic, ["MakeArt"]);
        builder.AddGiver("MakeDrug", 1, crafting, hasCuratedXp: true, curatedXpSkillDefNames: ["Crafting"]);
        builder.AddGiver("MakeArt", 2, artistic, hasCuratedXp: true, curatedXpSkillDefNames: ["Artistic"]);
        return builder.Build();
    }

    private static JobProfileIndex SpecialProfiles()
    {
        var builder = new JobProfileIndexBuilder();
        JobProfileSkillSource[] shooting = [new(20, "Shooting")];
        builder.AddWorkType(1, "Hunting", shooting, ["Hunt"]);
        builder.AddWorkType(2, "Firefighter", [], ["FightFire"]);
        builder.AddGiver("Hunt", 1, shooting, hasCuratedXp: true, curatedXpSkillDefNames: ["Shooting"]);
        builder.AddGiver("FightFire", 2, [], hasCuratedXp: true, curatedXpSkillDefNames: []);
        return builder.Build();
    }
}
