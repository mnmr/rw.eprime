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

        // Equal recall: precision (tighter coverage) outranks even a matching
        // primary skill on the looser candidate.
        RoleTemplateCandidate precise = RoleTemplateMatcher.Closest(
            user, "Crafting", new[]
            {
                new RoleTemplateCandidate("loose", new[] { "A", "B", "C", "D" }, "Crafting"),
                new RoleTemplateCandidate("tight", new[] { "A", "B" }, "Cooking"),
            });

        await Assert.That(precise.Key).IsEqualTo("tight");
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
