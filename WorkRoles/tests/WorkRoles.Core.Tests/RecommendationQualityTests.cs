using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

/// Certifies engine output against the shipped defs on seeded colonies.
/// Rules live here only once the user has ratified them.
public class RecommendationQualityTests
{
    /// Every assignment the engine invents carries a reason the UI can show
    /// (protected re-entries and kept chores excepted — these colonies start
    /// with no existing assignments, so every entry must have one).
    [Test]
    [Arguments(6, 7)] [Arguments(10, 33)] [Arguments(50, 44)]
    public async Task EveryAssignmentCarriesAReason(int size, int seed)
    {
        var (catalog, _, results) = ColonyScenarioTests.Execute(size, seed);
        for (int i = 0; i < results.Count; i++)
            foreach (var assignment in results[i].Assignments)
                await Assert.That(results[i].Reasons.ContainsKey(assignment.RoleId)).IsTrue()
                    .Because($"{catalog.DefNames[assignment.RoleId]} has no reason "
                        + $"for pawn {i} (size {size}, seed {seed})");
    }

}
