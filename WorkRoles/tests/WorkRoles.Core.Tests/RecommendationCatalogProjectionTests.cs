using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// The catalog projection is the stable boundary shared by the game adapter
/// and offline recommendation tools. These assertions belong here because a
/// final plan cannot distinguish a correctly reduced training-role profile
/// from an adapter that supplied that already-reduced profile by hand.
public class RecommendationCatalogProjectionTests
{
    [Test]
    public async Task TrainingTargetCoverageDoesNotSupplyTheTrainerSkillProfile()
    {
        var jobs = new FakeCatalog()
            .WithWorkType("Crafting", "MakeDrug")
            .WithWorkType("Art", "MakeArt");
        JobProfileIndex profiles = Profiles();
        var general = new RecommendationRoleSource
        {
            Id = 1,
            Entries =
            {
                new JobEntry(JobEntryKind.WorkGiver, "MakeDrug"),
                new JobEntry(JobEntryKind.WorkGiver, "MakeArt"),
            },
        };
        var drugMaker = new RecommendationRoleSource
        {
            Id = 2,
            Entries =
            {
                new JobEntry(JobEntryKind.WorkGiver, "MakeDrug"),
            },
        };
        PathView path = RecsTestBed.Path(
            1, (general.Id, 0, 12), (drugMaker.Id, 12, 21));

        RecommendationCatalogProjection projection =
            RecommendationCatalogBuilder.Build(
                new[] { general, drugMaker },
                new[] { path },
                jobs,
                new Dictionary<string, int>
                {
                    ["Crafting"] = 400,
                    ["Art"] = 430,
                },
                profiles);

        RoleView projectedGeneral = projection.Roles.Single(
            role => role.Id == general.Id);
        await Assert.That(projectedGeneral.Skills
                .Select(skill => skill.SkillDefName))
            .IsEquivalentTo(new[] { "Artistic" });
        await Assert.That(projectedGeneral.PrimarySkill)
            .IsEqualTo("Artistic");
    }

    [Test]
    public async Task InvalidPreferredSpecialRolesUseContentBasedFallbacks()
    {
        var jobs = new FakeCatalog()
            .WithWorkType("Hunting", "Hunt")
            .WithWorkType("Firefighter", "FightFire");
        JobProfileIndex profiles = SpecialProfiles();
        var preferredHunter = new RecommendationRoleSource
        {
            Id = 1,
            Enabled = false,
            SpecialRole = RecommendationSpecialRoleKind.Hunter,
            Entries =
            {
                new JobEntry(JobEntryKind.WorkType, "Hunting"),
            },
        };
        var fallbackHunter = new RecommendationRoleSource
        {
            Id = 2,
            Entries =
            {
                new JobEntry(JobEntryKind.WorkType, "Hunting"),
            },
        };
        var preferredFireBlocker = new RecommendationRoleSource
        {
            Id = 3,
            Blocker = true,
            HasRules = true,
            SpecialRole = RecommendationSpecialRoleKind.FireBlocker,
            Entries =
            {
                new JobEntry(JobEntryKind.WorkType, "Firefighter"),
            },
        };
        var fallbackFireBlocker = new RecommendationRoleSource
        {
            Id = 4,
            Blocker = true,
            Entries =
            {
                new JobEntry(JobEntryKind.WorkType, "Firefighter"),
            },
        };

        RecommendationCatalogProjection projection =
            RecommendationCatalogBuilder.Build(
                new[]
                {
                    preferredHunter,
                    fallbackHunter,
                    preferredFireBlocker,
                    fallbackFireBlocker,
                },
                Array.Empty<PathView>(),
                jobs,
                new Dictionary<string, int>
                {
                    ["Hunting"] = 950,
                    ["Firefighter"] = 1400,
                },
                profiles);

        await Assert.That(projection.HunterRoleId)
            .IsEqualTo(fallbackHunter.Id);
        await Assert.That(projection.FireBlockerRoleId)
            .IsEqualTo(fallbackFireBlocker.Id);
    }

    [Test]
    public async Task CreatingAColonyDerivesSkillMaximumsFromItsPawns()
    {
        var jobs = new FakeCatalog()
            .WithWorkType("Crafting", "MakeDrug");
        var role = new RecommendationRoleSource
        {
            Id = 1,
            Entries =
            {
                new JobEntry(JobEntryKind.WorkType, "Crafting"),
            },
        };
        RecommendationCatalogProjection projection =
            RecommendationCatalogBuilder.Build(
                new[] { role },
                Array.Empty<PathView>(),
                jobs,
                new Dictionary<string, int> { ["Crafting"] = 400 },
                Profiles());
        var first = new PawnView();
        first.SkillLevels["Crafting"] = 8;
        var second = new PawnView();
        second.SkillLevels["Crafting"] = 13;

        ColonyView colony = projection.CreateColony(
            new[] { role.Id }, new[] { first, second });

        await Assert.That(colony.SkillMaxLevels["Crafting"])
            .IsEqualTo(13);
        await Assert.That(colony.WorkTypeSkills["Crafting"])
            .IsEquivalentTo(new[] { "Crafting" });
        await Assert.That(colony.Roles.Single().Id).IsEqualTo(role.Id);
    }

    [Test]
    public async Task ProjectionOwnsMutablePathAndScaleInputs()
    {
        var jobs = new FakeCatalog()
            .WithWorkType("Crafting", "MakeDrug");
        var scale = new HolderScale();
        Array.Fill(scale.RequiredTotals, 2);
        Array.Fill(scale.TrainingWaivers, 1);
        Array.Fill(scale.Max, RoleHolderRange.Uncapped);
        var training = new RecommendationRoleSource
        {
            Id = 1,
            Scale = scale,
            Entries =
            {
                new JobEntry(JobEntryKind.WorkType, "Crafting"),
            },
        };
        var target = new RecommendationRoleSource
        {
            Id = 2,
            Entries =
            {
                new JobEntry(JobEntryKind.WorkGiver, "MakeDrug"),
            },
        };
        PathView path = RecsTestBed.Path(
            1, (training.Id, 0, 12), (target.Id, 12, 21));

        RecommendationCatalogProjection projection =
            RecommendationCatalogBuilder.Build(
                new[] { training, target },
                new[] { path },
                jobs,
                new Dictionary<string, int> { ["Crafting"] = 400 },
                Profiles());
        path.RoleIds[0] = 99;
        path.BandMins[1] = 99;
        scale.RequiredTotals[0] = 99;

        await Assert.That(projection.Paths[0].RoleIds[0])
            .IsEqualTo(training.Id);
        await Assert.That(projection.Paths[0].BandMins[1])
            .IsEqualTo(12);
        await Assert.That(projection.Roles.Single(
                role => role.Id == training.Id).Scale.RequiredTotalAt(1))
            .IsEqualTo(2);
    }

    [Test]
    public async Task CuratedNoXpWorkDoesNotInheritTheParentWorkTypeSkill()
    {
        // Rescue-shaped: a curated no-XP giver under a skilled work type. A
        // role covering only that giver is unskilled work; the tend-shaped
        // giver keeps its skill through the curated XP facts.
        var jobs = new FakeCatalog().WithWorkType("Doctor", "Tend", "Rescue");
        var builder = new JobProfileIndexBuilder();
        var medicine = new[] { new JobProfileSkillSource(30, "Medicine") };
        builder.AddWorkType(1, "Doctor", medicine, new[] { "Tend", "Rescue" });
        builder.AddGiver("Tend", 1, medicine,
            hasCuratedXp: true, curatedXpSkillDefNames: new[] { "Medicine" });
        builder.AddGiver("Rescue", 1, medicine,
            hasCuratedXp: true, curatedXpSkillDefNames: Array.Empty<string>());
        var rescuer = new RecommendationRoleSource
        {
            Id = 1,
            Entries = { new JobEntry(JobEntryKind.WorkGiver, "Rescue") },
        };
        var medic = new RecommendationRoleSource
        {
            Id = 2,
            Entries = { new JobEntry(JobEntryKind.WorkGiver, "Tend") },
        };

        RecommendationCatalogProjection projection =
            RecommendationCatalogBuilder.Build(
                new[] { rescuer, medic },
                Array.Empty<PathView>(),
                jobs,
                new Dictionary<string, int> { ["Doctor"] = 1300 },
                builder.Build());

        RoleView projectedRescuer = projection.Roles.Single(
            role => role.Id == rescuer.Id);
        await Assert.That(projectedRescuer.Skills.Count).IsEqualTo(0);
        await Assert.That(projectedRescuer.Unskilled).IsTrue();
        RoleView projectedMedic = projection.Roles.Single(
            role => role.Id == medic.Id);
        await Assert.That(projectedMedic.PrimarySkill).IsEqualTo("Medicine");
    }

    [Test]
    public async Task ChoreHeavyRoleKeepsItsOnlyTrainingSkill()
    {
        // Jailor-shaped: one Social-training giver among many curated no-XP
        // chores. Skill-less work must not dilute the share denominator and
        // erase the skill that defines the role's training purpose.
        var jobs = new FakeCatalog().WithWorkType("Warden",
            "Chat", "Feed", "Deliver", "Escort", "Release", "Execute");
        var builder = new JobProfileIndexBuilder();
        var social = new[] { new JobProfileSkillSource(40, "Social") };
        builder.AddWorkType(1, "Warden", social,
            new[] { "Chat", "Feed", "Deliver", "Escort", "Release", "Execute" });
        builder.AddGiver("Chat", 1, social,
            hasCuratedXp: true, curatedXpSkillDefNames: new[] { "Social" });
        foreach (string chore in new[]
                 { "Feed", "Deliver", "Escort", "Release", "Execute" })
            builder.AddGiver(chore, 1, social,
                hasCuratedXp: true,
                curatedXpSkillDefNames: Array.Empty<string>());
        var jailor = new RecommendationRoleSource
        {
            Id = 1,
            Entries = { new JobEntry(JobEntryKind.WorkType, "Warden") },
        };

        RecommendationCatalogProjection projection =
            RecommendationCatalogBuilder.Build(
                new[] { jailor },
                Array.Empty<PathView>(),
                jobs,
                new Dictionary<string, int> { ["Warden"] = 590 },
                builder.Build());

        RoleView projected = projection.Roles.Single();
        await Assert.That(projected.PrimarySkill).IsEqualTo("Social");
        await Assert.That(projected.Unskilled).IsFalse();
    }

    private static JobProfileIndex Profiles()
    {
        var builder = new JobProfileIndexBuilder();
        var crafting = new[] { new JobProfileSkillSource(10, "Crafting") };
        var artistic = new[] { new JobProfileSkillSource(11, "Artistic") };
        builder.AddWorkType(1, "Crafting", crafting, new[] { "MakeDrug" });
        builder.AddWorkType(2, "Art", artistic, new[] { "MakeArt" });
        builder.AddGiver(
            "MakeDrug", 1, crafting,
            hasCuratedXp: true,
            curatedXpSkillDefNames: new[] { "Crafting" });
        builder.AddGiver(
            "MakeArt", 2, artistic,
            hasCuratedXp: true,
            curatedXpSkillDefNames: new[] { "Artistic" });
        return builder.Build();
    }

    private static JobProfileIndex SpecialProfiles()
    {
        var builder = new JobProfileIndexBuilder();
        var shooting = new[] { new JobProfileSkillSource(20, "Shooting") };
        builder.AddWorkType(1, "Hunting", shooting, new[] { "Hunt" });
        builder.AddWorkType(
            2,
            "Firefighter",
            Array.Empty<JobProfileSkillSource>(),
            new[] { "FightFire" });
        builder.AddGiver(
            "Hunt", 1, shooting,
            hasCuratedXp: true,
            curatedXpSkillDefNames: new[] { "Shooting" });
        builder.AddGiver(
            "FightFire", 2, Array.Empty<JobProfileSkillSource>(),
            hasCuratedXp: true,
            curatedXpSkillDefNames: Array.Empty<string>());
        return builder.Build();
    }
}
