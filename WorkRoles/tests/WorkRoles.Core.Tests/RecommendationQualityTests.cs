using WorkRoles.Core;

namespace WorkRoles.Core.Tests;

/// Certifies recommendation output against the shipped defs on seeded
/// colonies. Rules live here only once the user has ratified them.
public class RecommendationQualityTests
{
    /// The recommendation list is template-monotonic, autos included: what
    /// the Options tab shows is the order every pawn's suggestions follow.
    [Test]
    [Arguments(6, 7)] [Arguments(10, 33)] [Arguments(50, 44)]
    public async Task RecommendationsFollowTheTemplate(int size, int seed)
    {
        var (catalog, _, _, _, recs) = ColonyScenarioTests.ExecutePipeline(size, seed);
        var positions = RecommendationOrder.PositionsFor(catalog.Recs, catalog.OrderTemplate);
        for (int i = 0; i < recs.Count; i++)
        {
            var ids = recs[i].Select(r => r.RoleId).ToList();
            for (int k = 1; k < ids.Count; k++)
                await Assert.That(positions[ids[k - 1]] <= positions[ids[k]]).IsTrue()
                    .Because($"{catalog.DefNames[ids[k - 1]]} outranks {catalog.DefNames[ids[k]]} "
                        + $"in pawn {i}'s recommendations (size {size}, seed {seed})");
        }
    }

    /// Every pinnable shipped role is either in the default template or an
    /// Add-menu candidate — certifies the CORE logic the Options tab calls
    /// (the in-game surface additionally depends on the adapter projection).
    [Test]
    public async Task EveryShippedRoleIsPinnedOrAddable()
    {
        var catalog = ColonyScenarioTests.ShippedCatalog();
        var template = RecommendationOrder.ResolveTemplate(new List<int>(), catalog.Recs);
        var reachable = template
            .Concat(RecommendationOrder.AddCandidates(catalog.Recs, template))
            .ToHashSet();
        foreach (var role in catalog.Recs.Where(RecommendationOrder.IsPinnable))
            await Assert.That(reachable.Contains(role.Id)).IsTrue()
                .Because($"{catalog.DefNames[role.Id]} is neither pinned nor addable");
    }
}
