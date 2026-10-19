using WorkRoles.Core;
using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

// Final insertion behavior cannot prove that geometry/cosmetic changes reuse
// ordering inputs; identity is an independently meaningful cache contract.
public class RecommendationOrderCacheTests
{
    [Test]
    public async Task FullAndTrainingOrderInputsShareEqualArtifactsAndKeepDistinctVariantsWarm()
    {
        var cache = new RecommendationOrderCache();
        object owner = new();
        RoleView[] full = [RecsTestBed.Unskilled(1, "Hauling"), RecsTestBed.Unskilled(2, "Cleaning")];
        RoleView[] equivalentTraining = [RecsTestBed.Unskilled(1, "Hauling"), RecsTestBed.Unskilled(2, "Cleaning")];
        full[0].TemplateDefName = equivalentTraining[0].TemplateDefName = "WS_Basics";
        var first = cache.For(owner, full, 0, [1]);
        await Assert.That(ReferenceEquals(first, cache.For(owner, equivalentTraining, 0, [1]))).IsTrue();
        RoleView[] changedTraining = [RecsTestBed.Unskilled(1, "Hauling"), RecsTestBed.Unskilled(2, "Cleaning")];
        changedTraining[0].TemplateDefName = "WS_Basics";
        changedTraining[1].PreserveRecommendationOrder = true;
        var second = cache.For(owner, changedTraining, 0, [1]);
        await Assert.That(second.Positions[2] < second.Positions[1]).IsTrue();
        await Assert.That(first.Positions[2] > first.Positions[1]).IsTrue();
        await Assert.That(ReferenceEquals(first, cache.For(owner, full, 0, [1]))).IsTrue();
        await Assert.That(ReferenceEquals(second, cache.For(owner, changedTraining, 0, [1]))).IsTrue();
    }

    [Test]
    public async Task OrderingConsumersIgnoreLegacyOverridesUntilPinnabilityChanges()
    {
        var cache = new RecommendationOrderCache();
        object owner = new();
        var basics = RecsTestBed.Unskilled(1, "Hauling");
        var craft = RecsTestBed.Skilled(2, "Crafting", "Crafting", "Make");
        RoleView[] roles = [basics, craft];
        var first = cache.For(owner, roles, 0, [1, 2]);
        await Assert.That(ReferenceEquals(first, cache.For(owner, roles, 0, [1, 2]))).IsTrue();
        await Assert.That(RolePlacement.AfterLeadingIndex([2, 1], 1, first.Positions, first.Positions[1])).IsEqualTo(2);
        // Construct a fresh, semantically equal ordering input like a scalar overlay.
        var otherCraft = RecsTestBed.Skilled(2, "Crafting", "Crafting", "Make");
        otherCraft.ColonyMin = 5;
        await Assert.That(ReferenceEquals(first, cache.For(owner, [basics, otherCraft], 0, [1, 2]))).IsTrue();
        var reordered = cache.For(owner, [basics, otherCraft], 1, [2, 1]);
        await Assert.That(string.Join(",", reordered.Template)).IsEqualTo("1,2");
        await Assert.That(ReferenceEquals(first, reordered)).IsTrue();
        var blocked = RecsTestBed.Skilled(2, "Crafting", "Crafting", "Make");
        blocked.Blocker = true;
        var filtered = cache.For(owner, [basics, blocked], 1, [2, 1]);
        await Assert.That(string.Join(",", filtered.Template)).IsEqualTo("1");
        await Assert.That(ReferenceEquals(filtered, cache.For(new object(), [basics, blocked], 1, [2, 1]))).IsFalse();
        cache.Reset();
        await Assert.That(ReferenceEquals(filtered, cache.For(owner, [basics, blocked], 1, [2, 1]))).IsFalse();
    }
}
