using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// Role tuning travels intact through the published catalog boundary: sources
/// carry it onto views, and the views own independent copies.
public class RoleTuningCatalogTests
{
    [Test]
    public async Task CatalogCarriesRoleTuningOntoViews()
    {
        var jobs = new FakeCatalog().WithWorkType("Doctor", "TendA");
        var required = new List<string> { "Medicine" };
        var optional = new List<string> { "Social" };
        var source = new RecommendationRoleSource
        {
            Id = 7,
            Entries = new List<JobEntry> { new JobEntry(JobEntryKind.WorkType, "Doctor") },
            ChampionPenalty = false,
            Category = RoleCategory.Important,
            Time = RoleTime.PartTime,
            DeclaredRequiredSkills = required,
            DeclaredOptionalSkills = optional,
        };
        RecommendationCatalogProjection catalog = RecommendationCatalogBuilder.Build(
            new[] { source }, new List<PathView>(), jobs,
            new Dictionary<string, int> { ["Doctor"] = 1300 },
            VanillaJobSkillBaseline.Index);

        RoleView view = catalog.Roles[0];
        await Assert.That(view.ChampionPenalty).IsFalse();
        await Assert.That(view.Category).IsEqualTo(RoleCategory.Important);
        await Assert.That(view.Time).IsEqualTo(RoleTime.PartTime);
        await Assert.That(string.Join(",", view.DeclaredRequiredSkills)).IsEqualTo("Medicine");
        await Assert.That(string.Join(",", view.DeclaredOptionalSkills)).IsEqualTo("Social");

        // The view owns its copies: later source-list mutations must not leak in.
        required.Add("Cooking");
        optional.Clear();
        await Assert.That(string.Join(",", view.DeclaredRequiredSkills)).IsEqualTo("Medicine");
        await Assert.That(string.Join(",", view.DeclaredOptionalSkills)).IsEqualTo("Social");
    }

    [Test]
    public async Task UnclassifiedSourcesStayUnclassified()
    {
        var jobs = new FakeCatalog().WithWorkType("Mining", "Mine");
        var source = new RecommendationRoleSource
        {
            Id = 1,
            Entries = new List<JobEntry> { new JobEntry(JobEntryKind.WorkType, "Mining") },
        };
        RecommendationCatalogProjection catalog = RecommendationCatalogBuilder.Build(
            new[] { source }, new List<PathView>(), jobs,
            new Dictionary<string, int> { ["Mining"] = 550 },
            VanillaJobSkillBaseline.Index);

        RoleView view = catalog.Roles[0];
        await Assert.That(view.ChampionPenalty).IsTrue();
        await Assert.That(view.Category).IsEqualTo(RoleCategory.None);
        await Assert.That(view.Time).IsEqualTo(RoleTime.None);
        await Assert.That(view.DeclaredRequiredSkills).IsNull();
        await Assert.That(view.DeclaredOptionalSkills).IsNull();
    }
}
