using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests;

public class RoleTemplateMatcherTests
{
    [Test]
    public async Task ClosestTemplatePrefersCoverageRecallThenPrecisionThenSkill()
    {
        var user = new HashSet<string> { "A", "B" };
        var candidates = new[]
        {
            new RoleTemplateCandidate("partial", new[] { "A" }, "Crafting"),
            new RoleTemplateCandidate("broad", new[] { "A", "B", "C" }, "Cooking"),
            new RoleTemplateCandidate("skilled", new[] { "A", "B", "D" }, "Crafting"),
        };

        RoleTemplateCandidate match = RoleTemplateMatcher.Closest(
            user, "Crafting", candidates);

        await Assert.That(match.Key).IsEqualTo("skilled");
    }

    [Test]
    public async Task ClosestTemplateReturnsNullWhenNothingOverlaps()
    {
        RoleTemplateCandidate match = RoleTemplateMatcher.Closest(
            new HashSet<string> { "A" }, "Crafting",
            new[] { new RoleTemplateCandidate("other", new[] { "B" }, "Crafting") });

        await Assert.That(match == null).IsTrue();
    }
}
