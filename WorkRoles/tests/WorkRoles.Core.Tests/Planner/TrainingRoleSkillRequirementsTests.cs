using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

public class TrainingRoleSkillRequirementsTests
{
    [Test]
    public async Task SubsetTargetCoverageIsExcludedFromTheTrainingRoleSkills()
    {
        RoleView general = RecsTestBed.Role(1, "Crafting", "MakeDrugs", "Smith", "Tailor");
        RoleView drugMaker = RecsTestBed.Role(2, "Crafting", "MakeDrugs");
        RoleView unrelated = RecsTestBed.Role(3, "Crafting", "CookMeals");
        PathView applicable = RecsTestBed.Path(10, (general.Id, 0, 15), (drugMaker.Id, 15, 21));
        PathView notASubset = RecsTestBed.Path(11, (general.Id, 0, 15), (unrelated.Id, 15, 21));

        IReadOnlyDictionary<int, HashSet<string>> excluded = TrainingRoleSkillRequirements.ExcludedCoverageByRole([general, drugMaker, unrelated], [applicable, notASubset]);

        await Assert.That(excluded.Keys).IsEquivalentTo([1]);
        await Assert.That(excluded[1]).IsEquivalentTo(["MakeDrugs"]);
    }
}
