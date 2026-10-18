using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

public class OptionalJobSkillProfileTests
{
    private const string BrothelPackage = "calamabanana.rjw.brothelcolony";
    private const string GiverClass = "BrothelColony.WorkGiver_Whore";

    [Test]
    public async Task OptionalSkillChangesTheAssignedWorkerThroughTheRealCatalogAndPlanner()
    {
        // Brothel declares only Social as relevant. Both adults can work; the
        // first is better at Social, the second at Sex. One demanded worker
        // among two makes the changed recommendation discipline observable.
        PawnView Worker(int socialLevel, int sexLevel) => new PawnView
        {
            BiologicalAgeTicks = 25 * BiologicalAge.TicksPerYear,
            CapableWorkTypes = { "Brothel" },
            SkillLevels = { ["Social"] = socialLevel, ["Sex"] = sexLevel },
            SignalBuckets = { ["Social"] = SignalBucket.Neutral, ["Sex"] = SignalBucket.Neutral },
        };
        PawnView[] workers = [Worker(6, 0), Worker(3, 20)];

        RecommendationPlan socialOnly = Plan(Profile(sexPackage: null), workers);
        RecommendationPlan withSexperience = Plan(Profile(sexPackage: "rjw.sexperience"), workers);

        await Assert.That(RecsProjection.Holds(socialOnly, 0, 1)).IsTrue();
        await Assert.That(RecsProjection.Holds(socialOnly, 1, 1)).IsFalse();
        await Assert.That(RecsProjection.Holds(withSexperience, 0, 1)).IsFalse();
        await Assert.That(RecsProjection.Holds(withSexperience, 1, 1)).IsTrue();
    }

    [Test]
    public async Task BrothelAloneReportsAuditedSocialUseTrainingAndSuccess()
    {
        OptionalJobSkillProfile? profile = Profile(sexPackage: null);

        await Assert.That(profile).IsNotNull();
        await Assert.That(profile!.UsedSkills).IsEquivalentTo(["Social"]);
        await Assert.That(profile.TrainedSkills).IsEquivalentTo(["Social"]);
        await Assert.That(profile.Effects.Single().SkillDefName).IsEqualTo("Social");
        await Assert.That(profile.Effects.Single().Effects).IsEqualTo(RoleWorkEffect.Success);
    }

    [Test]
    public async Task OwnedSexperienceSkillAddsAuditedUseTrainingAndPriceEffect()
    {
        OptionalJobSkillProfile? profile = Profile(sexPackage: "rjw.sexperience");

        await Assert.That(profile).IsNotNull();
        await Assert.That(profile!.UsedSkills).IsEquivalentTo(["Social", "Sex"]);
        await Assert.That(profile.TrainedSkills).IsEquivalentTo(["Social", "Sex"]);
        await Assert.That(profile.Effects.Single(effect => effect.SkillDefName == "Sex").Effects)
            .IsEqualTo(RoleWorkEffect.Yield);
    }

    [Test]
    [Arguments("Sex", "other.mod")]
    [Arguments("Other", "rjw.sexperience")]
    [Arguments(null, "rjw.sexperience")]
    public async Task ForeignOrMissingSkillCannotChangeTheBrothelDiscipline(
        string? skillName, string? skillPackage)
    {
        OptionalJobSkillProfile? profile = OptionalJobSkillProfile.ForBrothel(
            "Soliciting", BrothelPackage, GiverClass, true,
            "Brothel", BrothelPackage, skillName, skillPackage);

        await Assert.That(profile).IsNotNull();
        await Assert.That(profile!.UsedSkills).IsEquivalentTo(["Social"]);
        await Assert.That(profile.TrainedSkills).IsEquivalentTo(["Social"]);
    }

    [Test]
    [Arguments(null, null, null, false, null, null)]
    [Arguments("Soliciting", "other.mod", GiverClass, true, "Brothel", BrothelPackage)]
    [Arguments("Soliciting", BrothelPackage, "Other.WorkGiver", true, "Brothel", BrothelPackage)]
    [Arguments("Soliciting", BrothelPackage, GiverClass, false, "Brothel", BrothelPackage)]
    [Arguments("Soliciting", BrothelPackage, GiverClass, true, "Brothel", "other.mod")]
    [Arguments("Other", BrothelPackage, GiverClass, true, "Brothel", BrothelPackage)]
    [Arguments("Soliciting", BrothelPackage, GiverClass, true, "Other", BrothelPackage)]
    public async Task AbsentOrCollidingDefinitionsDoNotReceiveCuratedOptionalFacts(
        string? giver, string? giverPackage, string? giverClass, bool classOwned,
        string? workType, string? workTypePackage)
    {
        OptionalJobSkillProfile? profile = OptionalJobSkillProfile.ForBrothel(
            giver, giverPackage, giverClass, classOwned, workType,
            workTypePackage, "Sex", "rjw.sexperience");

        await Assert.That(profile).IsNull();
    }

    private static OptionalJobSkillProfile? Profile(string? sexPackage) =>
        OptionalJobSkillProfile.ForBrothel("Soliciting", BrothelPackage,
            GiverClass, true, "Brothel", BrothelPackage,
            sexPackage == null ? null : "Sex", sexPackage);

    private static RecommendationPlan Plan(OptionalJobSkillProfile? profile,
        PawnView[] workers)
    {
        var jobs = new FakeCatalog().WithWorkType("Brothel", "Soliciting");
        var profiles = new JobProfileIndexBuilder();
        JobProfileSkillSource[] social = [new(1, "Social")];
        profiles.AddWorkType(1, "Brothel", social, ["Soliciting"]);
        JobProfileSkillSource[] recommendationSkills = profile?.RecommendationSkillDefName == "Sex"
            ? [new(2, "Sex")] : social;
        profiles.AddGiver("Soliciting", 1, recommendationSkills,
            hasCuratedXp: profile != null,
            curatedXpSkillDefNames: profile?.TrainedSkills,
            hasCuratedUsedSkills: profile != null,
            curatedUsedSkillDefNames: profile?.UsedSkills,
            curatedSkillEffects: profile?.Effects);
        var role = new RecommendationRoleSource
        {
            Id = 1, ColonyMin = 1, Coverage = 50, MinAge = 18,
            Entries = [new JobEntry(JobEntryKind.WorkType, "Brothel")],
        };
        RecommendationCatalogProjection projection = RecommendationCatalogBuilder.Build(
            [role], [], jobs, new Dictionary<string, int> { ["Brothel"] = 500 }, profiles.Build());
        return RecommendationPlan.Build(projection.CreateColony([role.Id], workers));
    }
}
