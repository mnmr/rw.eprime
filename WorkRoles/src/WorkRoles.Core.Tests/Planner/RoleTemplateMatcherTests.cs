using WorkRoles.Core.Recs;

namespace WorkRoles.Core.Tests.Planner;

public class RoleTemplateMatcherTests
{
    [Test]
    public async Task ClosestTemplatePrefersCoverageRecallThenSkill()
    {
        HashSet<string> user = ["A", "B"];
        RoleTemplateCandidate[] candidates =
        [
            new RoleTemplateCandidate("partial", ["A"], "Crafting"),
            new RoleTemplateCandidate("broad", ["A", "B", "C"], "Cooking"),
            new RoleTemplateCandidate("skilled", ["A", "B", "D"], "Crafting"),
        ];

        RoleTemplateCandidate match = RoleTemplateMatcher.Closest(user, "Crafting", candidates)
            ?? throw new InvalidOperationException("Expected a matching template.");

        await Assert.That(match.Key).IsEqualTo("skilled");
    }

    [Test]
    public async Task ClosestTemplatePrefersPrecisionOverMatchingSkill()
    {
        // Equal recall: precision (tighter coverage) outranks even a matching
        // primary skill on the looser candidate.
        HashSet<string> user = ["A", "B"];
        RoleTemplateCandidate precise = RoleTemplateMatcher.Closest(
            user,
            "Crafting",
            [new RoleTemplateCandidate("loose", ["A", "B", "C", "D"], "Crafting"), new RoleTemplateCandidate("tight", ["A", "B"], "Cooking")]
        ) ?? throw new InvalidOperationException("Expected a precise template match.");

        await Assert.That(precise.Key).IsEqualTo("tight");
    }

    [Test]
    public async Task ClosestTemplateReturnsNullWhenNothingOverlaps()
    {
        RoleTemplateCandidate? match = RoleTemplateMatcher.Closest(["A"], "Crafting", [new RoleTemplateCandidate("other", ["B"], "Crafting")]);

        await Assert.That(match == null).IsTrue();
    }
}
