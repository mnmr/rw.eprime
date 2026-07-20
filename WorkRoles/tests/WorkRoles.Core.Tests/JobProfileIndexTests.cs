using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

public class JobProfileIndexTests
{
    [Test]
    public async Task BillFactsPreserveBenchAndAllRecipesOrderingWithoutDefReferences()
    {
        var builder = new JobProfileIndexBuilder();
        builder.AddWorkType(1, "Crafting", Skills((10, "Crafting")), new[] { "Bills" });
        builder.AddWorkType(2, "Cooking", Skills((11, "Cooking")), Array.Empty<string>());
        builder.AddRecipeUser(90, JobProfileRecipeUserKind.None, new[] { 900 });
        builder.AddRecipeUser(100, JobProfileRecipeUserKind.Humanlike, new[] { 1000, 1001 });
        builder.AddRecipeUser(101, JobProfileRecipeUserKind.Humanlike, new[] { 1003 });

        // Direct recipes are observed from ThingDef.recipes even when absent
        // from RecipeDef's database. Database reverse users are appended after
        // every user's direct list, just like ThingDef.AllRecipes.
        builder.AddRecipe(Recipe(900, 10, "Crafting", 1f));
        builder.AddRecipe(Recipe(1000, 10, "Crafting", 1f));
        builder.AddRecipe(Recipe(1001, 11, "Cooking", 0f));
        builder.AddDatabaseRecipe(Recipe(1000, 10, "Crafting", 1f), new[] { 100 });
        builder.AddDatabaseRecipe(Recipe(1002, 12, "Artistic", 1f), new[] { 90, 100, 101 });
        builder.AddDatabaseRecipe(Recipe(1003, 13, "Medicine", 1f,
            requiredWorkTypeId: 2), new[] { 101 });
        builder.AddGiver("Bills", 1, Skills((10, "Crafting")),
            fixedRecipeUserIds: new[] { 90 }, allHumanlikes: true);

        JobProfileIndex index = builder.Build();
        JobProfileGiverFacts facts = index.Givers["Bills"];

        await Assert.That(string.Join(",", index.RecipesByUser[90])).IsEqualTo("900,1002");
        await Assert.That(string.Join(",", index.RecipesByUser[100]))
            .IsEqualTo("1000,1001,1000,1002")
            .Because("AllRecipes concatenates direct then database-user recipes; giver Distinct is later");
        await Assert.That(string.Join(",", facts.UsedSkillDefNames))
            .IsEqualTo("Crafting,Artistic,Cooking");
        await Assert.That(string.Join(",", facts.TrainedSkillDefNames))
            .IsEqualTo("Crafting,Artistic");
        await Assert.That(facts.UsesRecipes).IsTrue();
        await Assert.That(facts.GivesXp).IsTrue();
        await Assert.That(facts.Requirements.Count).IsEqualTo(0);
        await Assert.That(index.RecipesByUser.ContainsKey(90)).IsTrue()
            .Because("recipe users outside ThingDef's database still participate for fixed benches");
    }

    [Test]
    public async Task CuratedEmptyWinsAndUnknownCuratedNamesRemainXpFacts()
    {
        var builder = new JobProfileIndexBuilder();
        builder.AddWorkType(1, "Skilled", Skills((10, "Crafting")),
            new[] { "CuratedEmpty", "CuratedUnknown", "Fallback" });
        builder.AddGiver("CuratedEmpty", 1, Skills((10, "Crafting")),
            hasCuratedXp: true, curatedXpSkillDefNames: Array.Empty<string>());
        builder.AddGiver("CuratedUnknown", 1, Skills((10, "Crafting")),
            hasCuratedXp: true, curatedXpSkillDefNames: new[] { "MissingSkill" });
        builder.AddGiver("Fallback", 1, Skills((10, "Crafting")));

        JobProfileIndex index = builder.Build();

        await Assert.That(index.Givers["CuratedEmpty"].TrainedSkillDefNames.Count).IsEqualTo(0);
        await Assert.That(index.Givers["CuratedEmpty"].GivesXp).IsFalse();
        await Assert.That(index.Givers["CuratedUnknown"].TrainedSkillDefNames[0])
            .IsEqualTo("MissingSkill");
        await Assert.That(index.Givers["CuratedUnknown"].GivesXp).IsTrue();
        await Assert.That(index.Givers["Fallback"].TrainedSkillDefNames[0])
            .IsEqualTo("Crafting");
    }

    [Test]
    public async Task RequirementsPreserveReferenceGroupsDuplicateCountsAndEmptySpecialRanges()
    {
        var builder = new JobProfileIndexBuilder();
        builder.SetConstructionSkill(20, "Construction");
        builder.SetSowingSkill(21, "Plants");
        builder.AddConstructionRequirement(3);
        builder.AddConstructionRequirement(3);
        builder.AddConstructionRequirement(8);
        builder.AddWorkType(1, "Build", Skills((20, "Construction")),
            new[] { "ConstructFinishFrames", "Bills" });
        builder.AddWorkType(2, "Grow", Skills((21, "Plants")), new[] { "GrowerSow" });
        builder.AddGiver("ConstructFinishFrames", 1, Skills((20, "Construction")),
            hasCuratedXp: true, curatedXpSkillDefNames: new[] { "Construction" });
        builder.AddGiver("GrowerSow", 2, Skills((21, "Plants")),
            hasCuratedXp: true, curatedXpSkillDefNames: new[] { "Plants" });
        builder.AddRecipeUser(50, JobProfileRecipeUserKind.None, new[] { 1, 2 });
        builder.AddRecipe(Recipe(1, 30, "Crafting", 1f, requirements: new[]
        {
            new JobProfileSkillRequirementSource(40, "Medicine", 2),
            new JobProfileSkillRequirementSource(40, "Medicine", 2),
        }));
        builder.AddRecipe(Recipe(2, 31, "Cooking", 1f, requirements: new[]
        {
            // Same defName, distinct identity: giver ranges stay separate.
            new JobProfileSkillRequirementSource(41, "Medicine", 9),
        }));
        builder.AddGiver("Bills", 1, Skills((20, "Construction")),
            fixedRecipeUserIds: new[] { 50 });

        JobProfileIndex index = builder.Build();
        JobProfileGiverFacts bills = index.Givers["Bills"];

        await Assert.That(bills.Requirements.Count).IsEqualTo(2);
        await Assert.That(bills.Requirements[0].Gated).IsEqualTo(2);
        await Assert.That(bills.Requirements[0].Floor).IsEqualTo(2);
        await Assert.That(bills.Requirements[0].Top).IsEqualTo(2);
        await Assert.That(bills.Requirements[0].Total).IsEqualTo(2);
        await Assert.That(bills.Requirements[1].Top).IsEqualTo(9);
        await Assert.That(index.ConstructionRequirement.Gated).IsEqualTo(3);
        await Assert.That(index.ConstructionRequirement.Floor).IsEqualTo(3);
        await Assert.That(index.ConstructionRequirement.Top).IsEqualTo(8);
        await Assert.That(index.SowingRequirement.Gated).IsEqualTo(0);
        await Assert.That(index.SowingRequirement.Floor).IsEqualTo(0);
        await Assert.That(index.SowingRequirement.Top).IsEqualTo(0);
        await Assert.That(index.Givers["GrowerSow"].Requirements.Count).IsEqualTo(1);
    }

    [Test]
    public async Task LastWinsGiversAndDeclaredWorkTypeMembersKeepBaselineCounts()
    {
        var members = new List<string> { "Duplicate", "Missing", "Duplicate" };
        var builder = new JobProfileIndexBuilder();
        builder.AddWorkType(1, "Type", Skills((10, "Crafting")), members);
        builder.AddGiver("Duplicate", 1, Skills((10, "Crafting")),
            hasCuratedXp: true, curatedXpSkillDefNames: Array.Empty<string>());
        builder.AddGiver("Duplicate", 1, Skills((10, "Crafting")),
            hasCuratedXp: true, curatedXpSkillDefNames: new[] { "Crafting" });

        JobProfileIndex index = builder.Build();
        members.Clear();
        JobProfileWorkTypeFacts type = index.WorkTypes["Type"];

        await Assert.That(type.TotalGivers).IsEqualTo(2);
        await Assert.That(type.XpGivers).IsEqualTo(2);
        await Assert.That(string.Join(",", type.GiverDefNames))
            .IsEqualTo("Duplicate,Duplicate");
    }

    [Test]
    public async Task IndexOwnsInputsAndPublishesReadOnlyCollections()
    {
        var skills = new List<JobProfileSkillSource> { new JobProfileSkillSource(10, "Crafting") };
        var fixedUsers = new List<int> { 20 };
        var builder = new JobProfileIndexBuilder();
        builder.AddWorkType(1, "Type", skills, new[] { "Giver" });
        builder.AddRecipeUser(20, JobProfileRecipeUserKind.None, Array.Empty<int>());
        builder.AddGiver("Giver", 1, skills, fixedRecipeUserIds: fixedUsers);
        JobProfileIndex index = builder.Build();

        skills.Clear();
        fixedUsers.Clear();

        await Assert.That(index.Givers["Giver"].RelevantSkillDefNames[0]).IsEqualTo("Crafting");
        await Assert.That(() => ((IDictionary<string, JobProfileGiverFacts>)index.Givers)
            .Add("Other", index.Givers["Giver"])).Throws<NotSupportedException>();
        await Assert.That(() => ((IList<string>)index.Givers["Giver"].RelevantSkillDefNames)
            .Clear()).Throws<NotSupportedException>();
        await Assert.That(() => builder.AddConstructionRequirement(4))
            .Throws<InvalidOperationException>()
            .Because("a built index is an immutable snapshot and its builder is single-use");
    }

    private static JobProfileRecipeSource Recipe(
        int id,
        int skillId,
        string skillName,
        float learnFactor,
        int? requiredWorkTypeId = null,
        IEnumerable<JobProfileSkillRequirementSource> requirements = null) =>
        new JobProfileRecipeSource(id, requiredWorkTypeId,
            new JobProfileSkillSource(skillId, skillName), learnFactor,
            requirements ?? Array.Empty<JobProfileSkillRequirementSource>());

    private static IReadOnlyList<JobProfileSkillSource> Skills(
        params (int id, string name)[] values) =>
        values.Select(value => new JobProfileSkillSource(value.id, value.name)).ToArray();
}
