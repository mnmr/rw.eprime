using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

/// Explicit role skill gates travel intact through the published catalog
/// boundary, and the views own independent copies.
public class RoleTuningCatalogTests
{
    [Test]
    public async Task CatalogCarriesRoleTuningOntoViews()
    {
        var jobs = new FakeCatalog().WithWorkType("Doctor", "TendA");
        List<string> required = ["Medicine"];
        var source = new RecommendationRoleSource
        {
            Id = 7,
            Entries = [new JobEntry(JobEntryKind.WorkType, "Doctor")],
            ChampionPenalty = false,
            Category = RoleCategory.Important,
            Time = RoleTime.PartTime,
            DeclaredRequiredSkills = required,
        };
        RecommendationCatalogProjection catalog = RecommendationCatalogBuilder.Build([source], [], jobs, new Dictionary<string, int> { ["Doctor"] = 1300 }, VanillaJobSkillBaseline.Index);

        RoleView view = catalog.Roles[0];
        await Assert.That(view.ChampionPenalty).IsFalse();
        await Assert.That(view.Category).IsEqualTo(RoleCategory.Important);
        await Assert.That(view.Time).IsEqualTo(RoleTime.PartTime);
        await Assert.That(string.Join(",", view.DeclaredRequiredSkills)).IsEqualTo("Medicine");

        // The view owns its copies: later source-list mutations must not leak in.
        required.Add("Cooking");
        await Assert.That(string.Join(",", view.DeclaredRequiredSkills)).IsEqualTo("Medicine");
    }

    [Test]
    public async Task UnclassifiedSourcesStayUnclassified()
    {
        var jobs = new FakeCatalog().WithWorkType("Mining", "Mine");
        var source = new RecommendationRoleSource { Id = 1, Entries = [new JobEntry(JobEntryKind.WorkType, "Mining")] };
        RecommendationCatalogProjection catalog = RecommendationCatalogBuilder.Build([source], [], jobs, new Dictionary<string, int> { ["Mining"] = 550 }, VanillaJobSkillBaseline.Index);

        RoleView view = catalog.Roles[0];
        await Assert.That(view.ChampionPenalty).IsTrue();
        await Assert.That(view.Category).IsEqualTo(RoleCategory.None);
        await Assert.That(view.Time).IsEqualTo(RoleTime.None);
        await Assert.That(view.DeclaredRequiredSkills).IsEmpty();
    }

    [Test]
    public async Task CompositeInheritsMemberSkillGatesAndKeepsItsOwn()
    {
        var jobs = new FakeCatalog()
            .WithWorkType("Doctor", "TendA")
            .WithWorkType("Crafting", "CraftA");
        var doctor = new RecommendationRoleSource
        {
            Id = 1,
            DeclaredRequiredSkills = ["Medicine"],
            Entries = [new JobEntry(JobEntryKind.WorkType, "Doctor")],
        };
        var crafter = new RecommendationRoleSource
        {
            Id = 2,
            DeclaredRequiredSkills = ["Crafting"],
            Entries = [new JobEntry(JobEntryKind.WorkType, "Crafting")],
        };
        var composite = new RecommendationRoleSource
        {
            Id = 3,
            MemberRoleIds = [doctor.Id, crafter.Id],
            DeclaredRequiredSkills = ["Intellectual"],
            Entries =
            [
                new JobEntry(JobEntryKind.WorkType, "Doctor"),
                new JobEntry(JobEntryKind.WorkType, "Crafting"),
            ],
        };

        RecommendationCatalogProjection catalog =
            RecommendationCatalogBuilder.Build(
                [doctor, crafter, composite], [], jobs,
                new Dictionary<string, int>
                {
                    ["Doctor"] = 1300,
                    ["Crafting"] = 400,
                },
                VanillaJobSkillBaseline.Index);

        RoleView view = catalog.Roles.Single(role => role.Id == composite.Id);
        await Assert.That(view.DeclaredRequiredSkills)
            .IsEquivalentTo(["Intellectual", "Medicine", "Crafting"]);
    }
}
