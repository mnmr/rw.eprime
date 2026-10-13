using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// The catalog projection is the stable boundary shared by the game adapter
/// and offline recommendation tools. These assertions belong here because a
/// final plan cannot distinguish a correctly reduced training-role profile
/// from an adapter that supplied that already-reduced profile by hand.
public class RecommendationCatalogProjectionTests
{
    [Test]
    public async Task TrainingBandChangesReuseWorkUntilTheExcludedGiversActuallyChange()
    {
        var jobs = new CountedCatalog(new FakeCatalog().WithWorkType("Crafting", "MakeDrug").WithWorkType("Art", "MakeArt"));
        RecommendationRoleSource[] sources =
        [
            new() { Id = 1, Entries = [new(JobEntryKind.WorkGiver, "MakeDrug"), new(JobEntryKind.WorkGiver, "MakeArt")] },
            new() { Id = 2, Entries = [new(JobEntryKind.WorkGiver, "MakeDrug")] },
            new() { Id = 3, Entries = [new(JobEntryKind.WorkGiver, "MakeArt")] },
            new() { Id = 4, MemberRoleIds = [1, 2] },
        ];
        var priorities = new Dictionary<string, int> { ["Crafting"] = 400, ["Art"] = 430 };
        var profiles = Profiles();
        var full = RecommendationCatalogBuilder.Build(sources, [], jobs, priorities, profiles);
        var first = RecommendationCatalogBuilder.WithTraining(full, sources,
            [RecsTestBed.Path(2, (1, 0, 12), (2, 12, 21))], jobs, priorities, profiles);
        jobs.Reads = 0;
        var bands = RecommendationCatalogBuilder.WithTraining(full, sources,
            [RecsTestBed.Path(2, (1, 0, 13), (2, 13, 21))], jobs, priorities, profiles, first);
        await Assert.That(jobs.Reads).IsEqualTo(0);
        await Assert.That(bands.Paths[0].BandMins[1]).IsEqualTo(13);
        for (int i = 0; i < first.Roles.Count; i++)
            await Assert.That(ReferenceEquals(first.Roles[i].WorkSpec, bands.Roles[i].WorkSpec)).IsTrue();
        var target = RecommendationCatalogBuilder.WithTraining(full, sources,
            [RecsTestBed.Path(3, (1, 0, 13), (3, 13, 21))], jobs, priorities, profiles, bands);
        await Assert.That(jobs.Reads > 0).IsTrue();
        await Assert.That(string.Join(",", target.Roles[0].Skills.Select(skill => skill.SkillDefName))).IsEqualTo("Crafting");
        await Assert.That(string.Join(",", bands.Roles[0].Skills.Select(skill => skill.SkillDefName))).IsEqualTo("Artistic");
        await Assert.That(ReferenceEquals(bands.Roles[1].WorkSpec, target.Roles[1].WorkSpec)).IsTrue();
        await Assert.That(ReferenceEquals(bands.Roles[2].WorkSpec, target.Roles[2].WorkSpec)).IsTrue();
    }

    private sealed class CountedCatalog(IJobCatalog source) : IJobCatalog
    {
        internal int Reads;
        public IReadOnlyList<string> WorkGiversOf(string workType) { Reads++; return source.WorkGiversOf(workType); }
        public string WorkTypeOf(string giver) { Reads++; return source.WorkTypeOf(giver); }
        public bool IsEmergency(string giver) { Reads++; return source.IsEmergency(giver); }
    }

    [Test]
    public async Task CatalogReorderingUpdatesTiedDefaultOrderWithoutReplacingWorkOrPathFacts()
    {
        var jobs = new FakeCatalog().WithWorkType("Crafting", "MakeDrug").WithWorkType("Art", "MakeArt");
        var first = new RecommendationRoleSource { Id = 1, Entries = [new(JobEntryKind.WorkGiver, "MakeDrug")] };
        var second = new RecommendationRoleSource { Id = 2, Entries = [new(JobEntryKind.WorkGiver, "MakeArt")] };
        var original = RecommendationCatalogBuilder.Build([first, second],
            [RecsTestBed.Path(1, (1, 0, 21)), RecsTestBed.Path(2, (2, 0, 21))], jobs,
            new Dictionary<string, int> { ["Crafting"] = 400, ["Art"] = 400 }, Profiles());
        var reordered = original.WithRoleOrder([2, 1]);
        await Assert.That(string.Join(",", OrderTemplate.DeriveTemplate(original.Roles))).IsEqualTo("1,2");
        await Assert.That(string.Join(",", OrderTemplate.DeriveTemplate(reordered.Roles))).IsEqualTo("2,1");
        await Assert.That(ReferenceEquals(original.Roles[1], reordered.Roles[0])).IsTrue();
        await Assert.That(ReferenceEquals(original.Paths[1], reordered.Paths[0])).IsTrue();
        await Assert.That(ReferenceEquals(reordered, reordered.WithRoleOrder([2, 1]))).IsTrue();
    }

    [Test]
    public async Task ScalarPolicyEditsRetainTrainingArtifactAndEqualCatalogIdentity()
    {
        var jobs = new FakeCatalog().WithWorkType("Crafting", "MakeDrug").WithWorkType("Art", "MakeArt");
        var general = new RecommendationRoleSource { Id = 1, Entries = [new(JobEntryKind.WorkGiver, "MakeDrug"), new(JobEntryKind.WorkGiver, "MakeArt")] };
        var maker = new RecommendationRoleSource { Id = 2, Entries = [new(JobEntryKind.WorkGiver, "MakeDrug")] };
        var composite = new RecommendationRoleSource { Id = 3, MemberRoleIds = [1, 2] };
        var original = RecommendationCatalogBuilder.Build([general, maker, composite],
            [RecsTestBed.Path(2, (1, 0, 12), (2, 12, 21))], jobs,
            new Dictionary<string, int> { ["Crafting"] = 400, ["Art"] = 430 }, Profiles());
        var policies = original.Roles.Select(RecommendationRolePolicy.From).ToArray();
        policies[1].ColonyMin = 5;
        policies[1].HasRules = true;
        policies[1].Available = false;
        var changed = original.WithPolicy(policies);
        await Assert.That(changed.Roles[1].ColonyMin).IsEqualTo(5);
        await Assert.That(changed.Roles[1].HasRules).IsTrue();
        await Assert.That(changed.Roles[1].Available).IsFalse();
        await Assert.That(original.Roles[1].Available).IsTrue();
        await Assert.That(ReferenceEquals(original.Paths, changed.Paths)).IsTrue();
        await Assert.That(ReferenceEquals(original.Roles[0], changed.Roles[0])).IsTrue();
        for (int i = 0; i < original.Roles.Count; i++)
            await Assert.That(ReferenceEquals(original.Roles[i].WorkSpec, changed.Roles[i].WorkSpec)).IsTrue();
        await Assert.That(ReferenceEquals(changed, changed.WithPolicy(policies))).IsTrue();
    }

    [Test]
    public async Task ActivationChangesRetainTrainingAndCompositeSpecsAndSwitchSpecialFallbacks()
    {
        var jobs = new FakeCatalog().WithWorkType("Crafting", "MakeDrug")
            .WithWorkType("Art", "MakeArt").WithWorkType("Hunting", "Hunt");
        var general = new RecommendationRoleSource { Id = 1, Entries = [new(JobEntryKind.WorkGiver, "MakeDrug"), new(JobEntryKind.WorkGiver, "MakeArt")] };
        var maker = new RecommendationRoleSource { Id = 2, Entries = [new(JobEntryKind.WorkGiver, "MakeDrug")] };
        var composite = new RecommendationRoleSource { Id = 3, MemberRoleIds = [1, 2] };
        var preferred = new RecommendationRoleSource { Id = 4, SpecialRole = RecommendationSpecialRoleKind.Hunter, Entries = [new(JobEntryKind.WorkType, "Hunting")] };
        var fallback = new RecommendationRoleSource { Id = 5, Entries = [new(JobEntryKind.WorkType, "Hunting")] };
        RecommendationCatalogProjection original = RecommendationCatalogBuilder.Build(
            [general, maker, composite, preferred, fallback], [RecsTestBed.Path(2, (1, 0, 12), (2, 12, 21))],
            jobs, new Dictionary<string, int> { ["Crafting"] = 400, ["Art"] = 430, ["Hunting"] = 950 }, Profiles());

        RecommendationCatalogProjection changed = original.WithActivation([true, false, true, false, true]);

        await Assert.That(changed.Roles[1].Enabled).IsFalse();
        await Assert.That(original.Roles[1].Enabled).IsTrue();
        await Assert.That(changed.HunterRoleId).IsEqualTo(5);
        for (int index = 0; index < original.Roles.Count; index++)
            await Assert.That(ReferenceEquals(original.Roles[index].WorkSpec, changed.Roles[index].WorkSpec)).IsTrue();
        await Assert.That(ReferenceEquals(original.Roles[0], changed.Roles[0])).IsTrue();
        await Assert.That(ReferenceEquals(original.Paths, changed.Paths)).IsTrue();
        await Assert.That(ReferenceEquals(changed, changed.WithActivation([true, false, true, false, true]))).IsTrue();
    }

    [Test]
    public async Task SharedFullWorkSpecsAreReusedUntilTrainingExclusionNeedsADifferentProjection()
    {
        var jobs = new FakeCatalog().WithWorkType("Crafting", "MakeDrug").WithWorkType("Art", "MakeArt");
        JobProfileIndex profiles = Profiles();
        var priorities = new Dictionary<string, int> { ["Crafting"] = 400, ["Art"] = 430 };
        var general = new RecommendationRoleSource { Id = 1, Entries = [new JobEntry(JobEntryKind.WorkGiver, "MakeDrug"), new JobEntry(JobEntryKind.WorkGiver, "MakeArt")] };
        var drugMaker = new RecommendationRoleSource { Id = 2, Entries = [new JobEntry(JobEntryKind.WorkGiver, "MakeDrug")] };
        RecommendationCatalogProjection full = RecommendationCatalogBuilder.Build(
            [general, drugMaker], [], jobs, priorities, profiles);
        general.FullWorkSpec = full.Roles[0].WorkSpec;
        drugMaker.FullWorkSpec = full.Roles[1].WorkSpec;

        RecommendationCatalogProjection reused = RecommendationCatalogBuilder.Build(
            [general, drugMaker], [], jobs, priorities, profiles);
        await Assert.That(ReferenceEquals(full.Roles[0].WorkSpec, reused.Roles[0].WorkSpec)).IsTrue();
        await Assert.That(ReferenceEquals(full.Roles[1].WorkSpec, reused.Roles[1].WorkSpec)).IsTrue();

        RecommendationCatalogProjection training = RecommendationCatalogBuilder.Build(
            [general, drugMaker], [RecsTestBed.Path(2, (1, 0, 12), (2, 12, 21))],
            jobs, priorities, profiles);
        await Assert.That(ReferenceEquals(full.Roles[1].WorkSpec, training.Roles[1].WorkSpec)).IsTrue();
        await Assert.That(ReferenceEquals(full.Roles[0].WorkSpec, training.Roles[0].WorkSpec)).IsFalse();
        await Assert.That(training.Roles[0].Skills.Select(skill => skill.SkillDefName)).IsEquivalentTo(["Artistic"]);
        await Assert.That(full.Roles[0].Skills.Select(skill => skill.SkillDefName)).IsEquivalentTo(["Crafting", "Artistic"]);
    }

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
    public async Task ExactSecondaryUsedSkillParticipatesInTheRoleSkillProjection()
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
            .IsEquivalentTo(["Shooting", "Animals"])
            .Because("role skill facts must use the exact skills each job uses");
        await Assert.That(projected.PrimarySkill).IsEqualTo("Shooting");
    }

    [Test]
    public async Task CompositeInheritsTheAlreadyFilteredProfilesOfItsMembers()
    {
        var jobs = new FakeCatalog()
            .WithWorkType("Crafting", "Craft")
            .WithWorkType("Cooking", "CookA", "CookB", "CookC");
        var builder = new JobProfileIndexBuilder();
        JobProfileSkillSource[] crafting = [new(10, "Crafting")];
        JobProfileSkillSource[] cooking = [new(11, "Cooking")];
        builder.AddWorkType(1, "Crafting", crafting, ["Craft"]);
        builder.AddWorkType(2, "Cooking", cooking, ["CookA", "CookB", "CookC"]);
        builder.AddGiver("Craft", 1, crafting,
            hasCuratedXp: true, curatedXpSkillDefNames: ["Crafting"]);
        builder.AddGiver("CookA", 2, cooking,
            hasCuratedXp: true, curatedXpSkillDefNames: ["Cooking"]);
        builder.AddGiver("CookB", 2, cooking,
            hasCuratedXp: true, curatedXpSkillDefNames: ["Cooking"]);
        builder.AddGiver("CookC", 2, cooking,
            hasCuratedXp: true, curatedXpSkillDefNames: ["Cooking"]);
        var crafter = new RecommendationRoleSource
        {
            Id = 1,
            Entries = [new JobEntry(JobEntryKind.WorkType, "Crafting")],
        };
        var cook = new RecommendationRoleSource
        {
            Id = 2,
            Entries = [new JobEntry(JobEntryKind.WorkType, "Cooking")],
        };
        var composite = new RecommendationRoleSource
        {
            Id = 3,
            MemberRoleIds = [crafter.Id, cook.Id],
        };

        RecommendationCatalogProjection projection = RecommendationCatalogBuilder.Build(
            [crafter, cook, composite], [], jobs,
            new Dictionary<string, int>
            {
                ["Crafting"] = 400,
                ["Cooking"] = 300,
            },
            builder.Build());

        RoleView projected = projection.Roles.Single(role => role.Id == composite.Id);
        await Assert.That(projected.Skills.Select(skill => skill.SkillDefName))
            .IsEquivalentTo(["Crafting", "Cooking"])
            .Because("a dominant member must not erase another member's specialist skill");
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
